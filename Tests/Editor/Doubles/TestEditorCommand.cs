using System.Collections;
using NovelForge.Runtime;

namespace NovelForge.Editor.Tests
{
    [NovelCommand("test_editor_cmd")]
    public class TestEditorCommand : Command
    {
        public TestEditorCommand(string rawArgs)
        {
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield break;
        }
    }
}
