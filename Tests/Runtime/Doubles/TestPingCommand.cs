using System.Collections;

namespace NovelForge.Runtime.Tests
{
    [NovelCommand("test_ping")]
    public class TestPingCommand : Command
    {
        public string Args { get; }

        public TestPingCommand(string rawArgs) => Args = rawArgs;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield break;
        }
    }
}
