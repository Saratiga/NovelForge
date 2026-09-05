using System.Collections;

namespace NovelForge.Runtime
{
    public abstract class Command
    {
        public string SourceComment { get; set; }

        public abstract IEnumerator Execute(StoryContext context, IStoryPointer pointer);
    }
}
