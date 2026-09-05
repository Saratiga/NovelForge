using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ShowCgCommand : Command
    {
        private readonly string _cgId;

        public ShowCgCommand(string cgId) => _cgId = cgId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Backgrounds == null)
            {
                Debug.LogError("NovelForge: no IBackgroundPresenter wired — skipping CG.");
                yield break;
            }
            yield return context.Backgrounds.ShowCg(_cgId);
        }
    }
}
