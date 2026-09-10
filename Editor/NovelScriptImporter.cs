using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using NovelForge.Runtime;

namespace NovelForge.Editor
{
    [ScriptedImporter(1, "nfscript")]
    public class NovelScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
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
