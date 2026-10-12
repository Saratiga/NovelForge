using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using NovelForge.Runtime;

namespace NovelForge.Editor
{
    [ScriptedImporter(2, "nfscript")]
    public class NovelScriptImporter : ScriptedImporter
    {
        internal static int ImportCountForTesting;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            ImportCountForTesting++;
            ctx.DependsOnCustomDependency(NovelCommandDependency.Name);
            string source = File.ReadAllText(ctx.assetPath);

            try
            {
                new ScriptCompiler().Compile(source);
            }
            catch (ParseException e)
            {
                ctx.LogImportError($"NovelForge: {e.Message}");
            }

            var asset = ScriptableObject.CreateInstance<NovelScriptAsset>();
            asset.Source = source;
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}
