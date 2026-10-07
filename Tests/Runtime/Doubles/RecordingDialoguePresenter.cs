using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingDialoguePresenter : IDialoguePresenter
    {
        public readonly List<(string characterId, string text, string emotion, string position)> Calls = new();
        public readonly List<(string characterId, string emotion, string position)> ActorCalls = new();

        public IEnumerator ShowLine(string characterId, string text, string emotion, string position)
        {
            Calls.Add((characterId, text, emotion, position));
            yield break;
        }

        public IEnumerator ShowActor(string characterId, string emotion, string position)
        {
            ActorCalls.Add((characterId, emotion, position));
            yield break;
        }
    }
}
