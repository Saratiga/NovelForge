using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class ActorState
    {
        public string CharacterId;
        public string Emotion;
    }

    // What the script has put on screen so far. Written by content commands, saved with
    // the game, and replayed through the presenters after a load.
    public class SceneState
    {
        public string Background;
        public string Cg;
        public string Music;
        // Keyed by position; a line without a position uses "".
        public Dictionary<string, ActorState> Actors = new();

        public void CopyFrom(SceneState other)
        {
            Background = other?.Background;
            Cg = other?.Cg;
            Music = other?.Music;
            Actors.Clear();
            if (other == null)
                return;
            foreach (var pair in other.Actors)
                Actors[pair.Key] = new ActorState { CharacterId = pair.Value.CharacterId, Emotion = pair.Value.Emotion };
        }
    }
}
