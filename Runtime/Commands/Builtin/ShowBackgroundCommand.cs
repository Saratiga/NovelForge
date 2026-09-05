using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ShowBackgroundCommand : Command
    {
        private readonly string _backgroundId;

        public ShowBackgroundCommand(string backgroundId) => _backgroundId = backgroundId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Backgrounds == null)
            {
                Debug.LogError("NovelForge: no IBackgroundPresenter wired — skipping background change.");
                yield break;
            }
            yield return context.Backgrounds.ShowBackground(_backgroundId);
        }
    }
}
