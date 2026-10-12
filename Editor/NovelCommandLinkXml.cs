using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace NovelForge.Editor
{
    // [NovelCommand] classes are only reached through reflection, so managed code stripping
    // removes their (string rawArgs) constructors and the command becomes "Unknown" in a
    // player build. This hands UnityLinker a generated link.xml that keeps every such class
    // whole, so users never need [Preserve] on their commands.
    public class NovelCommandLinkXml : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var xml = new StringBuilder("<linker>\n");
            IEnumerable<IGrouping<string, Type>> byAssembly = TypeCache.GetTypesWithAttribute<NovelCommandAttribute>()
                .GroupBy(type => type.Assembly.GetName().Name)
                .OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (IGrouping<string, Type> assembly in byAssembly)
            {
                // TypeCache also sees Editor-only and test assemblies, which never reach the player.
                xml.Append("  <assembly fullname=\"").Append(assembly.Key).Append("\" ignoreIfMissing=\"1\">\n");
                foreach (Type type in assembly.OrderBy(t => t.FullName, StringComparer.Ordinal))
                    xml.Append("    <type fullname=\"").Append(type.FullName.Replace('+', '/')).Append("\" preserve=\"all\" />\n");
                xml.Append("  </assembly>\n");
            }
            xml.Append("</linker>\n");

            string path = Path.GetFullPath(Path.Combine("Temp", "NovelForge", "NovelCommands.link.xml"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, xml.ToString());
            return path;
        }
    }
}
