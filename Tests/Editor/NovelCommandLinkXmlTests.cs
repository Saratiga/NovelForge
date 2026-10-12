using System.IO;
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class NovelCommandLinkXmlTests
    {
        [Test]
        public void GeneratedLinkXml_PreservesEveryNovelCommandTypeWholesale()
        {
            string path = new NovelCommandLinkXml().GenerateAdditionalLinkXmlFile(null, null);
            string xml = File.ReadAllText(path);

            StringAssert.Contains("<assembly fullname=\"NovelForge.Editor.Tests\">", xml);
            StringAssert.Contains("<type fullname=\"NovelForge.Editor.Tests.TestEditorCommand\" preserve=\"all\" />", xml);
            StringAssert.Contains("<type fullname=\"NovelForge.Runtime.Tests.TestPingCommand\" preserve=\"all\" />", xml);
        }
    }
}
