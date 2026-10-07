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
            // The presenter fades any CG out on a background change, so the scene drops it too.
            context.Scene.Background = _backgroundId;
            context.Scene.Cg = null;
            if (context.Backgrounds == null)
            {
                Debug.LogError("NovelForge: no IBackgroundPresenter wired — skipping background change.");
                yield break;
            }
            yield return context.Backgrounds.ShowBackground(_backgroundId);
        }
    }
}
