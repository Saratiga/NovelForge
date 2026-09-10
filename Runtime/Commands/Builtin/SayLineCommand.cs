using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class SayLineCommand : Command
    {
        private readonly string _characterId;
        private readonly string _text;
        private readonly string _emotion;
        private readonly string _position;
        private readonly string _lineId;

        public SayLineCommand(string characterId, string text, string emotion, string position, string lineId)
        {
            _characterId = characterId;
            _text = text;
            _emotion = emotion;
            _position = position;
            _lineId = lineId;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Dialogue == null)
            {
                Debug.LogError("NovelForge: no IDialoguePresenter wired — skipping dialogue line.");
                yield break;
            }
            yield return context.Dialogue.ShowLine(_characterId, ResolveText(context), _emotion, _position);
        }

        private string ResolveText(StoryContext context)
        {
            if (context.Localization == null)
                return _text;
            if (context.Localization.TryGetText(_lineId, out string translated))
                return translated;
            Debug.LogWarning($"NovelForge: no translation for line id '{_lineId}' — falling back to source text.");
            return _text;
        }
    }
}
