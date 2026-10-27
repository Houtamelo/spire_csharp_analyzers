using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.GlobalConfig;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Behavioral contract for the variant-payload representation. These tests use
/// the generator driver directly so the public payload API is tested independently
/// of the legacy layout-strategy test matrix.
public sealed class VariantPayloadCoreTests
{
    [Fact]
    public void BasicUnion_EmitsSiblingPayloadsAndTypedDeconstruct()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("Shape", throwOnError: true)!;
        var circle = assembly.GetType("ShapeTypes+Variants+Circle", throwOnError: true)!;
        var rectangle = assembly.GetType("ShapeTypes+Variants+Rectangle", throwOnError: true)!;
        var point = assembly.GetType("ShapeTypes+Variants+Point", throwOnError: true)!;

        Assert.True(circle.IsValueType);
        Assert.True(rectangle.IsValueType);
        Assert.True(point.IsValueType);
        Assert.Equal(typeof(float), circle.GetProperty("radius")!.PropertyType);
        Assert.Equal(typeof(float), rectangle.GetProperty("width")!.PropertyType);
        Assert.Equal(typeof(float), rectangle.GetProperty("height")!.PropertyType);
        Assert.NotNull(circle.GetMethod("Deconstruct"));
        Assert.NotNull(rectangle.GetMethod("Deconstruct"));
        Assert.NotNull(point.GetMethod("Deconstruct"));

        var value = union.GetMethod("Circle")!.Invoke(null, new object[] { 3.5f })!;
        Assert.Equal("Circle", union.GetProperty("kind")!.GetValue(value)!.ToString());
        var payload = union.GetProperty("circle")!.GetValue(value)!;
        Assert.Equal(3.5f, payload.GetType().GetProperty("radius")!.GetValue(payload));
    }

    [Fact]
    public void BasicUnion_ReplacesFlatPropertiesAndUnionDeconstruct()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);

        var union = generated.OutputCompilation.GetTypeByMetadataName("Shape")!;
        Assert.Null(union.GetMembers("radius").FirstOrDefault());
        Assert.Null(union.GetMembers("width").FirstOrDefault());
        Assert.Empty(union.GetMembers("Deconstruct"));
    }

    [Fact]
    public void VariantQualifiedAccessorsRemainHiddenFromCompletion()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("Shape", throwOnError: true)!;
        foreach (var name in new[] { "circle", "rectangle", "point" })
        {
            var property = union.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            var editorBrowsable = property!.GetCustomAttribute<EditorBrowsableAttribute>();
            Assert.NotNull(editorBrowsable);
            Assert.Equal(EditorBrowsableState.Never, editorBrowsable!.State);
        }

        var payload = assembly.GetType("ShapeTypes+Variants+Rectangle", throwOnError: true)!;
        Assert.Null(payload.GetProperty("width")!.GetCustomAttribute<EditorBrowsableAttribute>());
        Assert.Null(payload.GetProperty("height")!.GetCustomAttribute<EditorBrowsableAttribute>());
    }

    [Fact]
    public void AttributeSurfaceRemovesLegacyLayoutAndDeconstructSettings()
    {
        var attributeType = typeof(Houtamelo.Spire.DiscriminatedUnionAttribute);
        Assert.Null(attributeType.GetProperty("Layout"));
        Assert.Null(attributeType.GetProperty("GenerateDeconstruct"));
        Assert.NotNull(attributeType.GetProperty("ReferenceStorage"));
        Assert.Equal(
            new[] { "json", "jsonDiscriminator" },
            attributeType.GetConstructors().Single().GetParameters().Select(parameter => parameter.Name));
        Assert.Equal(
            Houtamelo.Spire.ReferenceStorage.ReadGlobalCfg,
            attributeType.GetProperty("ReferenceStorage")!.GetValue(
                Activator.CreateInstance(attributeType, new object?[] { Houtamelo.Spire.JsonLibrary.None, null })!));
    }

    [Fact]
    public void PayloadFieldsArePrivateAndGettersPreserveDeclarationOrder()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);

        var payload = generated.OutputCompilation.GetTypeByMetadataName("ShapeTypes+Variants+Rectangle")!;
        var fields = payload.GetMembers().OfType<IFieldSymbol>()
            .Where(field => !field.IsImplicitlyDeclared)
            .ToArray();
        Assert.Equal(new[] { "_width", "_height" }, fields.Select(field => field.Name));
        Assert.All(fields, field => Assert.Equal(Accessibility.Private, field.DeclaredAccessibility));
        Assert.Equal(new[] { "width", "height" }, payload.GetMembers().OfType<IPropertySymbol>()
            .Where(property => property.GetMethod is not null)
            .Select(property => property.Name)
            .ToArray());
        Assert.All(payload.GetMembers().OfType<IPropertySymbol>(), property =>
            Assert.Null(property.SetMethod));
    }

    [Fact]
    public void ConcreteUnmanagedFieldsUseCompilerDefaultSequentialPayloadHelpers()
    {
        var generated = Generate(UnmanagedSource());
        AssertNoErrors(generated);

        Assert.Contains("StructLayout", generated.GeneratedSource);
        Assert.DoesNotContain("LayoutKind.Auto", generated.GeneratedSource);
        Assert.Contains("LayoutKind.Explicit", generated.GeneratedSource);
        Assert.Contains("FieldOffset", generated.GeneratedSource);
        Assert.DoesNotContain("KnownSize", generated.GeneratedSource);
        Assert.DoesNotContain("sizeof(", generated.GeneratedSource);
        Assert.DoesNotContain("Pack =", generated.GeneratedSource);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        AssertCompilerDefaultSequentialPayloadHelpers(assembly, "GeometryTypes");

        var union = assembly.GetType("Geometry", throwOnError: true)!;
        var positionType = assembly.GetType("Position", throwOnError: true)!;
        var position = Activator.CreateInstance(positionType)!;
        positionType.GetField("x")!.SetValue(position, 1.5f);
        positionType.GetField("y")!.SetValue(position, -2.25f);

        var value = union.GetMethod("Point")!.Invoke(null, new object[] { position, 7 })!;
        var payload = union.GetProperty("point")!.GetValue(value)!;
        var payloadPosition = payload.GetType().GetProperty("position")!.GetValue(payload)!;
        Assert.Equal(1.5f, positionType.GetField("x")!.GetValue(payloadPosition));
        Assert.Equal(-2.25f, positionType.GetField("y")!.GetValue(payloadPosition));
        Assert.Equal(7, payload.GetType().GetProperty("count")!.GetValue(payload));
    }

    [Fact]
    public void UnknownSizeUnmanagedStructStillGetsAConcretePayload()
    {
        var generated = Generate(UnknownSizeSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("UnknownBlockUnion", throwOnError: true)!;
        var unknownType = assembly.GetType("UnknownBlock", throwOnError: true)!;
        var unknown = Activator.CreateInstance(unknownType)!;
        unknownType.GetField("first")!.SetValue(unknown, 7L);
        unknownType.GetField("second")!.SetValue(unknown, 11L);
        var value = union.GetMethod("Block")!.Invoke(null, new[] { unknown })!;
        var payload = union.GetProperty("block")!.GetValue(value)!;
        var property = payload.GetType().GetProperty("block")!;
        var roundTrip = property.GetValue(payload)!;
        Assert.Equal(7L, unknownType.GetField("first")!.GetValue(roundTrip));
        Assert.Equal(11L, unknownType.GetField("second")!.GetValue(roundTrip));
    }

    [Fact]
    public void ManagedNestedUnionIsNotPlacedInUnmanagedOverlay()
    {
        var generated = Generate(NestedManagedUnionSource());
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var consumer = assembly.GetType("NestedManagedConsumer", throwOnError: true)!;
        Assert.Equal("typed", consumer.GetMethod("ReadTyped")!.Invoke(null, null));
        Assert.Equal("default", consumer.GetMethod("ReadDefault")!.Invoke(null, null));
        Assert.Equal(10L, consumer.GetMethod("ReadTypedCount")!.Invoke(null, null));
        Assert.Equal(26L, consumer.GetMethod("ReadDefaultCount")!.Invoke(null, null));
        AssertExplicitHelpersAreUnmanaged(generated.OutputCompilation);
    }

    [Fact]
    public void StructContainingManagedNestedUnionIsNotPlacedInUnmanagedOverlay()
    {
        var generated = Generate(StructContainingManagedUnionSource());
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var consumer = assembly.GetType("ManagedHolderConsumer", throwOnError: true)!;
        Assert.Equal("holder", consumer.GetMethod("Read")!.Invoke(null, null));
        Assert.Equal(10L, consumer.GetMethod("ReadCount")!.Invoke(null, null));
        AssertExplicitHelpersAreUnmanaged(generated.OutputCompilation);
    }

    [Fact]
    public void NestedUnmanagedUnionRemainsEligibleForValueOverlay()
    {
        var generated = Generate(NestedUnmanagedUnionSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var consumer = assembly.GetType("NestedUnmanagedConsumer", throwOnError: true)!;
        Assert.Equal(42, consumer.GetMethod("Read")!.Invoke(null, null));
    }

    [Fact]
    public void TypedReferenceStorageReusesCompatibleNullableSlots()
    {
        var generated = Generate(ReferenceSource("Typed"));
        AssertNoErrors(generated);

        var source = generated.GeneratedSource;
        Assert.DoesNotContain("object?", source);
        Assert.DoesNotContain("FieldOffset", source);

        var union = generated.OutputCompilation.GetTypeByMetadataName("References")!;
        var storageFields = GetStorageFields(generated.OutputCompilation, "References");
        Assert.Contains(storageFields, field => field.Type.SpecialType == SpecialType.System_String);
        Assert.Contains(storageFields, field => field.Type is IArrayTypeSymbol array &&
            array.ElementType.SpecialType == SpecialType.System_Int32);
    }

    [Fact]
    public void SharedReferenceStorageUsesObjectSlotsWithoutReferenceOverlay()
    {
        var generated = Generate(ReferenceSource("Shared"));
        AssertNoErrors(generated);

        var source = generated.GeneratedSource;
        Assert.Contains("object?", source);
        Assert.DoesNotContain("FieldOffset", source);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("References", throwOnError: true)!;
        var text = union.GetMethod("Text")!.Invoke(null, new object[] { "hello" })!;
        var textPayload = union.GetProperty("text")!.GetValue(text)!;
        Assert.Equal("hello", textPayload.GetType().GetProperty("text")!.GetValue(textPayload));
        var numbersValue = new[] { 2, 4 };
        var numbers = union.GetMethod("Numbers")!.Invoke(null, new object[] { numbersValue })!;
        var numbersPayload = union.GetProperty("numbers")!.GetValue(numbers)!;
        Assert.Same(numbersValue, numbersPayload.GetType().GetProperty("numbers")!.GetValue(numbersPayload));
    }

    [Fact]
    public void SharedReferenceStorageKeepsSimultaneousCandidatesInSeparateSlots()
    {
        var generated = Generate(SimultaneousReferencesSource("Shared"));
        AssertNoErrors(generated);

        var union = generated.OutputCompilation.GetTypeByMetadataName("SimultaneousReferences")!;
        var objectSlots = GetStorageFields(
                generated.OutputCompilation,
                "SimultaneousReferences")
            .Count(field => field.Type.SpecialType == SpecialType.System_Object);
        Assert.True(objectSlots >= 2);
    }

    [Fact]
    public void TypedManagedReferenceContainingStructRemainsTyped()
    {
        var generated = Generate(ManagedStructReferenceSource("Typed"));
        AssertNoErrors(generated);

        var source = generated.GeneratedSource;
        Assert.DoesNotContain("object?", source);
        Assert.Contains("ManagedPair", source);
    }

    [Fact]
    public void TypedStorageReusesTupleNamesAndNullableReferenceAnnotations()
    {
        var generated = Generate(TupleReferenceSource());
        AssertNoErrors(generated);

        var tupleSlots = GetStorageFields(generated.OutputCompilation, "TupleReferences")
            .Where(field => field.Type is INamedTypeSymbol named && named.IsTupleType)
            .ToArray();
        Assert.Single(tupleSlots);
    }

    [Fact]
    public void SharedManagedReferenceContainingStructUsesObjectSlotAndUnboxesPayload()
    {
        var generated = Generate(ManagedStructReferenceSource("Shared"));
        AssertNoErrors(generated);
        Assert.Contains("object?", generated.GeneratedSource);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("ManagedStructReferences", throwOnError: true)!;
        var pairType = assembly.GetType("ManagedPair", throwOnError: true)!;
        var pair = Activator.CreateInstance(pairType)!;
        pairType.GetField("Name")!.SetValue(pair, "pair");
        var value = union.GetMethod("Pair")!.Invoke(null, new[] { pair })!;
        var payload = union.GetProperty("pair")!.GetValue(value)!;
        var payloadPair = payload.GetType().GetProperty("pair")!.GetValue(payload)!;
        Assert.Equal("pair", pairType.GetField("Name")!.GetValue(payloadPair));
    }

    [Fact]
    public void SharedDefaultManagedStructReturnsDefaultPayloadInsteadOfThrowing()
    {
        var generated = Generate(DefaultSharedManagedStructSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("DefaultSharedManaged", throwOnError: true)!;
        var pairType = assembly.GetType("ManagedPair", throwOnError: true)!;
        var value = Activator.CreateInstance(union)!;
        var payload = union.GetProperty("pair")!.GetValue(value)!;
        var pair = payload.GetType().GetProperty("pair")!.GetValue(payload)!;
        Assert.Null(pairType.GetField("Name")!.GetValue(pair));
        Assert.Equal(0, pairType.GetField("Count")!.GetValue(pair));
    }

    [Fact]
    public void SharedSimultaneousManagedStructsUseDistinctValueSlots()
    {
        var generated = Generate(SimultaneousManagedStructSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("SimultaneousManaged", throwOnError: true)!;
        var pairType = assembly.GetType("ManagedPair", throwOnError: true)!;
        var first = Activator.CreateInstance(pairType)!;
        var second = Activator.CreateInstance(pairType)!;
        pairType.GetField("Name")!.SetValue(first, "first");
        pairType.GetField("Name")!.SetValue(second, "second");
        var value = union.GetMethod("Pair")!.Invoke(null, new[] { first, second })!;
        var payload = union.GetProperty("pair")!.GetValue(value)!;
        Assert.Equal(
            "first",
            pairType.GetField("Name")!.GetValue(payload.GetType().GetProperty("first")!.GetValue(payload)));
        Assert.Equal(
            "second",
            pairType.GetField("Name")!.GetValue(payload.GetType().GetProperty("second")!.GetValue(payload)));
    }

    [Fact]
    public void SharedNullableManagedStructPreservesNullAndValue()
    {
        var generated = Generate(NullableManagedStructSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("NullableManaged", throwOnError: true)!;
        var nullValue = union.GetMethod("Optional")!.Invoke(null, new object?[] { null })!;
        var nullPayload = union.GetProperty("optional")!.GetValue(nullValue)!;
        Assert.Null(nullPayload.GetType().GetProperty("pair")!.GetValue(nullPayload));

        var pairType = assembly.GetType("ManagedPair", throwOnError: true)!;
        var pair = Activator.CreateInstance(pairType)!;
        pairType.GetField("Name")!.SetValue(pair, "value");
        var value = union.GetMethod("Optional")!.Invoke(null, new[] { pair })!;
        var payload = union.GetProperty("optional")!.GetValue(value)!;
        var roundTrip = payload.GetType().GetProperty("pair")!.GetValue(payload)!;
        Assert.Equal("value", pairType.GetField("Name")!.GetValue(roundTrip));
    }

    [Fact]
    public void AutoReferenceStorageUsesSharedOnlyAtHalfSlotThreshold()
    {
        var shared = Generate(AutoDistinctReferencesSource());
        var typed = Generate(AutoSameReferenceSource());
        AssertNoErrors(shared);
        AssertNoErrors(typed);

        Assert.Contains("object?", shared.GeneratedSource);
        Assert.DoesNotContain("object?", typed.GeneratedSource);
    }

    [Fact]
    public void ExplicitAutoDoesNotInheritTypedProjectDefault()
    {
        var generated = Generate(
            AutoDistinctReferencesSource(explicitAuto: true),
            new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultReferenceStorage"] = "Typed",
            });
        AssertNoErrors(generated);
        Assert.Contains("object?", generated.GeneratedSource);
    }

    [Fact]
    public void ReadGlobalConfigAndOmissionInheritProjectReferenceStorage()
    {
        foreach (var storage in new[] { "Typed", "Shared" })
        {
            var options = new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultReferenceStorage"] = storage,
            };
            var omitted = Generate(ReferenceSource(null), options);
            var explicitGlobal = Generate(ReferenceSource("ReadGlobalCfg"), options);
            AssertNoErrors(omitted);
            AssertNoErrors(explicitGlobal);
            Assert.Equal(
                omitted.GeneratedSource.Contains("object?", StringComparison.Ordinal),
                explicitGlobal.GeneratedSource.Contains("object?", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void AutoSelectsTypedWhenNoReferenceCandidatesExist()
    {
        var generated = Generate(NoReferenceSource());
        AssertNoErrors(generated);
        Assert.DoesNotContain("object?", generated.GeneratedSource);
    }

    [Fact]
    public void AutoSelectsSharedAtExactlyHalfAndTypedJustOverHalf()
    {
        var exact = Generate(AutoExactlyHalfSource());
        var over = Generate(AutoJustOverHalfSource());
        AssertNoErrors(exact);
        AssertNoErrors(over);
        Assert.Contains("object?", exact.GeneratedSource);
        Assert.DoesNotContain("object?", over.GeneratedSource);
    }

    [Fact]
    public void GenericUnionCopiesParametersConstraintsAndKeepsGenericValueTyped()
    {
        var generated = Generate(GenericSource());
        AssertNoErrors(generated);
        Assert.DoesNotContain("LayoutKind.Auto", generated.GeneratedSource);
        Assert.Contains("LayoutKind.Explicit", generated.GeneratedSource);

        var option = generated.OutputCompilation.GetTypeByMetadataName("Option`1")!;
        var payload = generated.OutputCompilation.GetTypeByMetadataName("OptionTypes1+Variants+Some`1")!;
        Assert.Equal("T", option.TypeParameters[0].Name);
        Assert.True(option.TypeParameters[0].HasValueTypeConstraint);
        Assert.Equal("T", payload.TypeParameters[0].Name);
        Assert.True(payload.TypeParameters[0].HasValueTypeConstraint);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        AssertCompilerDefaultSequentialPayloadHelpers(assembly, "OptionTypes1");
        var closed = assembly.GetType("Option`1")!.MakeGenericType(typeof(int));
        var value = closed.GetMethod("Some")!.Invoke(null, new object[] { 42, 1.5f })!;
        var payloadValue = closed.GetProperty("some")!.GetValue(value)!;
        Assert.Equal(42, payloadValue.GetType().GetProperty("value")!.GetValue(payloadValue));
        Assert.Equal(1.5f, payloadValue.GetType().GetProperty("scale")!.GetValue(payloadValue));
    }

    [Fact]
    public void GenericUnionCopiesMultipleConstraintsToPayloads()
    {
        var generated = Generate(MultiConstraintSource());
        AssertNoErrors(generated);
        var result = generated.OutputCompilation.GetTypeByMetadataName("Result`2")!;
        var payload = generated.OutputCompilation.GetTypeByMetadataName("ResultTypes2+Variants+Ok`2")!;
        Assert.Contains(
            result.TypeParameters[0].ConstraintTypes,
            constraint => constraint.ToDisplayString() == "System.Exception");
        Assert.Contains(
            payload.TypeParameters[0].ConstraintTypes,
            constraint => constraint.ToDisplayString() == "System.Exception");
        Assert.True(result.TypeParameters[1].HasReferenceTypeConstraint);
        Assert.Equal(result.TypeParameters.Select(parameter => parameter.Name),
            payload.TypeParameters.Select(parameter => parameter.Name));
    }

    [Fact]
    public void GenericUnionInsideNonGenericContainerIsValidAndCopiesConstraints()
    {
        var generated = Generate(GenericNestedInNonGenericSource());
        AssertNoErrors(generated);

        var union = generated.OutputCompilation.GetTypeByMetadataName("GenericHost+Option`1")!;
        var payload = generated.OutputCompilation.GetTypeByMetadataName("GenericHost+OptionTypes1+Variants+Some`1")!;
        Assert.True(union.TypeParameters[0].HasValueTypeConstraint);
        Assert.True(payload.TypeParameters[0].HasValueTypeConstraint);
    }

    [Fact]
    public void ExplicitUnmanagedHelperNeverCapturesGenericTypeParameters()
    {
        var generated = Generate(GenericSource());
        AssertNoErrors(generated);
        var explicitHelpers = GetAllNamedTypes(generated.OutputCompilation)
            .Where(type => type.TypeKind == TypeKind.Struct)
            .Where(type => type.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "StructLayoutAttribute" &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is int value &&
                value == (int)System.Runtime.InteropServices.LayoutKind.Explicit))
            .ToArray();
        Assert.DoesNotContain(explicitHelpers, type => type.TypeParameters.Length != 0);
    }

    [Fact]
    public void CSharp9FieldlessPayloadDoesNotRequireExplicitParameterlessConstructor()
    {
        var generated = Generate(BasicSource(), languageVersion: LanguageVersion.CSharp9);
        AssertNoErrors(generated);
        var point = generated.OutputCompilation.GetTypeByMetadataName("ShapeTypes+Variants+Point")!;
        Assert.NotNull(point.GetMembers("Deconstruct").SingleOrDefault());
    }

    [Fact]
    public void ReadonlyUnionStillExposesVariantPayloads()
    {
        var generated = Generate(ReadonlySource());
        AssertNoErrors(generated);
        var union = generated.OutputCompilation.GetTypeByMetadataName("ReadonlyShape")!;
        Assert.True(union.IsValueType);
        Assert.NotNull(union.GetMembers("circle").SingleOrDefault());
    }

    [Fact]
    public void NestedUnionInNonGenericTypeKeepsSiblingPayloadScope()
    {
        var generated = Generate(NestedSource());
        AssertNoErrors(generated);

        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("Outer+Command"));
        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("Outer+CommandTypes+Variants+Run"));
        Assert.Null(generated.OutputCompilation.GetTypeByMetadataName("CommandTypes+Variants+Run"));
    }

    [Fact]
    public void PayloadAccessibilityMatchesImplicitInternalUnion()
    {
        var generated = Generate(ImplicitInternalSource());
        AssertNoErrors(generated);
        var payload = generated.OutputCompilation.GetTypeByMetadataName("ImplicitInternalTypes+Variants+Run")!;
        Assert.Equal(Accessibility.Public, payload.DeclaredAccessibility);
    }

    [Fact]
    public void PayloadAccessibilityMatchesPrivateNestedUnion()
    {
        var generated = Generate(PrivateNestedSource());
        AssertNoErrors(generated);
        var payload = generated.OutputCompilation.GetTypeByMetadataName("PrivateHost+CommandTypes+Variants+Run")!;
        Assert.Equal(Accessibility.Public, payload.DeclaredAccessibility);
    }

    [Fact]
    public void AccessorNamesNormalizeAcronymsAndSuffixFactoryConflicts()
    {
        var generated = Generate(NamingSource());
        AssertNoErrors(generated);

        var union = generated.OutputCompilation.GetTypeByMetadataName("Naming")!;
        Assert.NotNull(union.GetMembers("httpRequest").SingleOrDefault());
        Assert.NotNull(union.GetMembers("url").SingleOrDefault());
        Assert.NotNull(union.GetMembers("run_value").SingleOrDefault());
        Assert.Null(union.GetMembers("run").OfType<IPropertySymbol>().SingleOrDefault());
        Assert.NotNull(union.GetMembers("run").OfType<IMethodSymbol>().SingleOrDefault());
    }

    [Fact]
    public void KeywordParameterNamesRemainUsableInPayloads()
    {
        var generated = Generate(KeywordFieldSource());
        AssertNoErrors(generated);
        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("KeywordFields", throwOnError: true)!;
        var value = union.GetMethod("Token")!.Invoke(null, new object[] { 7, 8 })!;
        var payload = union.GetProperty("token")!.GetValue(value)!;
        Assert.Equal(7, payload.GetType().GetProperty("event")!.GetValue(payload));
        Assert.Equal(8, payload.GetType().GetProperty("class")!.GetValue(payload));
    }

    [Fact]
    public void KeywordFactoryNameAndIsVariantPropertyRemainUsable()
    {
        var generated = Generate(KeywordFactorySource());
        AssertNoErrors(generated);
        var unionSymbol = generated.OutputCompilation.GetTypeByMetadataName("KeywordFactory")!;
        Assert.True(
            unionSymbol.GetMembers("Isevent").Length != 0 ||
            unionSymbol.GetMembers("IsEvent").Length != 0);
        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("KeywordFactory", throwOnError: true)!;
        Assert.NotNull(union.GetMethod("event"));
        Assert.NotNull(union.GetProperty("event_value"));
    }

    [Fact]
    public void ExistingToStringFormattingIsPreserved()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);
        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("Shape", throwOnError: true)!;
        var circle = union.GetMethod("Circle")!.Invoke(null, new object[] { 3.5f })!;
        var point = union.GetMethod("Point")!.Invoke(null, null)!;
        Assert.Equal("Circle(3.5)", circle.ToString());
        Assert.Equal("Point()", point.ToString());
    }

    [Fact]
    public void UnrelatedCustomFieldIsInitializedByGeneratedCSharp9Constructor()
    {
        var generated = Generate(CustomFieldSource(), languageVersion: LanguageVersion.CSharp9);
        AssertNoErrors(generated);
    }

    [Fact]
    public void ManagedStorageHelpersAreGroupedBesideUnionAndRemainNongeneric()
    {
        var generated = Generate(ConcreteReferenceHelperSource());
        AssertNoErrors(generated);
        var union = generated.OutputCompilation.GetTypeByMetadataName("ConcreteReferences")!;
        var helpers = GetAllNamedTypes(generated.OutputCompilation)
            .Where(type => type.Name == "ConcreteReferencesTypes" &&
                SymbolEqualityComparer.Default.Equals(type.ContainingSymbol, union.ContainingSymbol))
            .ToArray();
        Assert.NotEmpty(helpers);
        Assert.All(helpers, helper => Assert.Empty(helper.TypeParameters));
        Assert.DoesNotContain(
            union.GetTypeMembers(),
            type => type.Name == "ConcreteReferencesTypes");
    }

    [Fact]
    public void GenericDependentReferenceFieldRemainsTypedOnUnion()
    {
        var generated = Generate(GenericReferenceSource());
        AssertNoErrors(generated);
        var union = generated.OutputCompilation.GetTypeByMetadataName("GenericReference`1")!;
        Assert.Contains(
            union.GetMembers().OfType<IFieldSymbol>(),
            field => field.Type is ITypeParameterSymbol);
    }

    [Fact]
    public void PrivateNestedPayloadAndPrivateUnmanagedFieldTypeCompile()
    {
        var generated = Generate(PrivateUnmanagedSource());
        AssertNoErrors(generated);
        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("PrivateDataHost+CommandTypes+Variants+Run"));
    }

    [Fact]
    public void SameNameUnionsWithDifferentGenericArityDoNotCollide()
    {
        var generated = Generate(SameNameDifferentAritySource());
        AssertNoErrors(generated);
        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("U"));
        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("U`1"));
        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("UTypes"));
        Assert.NotNull(generated.OutputCompilation.GetTypeByMetadataName("UTypes1"));
    }

    [Fact]
    public void TypedNestedNullableReferencesShareSlotsWithoutGeneratedWarnings()
    {
        var generated = Generate(NestedNullableReferenceSource());
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var compilation = generated.OutputCompilation;
        var nonNullableList = (INamedTypeSymbol)compilation
            .GetTypeByMetadataName("NestedNullableReferencesTypes+Variants+A")!
            .GetMembers("values").OfType<IPropertySymbol>().Single().Type;
        var nullableList = (INamedTypeSymbol)compilation
            .GetTypeByMetadataName("NestedNullableReferencesTypes+Variants+B")!
            .GetMembers("values").OfType<IPropertySymbol>().Single().Type;
        Assert.Equal(NullableAnnotation.NotAnnotated, nonNullableList.TypeArguments[0].NullableAnnotation);
        Assert.Equal(NullableAnnotation.Annotated, nullableList.TypeArguments[0].NullableAnnotation);

        var nonNullableTuple = (INamedTypeSymbol)compilation
            .GetTypeByMetadataName("NestedNullableReferencesTypes+Variants+C")!
            .GetMembers("pair").OfType<IPropertySymbol>().Single().Type;
        var nullableTuple = (INamedTypeSymbol)compilation
            .GetTypeByMetadataName("NestedNullableReferencesTypes+Variants+D")!
            .GetMembers("pair").OfType<IPropertySymbol>().Single().Type;
        Assert.Equal(NullableAnnotation.NotAnnotated, nonNullableTuple.TupleElements[0].Type.NullableAnnotation);
        Assert.Equal(NullableAnnotation.Annotated, nullableTuple.TupleElements[0].Type.NullableAnnotation);

        var storageFields = GetStorageFields(compilation, "NestedNullableReferences");
        var listSlots = storageFields.Count(field =>
            field.Type is INamedTypeSymbol named &&
            named.OriginalDefinition.ToDisplayString().Contains("List", StringComparison.Ordinal));
        var tupleSlots = storageFields.Count(field => field.Type is INamedTypeSymbol named && named.IsTupleType);
        Assert.Equal(1, listSlots);
        Assert.Equal(1, tupleSlots);
    }

    [Fact]
    public void TypedNestedNullableFactoriesAndAccessorsDoNotBoxOrCast()
    {
        var generated = Generate(NestedNullableReferenceSource());
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("NestedNullableReferences", throwOnError: true)!;
        foreach (var methodName in new[] { "A", "B", "C", "D", "get_a", "get_b", "get_c", "get_d" })
        {
            var method = union.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.False(ContainsInstruction(method!, OpCodes.Box), methodName);
            Assert.False(ContainsInstruction(method!, OpCodes.Unbox_Any), methodName);
            Assert.False(ContainsInstruction(method!, OpCodes.Castclass), methodName);
        }
    }

    [Fact]
    public void SharedManagedTuplePayloadsRoundTripAndBoxOnlySharedStorage()
    {
        var generated = Generate(SharedTupleManagedSource());
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var storageFields = GetStorageFields(generated.OutputCompilation, "SharedTupleManaged");
        Assert.True(storageFields.Count(field => field.Type.SpecialType == SpecialType.System_Object) >= 2);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("SharedTupleManaged", throwOnError: true)!;
        var a = union.GetMethod("A")!;
        var b = union.GetMethod("B")!;
        var c = union.GetMethod("C")!;
        Assert.True(ContainsInstruction(a, OpCodes.Box));

        var nullPair = ValueTuple.Create<string, int>(null!, 4);
        var aValue = a.Invoke(null, new object[] { nullPair })!;
        var aPayload = union.GetProperty("a")!.GetValue(aValue)!;
        var aPair = (ValueTuple<string, int>)aPayload.GetType().GetProperty("pair")!.GetValue(aPayload)!;
        Assert.Null(aPair.Item1);
        Assert.Equal(4, aPair.Item2);

        var bValue = b.Invoke(null, new object[] { ValueTuple.Create<string, int>("text", 8) })!;
        var bPayload = union.GetProperty("b")!.GetValue(bValue)!;
        var bPair = (ValueTuple<string, int>)bPayload.GetType().GetProperty("value")!.GetValue(bPayload)!;
        Assert.Equal("text", bPair.Item1);
        Assert.Equal(8, bPair.Item2);

        var cValue = c.Invoke(null, new object[]
        {
            ValueTuple.Create<string, int>("left", 1),
            ValueTuple.Create<string, int>("right", 2),
        })!;
        var cPayload = union.GetProperty("c")!.GetValue(cValue)!;
        var left = (ValueTuple<string, int>)cPayload.GetType().GetProperty("left")!.GetValue(cPayload)!;
        var right = (ValueTuple<string, int>)cPayload.GetType().GetProperty("right")!.GetValue(cPayload)!;
        Assert.Equal("left", left.Item1);
        Assert.Equal("right", right.Item1);
    }

    [Fact]
    public void TypedEquivalentTuplesReuseSlotsButPreserveSimultaneousOccupancy()
    {
        var generated = Generate(TypedTupleOccupancySource());
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var tupleSlots = GetStorageFields(generated.OutputCompilation, "TypedTupleOccupancy")
            .Count(field => field.Type is INamedTypeSymbol named && named.IsTupleType);
        Assert.Equal(2, tupleSlots);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("TypedTupleOccupancy", throwOnError: true)!;
        Assert.False(ContainsInstruction(union.GetMethod("A")!, OpCodes.Box));
        var value = union.GetMethod("A")!.Invoke(null, new object[]
        {
            ValueTuple.Create<string, int>(null!, 1),
            ValueTuple.Create<string, int>("second", 2),
        })!;
        var payload = union.GetProperty("a")!.GetValue(value)!;
        var first = (ValueTuple<string, int>)payload.GetType().GetProperty("first")!.GetValue(payload)!;
        var second = (ValueTuple<string, int>)payload.GetType().GetProperty("second")!.GetValue(payload)!;
        Assert.Null(first.Item1);
        Assert.Equal("second", second.Item1);
    }

    [Fact]
    public void OtherAccessorCollisionsRemainCompilerDiagnostics()
    {
        var generated = Generate(CompilerOwnedCollisionSource());
        Assert.Contains(generated.OutputCompilation.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0102");
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id == "SPIRE_DU018");
    }

    [Fact]
    public void PayloadDeconstructIsTypedAndContainsNoKind()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);

        var payload = generated.OutputCompilation.GetTypeByMetadataName("ShapeTypes+Variants+Rectangle")!;
        var deconstruct = payload.GetMembers("Deconstruct").OfType<IMethodSymbol>().Single();
        Assert.Equal(2, deconstruct.Parameters.Length);
        Assert.Equal("width", deconstruct.Parameters[0].Name);
        Assert.Equal("height", deconstruct.Parameters[1].Name);
        Assert.DoesNotContain(deconstruct.Parameters, parameter => parameter.Name == "kind");
    }

    [Fact]
    public void DefaultValueUsesFirstVariantAndCopiesDoNotAlias()
    {
        var generated = Generate(BasicSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("Shape", throwOnError: true)!;
        var defaultValue = Activator.CreateInstance(union)!;
        Assert.Equal("Circle", union.GetProperty("kind")!.GetValue(defaultValue)!.ToString());

        var consumer = assembly.GetType("CopyConsumer", throwOnError: true)!;
        var result = consumer.GetMethod("CopyAndMutatePayload")!.Invoke(null, null);
        Assert.Equal(2f, result);
    }

    [Fact]
    public void GenericAncestorDiagnosticDoesNotSuppressGeneratedApi()
    {
        var generated = Generate(GenericAncestorSource());
        var diagnostic = Assert.Single(generated.Diagnostics.Where(item => item.Id == "SPIRE_DU018"));
        Assert.Contains("Command", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Container", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Command", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));

        var source = generated.GeneratedSource;
        Assert.Contains("public struct Run", source);
        Assert.DoesNotContain(
            generated.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Id is "CS0759" or "CS0117" or "CS0246");
    }

    [Fact]
    public void IndirectGenericAncestorReportsNearestDeclaringAncestor()
    {
        var generated = Generate(IndirectGenericAncestorSource());
        var diagnostic = Assert.Single(generated.Diagnostics.Where(item => item.Id == "SPIRE_DU018"));
        Assert.Contains("Outer", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("Inner", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("public struct Run", generated.GeneratedSource);
    }

    [Fact]
    public void MultipleGenericAncestorsNameTheNearestGenericAncestor()
    {
        var generated = Generate(MultipleGenericAncestorSource());
        var diagnostic = Assert.Single(generated.Diagnostics.Where(item => item.Id == "SPIRE_DU018"));
        Assert.Contains("Middle", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("Outer", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Command", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Contains("public struct Run", generated.GeneratedSource);
    }

    [Fact]
    public void PatternConsumerUsesTypedPayloadDeconstruct()
    {
        var generated = Generate(PatternConsumerSource());
        AssertNoErrors(generated);
        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("PatternShape", throwOnError: true)!;
        var consumer = assembly.GetType("PatternConsumer", throwOnError: true)!;
        var shape = union.GetMethod("Rectangle")!.Invoke(null, new object[] { 2f, 3f })!;
        Assert.Equal(5f, consumer.GetMethod("Read")!.Invoke(null, new[] { shape }));
    }

    private static GeneratedResult Generate(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var tree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "variant-payload-test.cs");
        var references = UnityGeneratorTestHarness.GetReferences(
            includeUnityReferences: false,
            includeEditorReferences: false);
        var compilation = CSharpCompilation.Create(
            $"VariantPayloadTests_{Guid.NewGuid():N}",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var config = new TestAnalyzerConfigOptionsProvider(
            globalOptions is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(globalOptions));
        var driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                optionsProvider: config)
            .WithUpdatedParseOptions(parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);
        var runResult = driver.GetRunResult();
        var generatedSource = string.Join(
            "\n",
            runResult.GeneratedTrees.Select(treeValue => treeValue.GetText().ToString()));
        return new GeneratedResult(outputCompilation, diagnostics, generatedSource);
    }

    private static void AssertNoErrors(GeneratedResult generated)
    {
        var diagnostics = generated.Diagnostics
            .Concat(generated.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (diagnostics.Length != 0)
        {
            Assert.Fail(string.Join(
                "\n",
                diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")));
        }
    }

    private static void AssertNoWarnings(GeneratedResult generated)
    {
        var diagnostics = generated.Diagnostics
            .Concat(generated.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.Empty(diagnostics);
    }

    private static IReadOnlyList<IFieldSymbol> GetStorageFields(
        Compilation compilation,
        string unionName)
    {
        var fields = new List<IFieldSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        void Visit(INamedTypeSymbol type)
        {
            if (!visited.Add(type))
                return;

            fields.AddRange(type.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic && !field.IsImplicitlyDeclared));
            foreach (var nested in type.GetTypeMembers())
                Visit(nested);
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.Type is INamedTypeSymbol nestedType &&
                    (nestedType.Name.Contains("Data", StringComparison.Ordinal) ||
                     nestedType.Name.Contains("Storage", StringComparison.Ordinal) ||
                     nestedType.Name.Contains("Fields", StringComparison.Ordinal)))
                {
                    Visit(nestedType);
                }
            }
        }

        var union = compilation.GetTypeByMetadataName(unionName);
        if (union is not null)
        {
            var types = GetAllNamedTypes(compilation)
                .Where(type => type.Name.StartsWith(union.Name + "Types", StringComparison.Ordinal) &&
                    SymbolEqualityComparer.Default.Equals(type.ContainingSymbol, union.ContainingSymbol));
            foreach (var container in types)
            {
                var fieldsContainer = container.GetTypeMembers("Fields").SingleOrDefault();
                if (fieldsContainer is not null)
                    Visit(fieldsContainer);
            }
        }

        return fields;
    }

    private static IReadOnlyList<INamedTypeSymbol> GetAllNamedTypes(Compilation compilation)
    {
        var result = new List<INamedTypeSymbol>();

        void VisitNamespace(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
                VisitType(type);
            foreach (var child in ns.GetNamespaceMembers())
                VisitNamespace(child);
        }

        void VisitType(INamedTypeSymbol type)
        {
            result.Add(type);
            foreach (var nested in type.GetTypeMembers())
                VisitType(nested);
        }

        VisitNamespace(compilation.GlobalNamespace);
        return result;
    }

    private static void AssertExplicitHelpersAreUnmanaged(Compilation compilation)
    {
        var explicitHelpers = GetAllNamedTypes(compilation)
            .Where(type => type.TypeKind == TypeKind.Struct)
            .Where(type => type.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "StructLayoutAttribute" &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is int value &&
                value == (int)System.Runtime.InteropServices.LayoutKind.Explicit))
            .ToArray();
        Assert.NotEmpty(explicitHelpers);
        Assert.All(explicitHelpers, helper => Assert.True(helper.IsUnmanagedType, helper.Name));
    }

    private static void AssertCompilerDefaultSequentialPayloadHelpers(
        Assembly assembly,
        string typesName)
    {
        var overlay = assembly.GetType($"{typesName}+Fields+Unmanaged", throwOnError: true)!;
        Assert.True(overlay.IsExplicitLayout, overlay.FullName);
        Assert.False(overlay.IsAutoLayout, overlay.FullName);

        var offsets = overlay.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(field => System.Runtime.InteropServices.Marshal.OffsetOf(overlay, field.Name).ToInt32())
            .ToArray();
        Assert.NotEmpty(offsets);
        Assert.All(offsets, offset => Assert.Equal(0, offset));

        var payloadHelpers = overlay.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(type => type.IsValueType)
            .ToArray();
        Assert.NotEmpty(payloadHelpers);
        Assert.All(payloadHelpers, helper =>
        {
            Assert.True(helper.IsLayoutSequential, helper.FullName);
            Assert.False(helper.IsAutoLayout, helper.FullName);
            Assert.False(helper.IsExplicitLayout, helper.FullName);
        });
    }

    private static bool ContainsInstruction(MethodInfo method, OpCode target)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        if (il is null)
            return false;

        var opCodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opCode => opCode.Value);

        var offset = 0;
        while (offset < il.Length)
        {
            var code = (int)il[offset++];
            if (code == 0xFE)
                code = 0xFE00 | il[offset++];

            if (!opCodes.TryGetValue((short)code, out var opCode))
                return false;
            if (opCode.Value == target.Value)
                return true;

            offset += OperandSize(opCode.OperandType, il, offset);
        }

        return false;
    }

    private static int OperandSize(OperandType operandType, byte[] il, int operandOffset)
        => operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineI => 1,
            OperandType.ShortInlineR => 4,
            OperandType.ShortInlineBrTarget => 1,
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI => 4,
            OperandType.InlineBrTarget => 4,
            OperandType.InlineField => 4,
            OperandType.InlineI8 => 8,
            OperandType.InlineMethod => 4,
            OperandType.InlineR => 8,
            OperandType.InlineSig => 4,
            OperandType.InlineString => 4,
            OperandType.InlineTok => 4,
            OperandType.InlineType => 4,
            OperandType.InlineSwitch => 4 + (ReadInt32(il, operandOffset) * 4),
            _ => throw new InvalidOperationException($"Unsupported operand type: {operandType}"),
        };

    private static int ReadInt32(byte[] il, int offset)
        => il[offset]
            | (il[offset + 1] << 8)
            | (il[offset + 2] << 16)
            | (il[offset + 3] << 24);

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private sealed record GeneratedResult(
        Compilation OutputCompilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        string GeneratedSource);

    private static string BasicSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(float radius);
            [Variant] public static partial Shape Rectangle(float width, float height);
            [Variant] public static partial Shape Point();
        }
        public static class CopyConsumer
        {
            public static float CopyAndMutatePayload()
            {
                var first = Shape.Circle(2f);
                var second = first;
                var payload = new ShapeTypes.Variants.Circle(9f);
                return second.circle.radius;
            }
        }
        """;

    private static string UnmanagedSource() => """
        using Houtamelo.Spire;
        public struct Position { public float x; public float y; }
        [DiscriminatedUnion]
        public partial struct Geometry
        {
            [Variant] public static partial Geometry Point(Position position, int count);
            [Variant] public static partial Geometry Empty();
        }
        """;

    private static string UnknownSizeSource() => """
        using Houtamelo.Spire;
        public struct UnknownBlock { public long first; public long second; public UnknownBlock(long first, long second) { this.first = first; this.second = second; } }
        [DiscriminatedUnion]
        public partial struct UnknownBlockUnion
        {
            [Variant] public static partial UnknownBlockUnion Block(UnknownBlock block);
            [Variant] public static partial UnknownBlockUnion Count(int count);
        }
        """;

    private static string NestedManagedUnionSource() => """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct Inner
        {
            [Variant] public static partial Inner Text(string text);
            [Variant] public static partial Inner Empty();
        }
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct OuterTyped
        {
            [Variant] public static partial OuterTyped Inner(Inner value);
            [Variant] public static partial OuterTyped Count(long first, long second, long third, long fourth);
        }
        [DiscriminatedUnion]
        public partial struct OuterDefault
        {
            [Variant] public static partial OuterDefault Inner(Inner value);
            [Variant] public static partial OuterDefault Count(long first, long second, long third, long fourth);
        }
        public static class NestedManagedConsumer
        {
            public static string ReadTyped()
            {
                var value = OuterTyped.Inner(Inner.Text("typed"));
                return value.inner.value.text.text;
            }
            public static string ReadDefault()
            {
                var value = OuterDefault.Inner(Inner.Text("default"));
                return value.inner.value.text.text;
            }
            public static long ReadTypedCount()
            {
                var value = OuterTyped.Count(1L, 2L, 3L, 4L);
                return value.count.first + value.count.second + value.count.third + value.count.fourth;
            }
            public static long ReadDefaultCount()
            {
                var value = OuterDefault.Count(5L, 6L, 7L, 8L);
                return value.count.first + value.count.second + value.count.third + value.count.fourth;
            }
        }
        """;

    private static string StructContainingManagedUnionSource() => """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Inner
        {
            [Variant] public static partial Inner Text(string text);
            [Variant] public static partial Inner Empty();
        }
        public struct Holder
        {
            public Inner value;
            public int count;
        }
        [DiscriminatedUnion]
        public partial struct OuterHolder
        {
            [Variant] public static partial OuterHolder Holder(Holder holder);
            [Variant] public static partial OuterHolder Longs(long first, long second, long third, long fourth);
        }
        public static class ManagedHolderConsumer
        {
            public static string Read()
            {
                var holder = new Holder { value = Inner.Text("holder"), count = 1 };
                var value = OuterHolder.Holder(holder);
                return value.holder.holder.value.text.text;
            }
            public static long ReadCount()
            {
                var value = OuterHolder.Longs(1L, 2L, 3L, 4L);
                return value.longs.first + value.longs.second + value.longs.third + value.longs.fourth;
            }
        }
        """;

    private static string NestedUnmanagedUnionSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ValueInner
        {
            [Variant] public static partial ValueInner Number(int value);
            [Variant] public static partial ValueInner Zero();
        }
        [DiscriminatedUnion]
        public partial struct ValueOuter
        {
            [Variant] public static partial ValueOuter Inner(ValueInner value);
            [Variant] public static partial ValueOuter Float(float value);
        }
        public static class NestedUnmanagedConsumer
        {
            public static int Read()
            {
                var value = ValueOuter.Inner(ValueInner.Number(42));
                return value.inner.value.number.value;
            }
        }
        """;

    private static string ReferenceSource(string? storage) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion{{(storage is null ? "" : $"(ReferenceStorage = ReferenceStorage.{storage})")}}]
        public partial struct References
        {
            [Variant] public static partial References Text(string text);
            [Variant] public static partial References Numbers(int[] numbers);
            [Variant] public static partial References Optional(string? text);
        }
        """;

    private static string SimultaneousReferencesSource(string storage) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.{{storage}})]
        public partial struct SimultaneousReferences
        {
            [Variant] public static partial SimultaneousReferences Pair(string text, int[] numbers);
            [Variant] public static partial SimultaneousReferences Uri(System.Uri value);
            [Variant] public static partial SimultaneousReferences Builder(System.Text.StringBuilder value);
        }
        """;

    private static string ManagedStructReferenceSource(string storage) => $$"""
        using Houtamelo.Spire;
        public struct ManagedPair { public string Name; public int Count; }
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.{{storage}})]
        public partial struct ManagedStructReferences
        {
            [Variant] public static partial ManagedStructReferences Pair(ManagedPair pair);
            [Variant] public static partial ManagedStructReferences Text(string text);
        }
        """;

    private static string DefaultSharedManagedStructSource() => """
        using Houtamelo.Spire;
        public struct ManagedPair { public string Name; public int Count; }
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct DefaultSharedManaged
        {
            [Variant] public static partial DefaultSharedManaged Pair(ManagedPair pair);
            [Variant] public static partial DefaultSharedManaged Empty();
        }
        """;

    private static string SimultaneousManagedStructSource() => """
        using Houtamelo.Spire;
        public struct ManagedPair { public string Name; public int Count; }
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct SimultaneousManaged
        {
            [Variant] public static partial SimultaneousManaged Pair(ManagedPair first, ManagedPair second);
            [Variant] public static partial SimultaneousManaged Empty();
        }
        """;

    private static string NullableManagedStructSource() => """
        using Houtamelo.Spire;
        public struct ManagedPair { public string Name; public int Count; }
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct NullableManaged
        {
            [Variant] public static partial NullableManaged Optional(ManagedPair? pair);
            [Variant] public static partial NullableManaged Text(string text);
        }
        """;

    private static string TupleReferenceSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct TupleReferences
        {
            [Variant] public static partial TupleReferences Left((string left, int count) pair);
            [Variant] public static partial TupleReferences Right((string right, int count) pair);
            [Variant] public static partial TupleReferences Empty();
        }
        """;

    private static string CompilerOwnedCollisionSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NamingCollision
        {
            public int circle;
            [Variant] public static partial NamingCollision Circle();
        }
        """;

    private static string NoReferenceSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NoReferences
        {
            [Variant] public static partial NoReferences A(int value);
            [Variant] public static partial NoReferences B(float value);
        }
        """;

    private static string AutoExactlyHalfSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct AutoExactlyHalf
        {
            [Variant] public static partial AutoExactlyHalf A(string value);
            [Variant] public static partial AutoExactlyHalf B(int[] value);
        }
        """;

    private static string AutoJustOverHalfSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct AutoJustOverHalf
        {
            [Variant] public static partial AutoJustOverHalf A(string text, int[] values);
            [Variant] public static partial AutoJustOverHalf B(System.Uri value);
        }
        """;

    private static string AutoDistinctReferencesSource(bool explicitAuto = false) => $$"""
        using Houtamelo.Spire;
        [DiscriminatedUnion{{(explicitAuto ? "(ReferenceStorage = ReferenceStorage.Auto)" : "")}}]
        public partial struct AutoReferences
        {
            [Variant] public static partial AutoReferences Text(string value);
            [Variant] public static partial AutoReferences Numbers(int[] value);
            [Variant] public static partial AutoReferences Uri(System.Uri value);
            [Variant] public static partial AutoReferences Builder(System.Text.StringBuilder value);
        }
        """;

    private static string AutoSameReferenceSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct AutoReferences
        {
            [Variant] public static partial AutoReferences A(string value);
            [Variant] public static partial AutoReferences B(string? value);
            [Variant] public static partial AutoReferences C(string value);
            [Variant] public static partial AutoReferences Empty();
        }
        """;

    private static string GenericSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Option<T> where T : unmanaged
        {
            [Variant] public static partial Option<T> Some(T value, float scale);
            [Variant] public static partial Option<T> None();
        }
        """;

    private static string GenericNestedInNonGenericSource() => """
        using Houtamelo.Spire;
        public partial class GenericHost
        {
            [DiscriminatedUnion]
            public partial struct Option<T> where T : unmanaged
            {
                [Variant] public static partial Option<T> Some(T value);
                [Variant] public static partial Option<T> None();
            }
        }
        """;

    private static string MultiConstraintSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Result<TError, TValue>
            where TError : System.Exception
            where TValue : class
        {
            [Variant] public static partial Result<TError, TValue> Ok(TValue value);
            [Variant] public static partial Result<TError, TValue> Error(TError error);
        }
        """;

    private static string ReadonlySource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public readonly partial struct ReadonlyShape
        {
            [Variant] public static partial ReadonlyShape Circle(float radius);
            [Variant] public static partial ReadonlyShape Empty();
        }
        """;

    private static string NestedSource() => """
        using Houtamelo.Spire;
        public static partial class Outer
        {
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(float speed);
                [Variant] public static partial Command Stop();
            }
        }
        """;

    private static string ImplicitInternalSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        partial struct ImplicitInternal
        {
            [Variant] public static partial ImplicitInternal Run(float speed);
        }
        """;

    private static string PrivateNestedSource() => """
        using Houtamelo.Spire;
        public partial class PrivateHost
        {
            [DiscriminatedUnion]
            private partial struct Command
            {
                [Variant] public static partial Command Run(float speed);
            }
        }
        """;

    private static string PatternConsumerSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct PatternShape
        {
            [Variant] public static partial PatternShape Rectangle(float width, float height);
            [Variant] public static partial PatternShape Empty();
        }
        public static class PatternConsumer
        {
            public static float Read(PatternShape shape) => shape switch
            {
                { kind: PatternShape.Kind.Rectangle, rectangle: (var width, var height) } => width + height,
                _ => -1f,
            };
        }
        """;

    private static string NamingSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Naming
        {
            [Variant] public static partial Naming HTTPRequest();
            [Variant] public static partial Naming URL();
            [Variant] public static partial Naming run();
        }
        """;

    private static string KeywordFieldSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct KeywordFields
        {
            [Variant] public static partial KeywordFields Token(int @event, int @class);
        }
        """;

    private static string KeywordFactorySource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct KeywordFactory
        {
            [Variant] public static partial KeywordFactory @event(int value);
        }
        """;

    private static string CustomFieldSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct CustomField
        {
            public int metadata;
            [Variant] public static partial CustomField Value(int value);
        }
        """;

    private static string ConcreteReferenceHelperSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ConcreteReferences
        {
            [Variant] public static partial ConcreteReferences Text(string text);
            [Variant] public static partial ConcreteReferences Numbers(int[] numbers);
        }
        """;

    private static string GenericReferenceSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct GenericReference<T> where T : class
        {
            [Variant] public static partial GenericReference<T> Value(T value, int count);
            [Variant] public static partial GenericReference<T> Empty();
        }
        """;

    private static string PrivateUnmanagedSource() => """
        using Houtamelo.Spire;
        public partial class PrivateDataHost
        {
            private struct Secret { public int Value; }
            [DiscriminatedUnion]
            private partial struct Command
            {
                [Variant] public static partial Command Run(Secret secret);
            }
        }
        """;

    private static string GenericAncestorSource() => """
        using Houtamelo.Spire;
        public partial class Container<T>
        {
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(int value);
            }
        }
        """;

    private static string IndirectGenericAncestorSource() => """
        using Houtamelo.Spire;
        public partial class Outer<T>
        {
            public partial class Inner
            {
                [DiscriminatedUnion]
                public partial struct Command
                {
                    [Variant] public static partial Command Run(int value);
                }
            }
        }
        """;

    private static string MultipleGenericAncestorSource() => """
        using Houtamelo.Spire;
        public partial class Outer<T>
        {
            public partial class Middle<U>
            {
                [DiscriminatedUnion]
                public partial struct Command
                {
                    [Variant] public static partial Command Run(int value);
                }
            }
        }
        """;

    private static string SameNameDifferentAritySource() => """
        using Houtamelo.Spire;
        public struct Position { public float Value; }
        [DiscriminatedUnion]
        public partial struct U
        {
            [Variant] public static partial U A(Position position, string text);
        }
        [DiscriminatedUnion]
        public partial struct U<T> where T : unmanaged
        {
            [Variant] public static partial U<T> A(T value, string text);
        }
        """;

    private static string NestedNullableReferenceSource() => """
        #nullable enable
        using System.Collections.Generic;
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct NestedNullableReferences
        {
            [Variant] public static partial NestedNullableReferences A(List<string> values);
            [Variant] public static partial NestedNullableReferences B(List<string?> values);
            [Variant] public static partial NestedNullableReferences C((string text, int count) pair);
            [Variant] public static partial NestedNullableReferences D((string? value, int count) pair);
        }
        """;

    private static string SharedTupleManagedSource() => """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct SharedTupleManaged
        {
            [Variant] public static partial SharedTupleManaged A((string? label, int count) pair);
            [Variant] public static partial SharedTupleManaged B((string? text, int count) value);
            [Variant] public static partial SharedTupleManaged C((string? left, int count) left, (string? right, int count) right);
        }
        """;

    private static string TypedTupleOccupancySource() => """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct TypedTupleOccupancy
        {
            [Variant] public static partial TypedTupleOccupancy A((string? label, int count) first, (string? label, int count) second);
            [Variant] public static partial TypedTupleOccupancy B((string label, int count) left, (string label, int count) right);
        }
        """;
}
