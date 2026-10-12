using System.Collections;
using NovelForge.Runtime;

namespace NovelForgeSmoke
{
    // Reached only through [NovelCommand] reflection — nothing references this type directly,
    // which is exactly what IL2CPP managed code stripping may remove.
    [NovelCommand("smoke_echo")]
    public class SmokeEchoCommand : Command
    {
        public static string LastArgs;

        public SmokeEchoCommand(string rawArgs) => LastArgs = rawArgs;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield break;
        }
    }
}
