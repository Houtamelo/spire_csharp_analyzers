using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityStructCatalog;

internal sealed record Declaration(string File, int Line);
internal sealed record StorageField(string Name, string Type, bool IsImplicit, bool IsFixedBuffer);
internal sealed record StorageSymbol(string Name, ITypeSymbol Type, bool IsImplicit, bool IsFixedBuffer, RefKind RefKind);
internal sealed record StructEntry(string Name, string Status, string[] Reasons, Declaration[] Declarations,
    StorageField[] Fields, bool IsGeneric, bool IsRefLike, bool IsPublic, string[] LayoutAttributes);
internal sealed record CatalogSummary(string SourceRoot, string SourceSha256, int SourceFiles, int ScannedStructs, int TotalStructs, int StructDeclarations,
    int Unmanaged, int Managed, int Unresolved, int UnparsedDeclarations, bool PublicOnly, bool ExcludeGenerics, int MinimumInstanceFields, string Framework, string[] References, string Method);
internal sealed record CatalogResult(StructEntry[] Structs, CatalogSummary Summary);
internal sealed record Verdict(string Status, string Reason);

internal static class Catalog
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    internal static CatalogResult Scan(Options options)
    {
        var files = SourceFiles(options.Source).Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
            throw new ArgumentException($"No C# source files found under {options.Source}.");

        var trees = new List<SyntaxTree>();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(options.Source, file).Replace('\\', '/') + "\0" + text + "\0"));
            var original = CSharpSyntaxTree.ParseText(text, ParseOptions, file);
            var characters = text.ToCharArray();
            // Keep positions stable while exposing disabled code. Conflicting alternatives are unresolved, never selected arbitrarily.
            foreach (var trivia in original.GetRoot().DescendantTrivia(descendIntoTrivia: true).Where(trivia => trivia.IsDirective))
                for (var index = trivia.SpanStart; index < trivia.Span.End; index++)
                    if (characters[index] is not ('\r' or '\n')) characters[index] = ' ';
            trees.Add(CSharpSyntaxTree.ParseText(new string(characters), ParseOptions, file));
        }

        var referencePaths = ReferencePaths(options);
        var compilation = CSharpCompilation.Create("UnityStructInventory", trees,
            referencePaths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, metadataImportOptions: MetadataImportOptions.All));
        if (compilation.GetSpecialType(SpecialType.System_Object).TypeKind == TypeKind.Error)
            throw new ArgumentException("Framework references do not resolve System.Object. Supply a complete runtime assembly directory.");
        var classifier = new Classifier(trees);
        var groups = new Dictionary<INamedTypeSymbol, List<BaseTypeDeclarationSyntax>>(SymbolEqualityComparer.Default);
        var unparsed = new List<StructEntry>();
        foreach (var tree in trees)
        {
            var root = tree.GetRoot();
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var declarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(IsStruct).ToArray();
            var parsedKeywords = new HashSet<int>();
            foreach (var constraint in root.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>())
                foreach (var token in constraint.DescendantTokens().Where(token => token.IsKind(SyntaxKind.StructKeyword)))
                    parsedKeywords.Add(token.SpanStart);
            foreach (var declaration in declarations)
            {
                parsedKeywords.Add(Keyword(declaration).SpanStart);
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                {
                    unparsed.Add(Unparsed(options.Source, declaration.Identifier.Text, tree, declaration.SpanStart));
                    continue;
                }
                if (!groups.TryGetValue(symbol, out var parts)) groups.Add(symbol, parts = new());
                parts.Add(declaration);
            }

            // Syntax recovery must not silently drop declarations from an invalid combination of conditional branches.
            var tokens = SyntaxFactory.ParseTokens(tree.GetText().ToString()).ToArray();
            for (var index = 0; index + 1 < tokens.Length; index++)
                if (tokens[index].IsKind(SyntaxKind.StructKeyword) && tokens[index + 1].IsKind(SyntaxKind.IdentifierToken)
                    && !parsedKeywords.Contains(tokens[index].SpanStart))
                    unparsed.Add(Unparsed(options.Source, tokens[index + 1].ValueText, tree, tokens[index].SpanStart));
        }

        var entries = new List<StructEntry>(unparsed);
        foreach (var (type, parts) in groups)
        {
            var verdict = classifier.Classify(type);
            entries.Add(new StructEntry(type.ToDisplayString(), verdict.Status, verdict.Reason.Length == 0 ? [] : [verdict.Reason],
                parts.Select(part => Location(options.Source, part.SyntaxTree, part.SpanStart)).OrderBy(part => part.File, StringComparer.Ordinal).ThenBy(part => part.Line).ToArray(),
                InstanceStorage(type).Select(field =>
                    new StorageField(field.Name, field.Type.ToDisplayString(), field.IsImplicit, field.IsFixedBuffer)).OrderBy(field => field.Name, StringComparer.Ordinal).ToArray(),
                IsGeneric(type), type.IsRefLikeType, IsPublic(type),
                parts.SelectMany(part => part.AttributeLists.SelectMany(list => list.Attributes))
                    .Where(attribute => attribute.Name.ToString().EndsWith("StructLayout", StringComparison.Ordinal) || attribute.Name.ToString().EndsWith("StructLayoutAttribute", StringComparison.Ordinal))
                    .Select(attribute => attribute.ToString()).Distinct().Order(StringComparer.Ordinal).ToArray()));
        }

        var ordered = entries.Where(entry => (!options.PublicOnly || entry.IsPublic) && (!options.ExcludeGenerics || !entry.IsGeneric)
                && entry.Fields.Length >= options.MinimumInstanceFields)
            .OrderBy(entry => entry.Name, StringComparer.Ordinal).ThenBy(entry => entry.Declarations[0].File, StringComparer.Ordinal).ThenBy(entry => entry.Declarations[0].Line).ToArray();
        return new CatalogResult(ordered, new CatalogSummary(options.Source, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            files.Length, entries.Count, ordered.Length, ordered.Sum(entry => entry.Declarations.Length), ordered.Count(entry => entry.Status == "unmanaged"), ordered.Count(entry => entry.Status == "managed"),
            ordered.Count(entry => entry.Status == "unresolved"), unparsed.Count, options.PublicOnly, options.ExcludeGenerics, options.MinimumInstanceFields, options.Framework ?? ".NET host", referencePaths,
            "All preprocessor branches exposed; recursive Roslyn field classification. Not a compiled Unity target or a size/alignment catalog."));
    }

    private static bool IsStruct(BaseTypeDeclarationSyntax declaration) => declaration is StructDeclarationSyntax
        || declaration is RecordDeclarationSyntax record && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword);

    private static SyntaxToken Keyword(BaseTypeDeclarationSyntax declaration) => declaration is RecordDeclarationSyntax record ? record.ClassOrStructKeyword : ((StructDeclarationSyntax)declaration).Keyword;

    private static Declaration Location(string source, SyntaxTree tree, int position) =>
        new(Path.GetRelativePath(source, tree.FilePath).Replace('\\', '/'), tree.GetText().Lines.GetLineFromPosition(position).LineNumber + 1);

    private static StructEntry Unparsed(string source, string name, SyntaxTree tree, int position) =>
        new(name, "unresolved", ["Declaration could not be parsed or bound after exposing conditional branches."],
            [Location(source, tree, position)], [], false, false, false, []);

    private static bool IsGeneric(INamedTypeSymbol type) => type.Arity != 0 || type.ContainingType is { } parent && IsGeneric(parent);
    private static bool IsPublic(INamedTypeSymbol type) => type.DeclaredAccessibility == Accessibility.Public
        && (type.ContainingType is null || IsPublic(type.ContainingType));

    private static StorageSymbol[] InstanceStorage(INamedTypeSymbol type)
    {
        var fields = type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic)
            .Select(field => new StorageSymbol(field.Name, field.Type, field.IsImplicitlyDeclared, field.IsFixedSizeBuffer, field.RefKind)).ToList();
        // Roslyn exposes source event symbols without their synthesized backing-field symbols.
        foreach (var item in type.GetMembers().OfType<IEventSymbol>().Where(item => !item.IsStatic))
            if (!fields.Any(field => field.Name == item.Name) && item.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax() is VariableDeclaratorSyntax { Parent.Parent: EventFieldDeclarationSyntax }))
                fields.Add(new StorageSymbol(item.Name, item.Type, true, false, RefKind.None));
        return fields.ToArray();
    }

    private static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs")) yield return file;
        foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(child) is ".git" or "bin" or "obj" || new DirectoryInfo(child).LinkTarget is not null) continue;
            foreach (var file in SourceFiles(child)) yield return file;
        }
    }

    private static string[] ReferencePaths(Options options)
    {
        var paths = options.Framework is not null ? [] : ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
            ?? throw new ArgumentException("The running .NET runtime did not expose its reference assemblies.");
        var references = paths.DistinctBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase);
        foreach (var input in options.Framework is null ? options.References : new[] { options.Framework }.Concat(options.References))
        {
            IEnumerable<string> candidates = Directory.Exists(input) ? Directory.EnumerateFiles(input, "*.dll", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetRelativePath(input, path).Count(character => character == Path.DirectorySeparatorChar)).ThenBy(path => path, StringComparer.Ordinal) : new[] { input };
            foreach (var path in candidates)
            {
                // Prefer the selected framework and runtime implementations over supplementary facades.
                var name = Path.GetFileNameWithoutExtension(path);
                if (!references.ContainsKey(name))
                {
                    _ = System.Reflection.AssemblyName.GetAssemblyName(path);
                    references.Add(name, path);
                }
            }
        }
        return references.Values.Order(StringComparer.Ordinal).ToArray();
    }

    private sealed class Classifier
    {
        private readonly Dictionary<ITypeSymbol, Verdict> cache = new(SymbolEqualityComparer.Default);
        private readonly HashSet<ITypeSymbol> visiting = new(SymbolEqualityComparer.Default);
        private readonly HashSet<string> ambiguousAliasFiles = new(StringComparer.Ordinal);
        private readonly bool ambiguousGlobalAliases;

        internal Classifier(IEnumerable<SyntaxTree> trees)
        {
            var aliases = trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()).Where(directive => directive.Alias is not null).ToArray();
            foreach (var group in aliases.Where(alias => !alias.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                .GroupBy(alias => (alias.SyntaxTree.FilePath, alias.Parent!.SpanStart, alias.Alias!.Name.Identifier.ValueText)))
                if (group.Count() > 1) ambiguousAliasFiles.Add(group.Key.FilePath);
            ambiguousGlobalAliases = aliases.Where(alias => alias.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                .GroupBy(alias => alias.Alias!.Name.Identifier.ValueText, StringComparer.Ordinal).Any(group => group.Count() > 1);
        }

        internal Verdict Classify(ITypeSymbol type)
        {
            if (cache.TryGetValue(type, out var cached)) return cached;
            if (type.TypeKind == TypeKind.Error) return new("unresolved", $"Unresolved or ambiguous type: {type}.");
            if (type is ITypeParameterSymbol parameter)
                return parameter.HasUnmanagedTypeConstraint ? new("unmanaged", "")
                    : parameter.HasReferenceTypeConstraint ? new("managed", $"Reference-constrained type parameter: {type}.")
                    : new("unresolved", $"Type parameter {type} has no unmanaged constraint.");
            if (type.IsReferenceType || type.TypeKind is TypeKind.Array or TypeKind.Dynamic)
                return new("managed", $"Reference type: {type}.");
            if (type is IPointerTypeSymbol pointer)
                return pointer.PointedAtType.TypeKind == TypeKind.Error ? new("unresolved", $"Unresolved pointer element: {type}.") : new("unmanaged", "");
            if (type is IFunctionPointerTypeSymbol) return new("unmanaged", "");
            if (type is not INamedTypeSymbol named || !named.IsValueType)
                return new("unresolved", $"Unsupported field type: {type}.");
            if (named.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte
                or SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32
                or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single
                or SpecialType.System_Double or SpecialType.System_IntPtr or SpecialType.System_UIntPtr)
                return new("unmanaged", "");
            if (named.TypeKind == TypeKind.Enum)
                return named.EnumUnderlyingType is { TypeKind: not TypeKind.Error } ? new("unmanaged", "") : new("unresolved", $"Invalid enum underlying type: {type}.");
            if (!visiting.Add(type)) return new("unresolved", $"Recursive value-type storage: {type}.");
            if (visiting.Count > 128)
            {
                visiting.Remove(type);
                return new("unresolved", $"Value-type recursion limit: {type}.");
            }
            var result = Inspect(named);
            visiting.Remove(type);
            cache[type] = result;
            return result;
        }

        private Verdict Inspect(INamedTypeSymbol type)
        {
            var declarations = type.OriginalDefinition.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<TypeDeclarationSyntax>().ToArray();
            if (declarations.Length > 1 && declarations.Any(declaration => !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
                return new("unresolved", "Multiple non-partial declarations, possibly from alternative conditional branches.");
            if (declarations.Any(declaration => declaration.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)))
                return new("unresolved", "Syntax errors in source declaration after exposing conditional branches.");
            if (declarations.Length != 0 && (ambiguousGlobalAliases || declarations.Any(declaration => ambiguousAliasFiles.Contains(declaration.SyntaxTree.FilePath))))
                return new("unresolved", "Conflicting using aliases after exposing conditional branches.");

            var fields = InstanceStorage(type);
            if (fields.GroupBy(field => field.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return new("unresolved", "Conflicting instance-field declarations, possibly from alternative conditional branches.");
            Verdict? unresolved = null;
            foreach (var field in fields)
            {
                var verdict = field.RefKind != RefKind.None ? new Verdict("managed", "By-reference field storage.") : Classify(field.Type);
                if (verdict.Status == "managed") return new("managed", $"{type}.{field.Name}: {verdict.Reason}");
                if (verdict.Status == "unresolved") unresolved ??= new("unresolved", $"{type}.{field.Name}: {verdict.Reason}");
            }
            if (unresolved is not null) return unresolved;
            return type.IsUnmanagedType ? new("unmanaged", "") : new("unresolved", $"Roslyn does not establish {type} as unmanaged.");
        }
    }
}
