using System.Collections;

namespace NovelForge.Runtime
{
    public class SayLineCommand : Command
    {
        private readonly string _characterId;
        private readonly string _text;
        private readonly string _emotion;
        private readonly string _position;

        public SayLineCommand(string characterId, string text, string emotion, string position)
        {
            _characterId = characterId;
            _text = text;
            _emotion = emotion;
            _position = position;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Dialogue.ShowLine(_characterId, _text, _emotion, _position);
        }
    }
}
