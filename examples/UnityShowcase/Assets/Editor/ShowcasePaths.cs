using System;
using System.IO;
using UnityEditor;

namespace Houtamelo.Spire.Showcase.Editor
{
    public static class ShowcasePaths
    {
        private const string RuntimeAssemblyGuid = "de1fa9429c4a323daa60cd365f1c7de3";

        public static string Root => ResolveRoot(RuntimeAssemblyGuid);
        public static string Scene => Root + "/Scenes/Showcase.unity";
        public static string Preset => Root + "/Examples/ShowcasePreset.asset";
        public static string Pipeline => Root + "/ShowcaseURP.asset";
        public static string Renderer => Root + "/URP_RendererData.asset";

        public static string ResolveRoot(string assemblyGuid)
        {
            var assemblyPath = AssetDatabase.GUIDToAssetPath(assemblyGuid);
            if (string.IsNullOrEmpty(assemblyPath))
                throw new InvalidOperationException("Import the Spire Unity Showcase sample before using its authoring tools.");

            return Path.GetDirectoryName(Path.GetDirectoryName(assemblyPath))!.Replace('\\', '/');
        }
    }
}
