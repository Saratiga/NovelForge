using System.Collections;

namespace NovelForge.Runtime
{
    public interface IDialoguePresenter
    {
        IEnumerator ShowLine(string characterId, string text, string emotion, string position);
        IEnumerator ShowActor(string characterId, string emotion, string position);
    }
}
