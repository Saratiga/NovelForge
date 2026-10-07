using System;
using System.Collections;
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

        public IEnumerator Replay(StoryContext context, Action<IEnumerator> runDetached = null)
        {
            if (Background != null && context.Backgrounds != null)
                yield return context.Backgrounds.ShowBackground(Background);
            if (Cg != null && context.Backgrounds != null)
                yield return context.Backgrounds.ShowCg(Cg);
            if (Music != null && context.Audio != null)
            {
                // A music crossfade is long and nothing visual depends on it, so a host that
                // can run coroutines in parallel starts it instead of waiting for it.
                IEnumerator music = context.Audio.PlayMusic(Music);
                if (runDetached != null)
                    runDetached(music);
                else
                    yield return music;
            }
            if (context.Dialogue == null)
                yield break;

            var positions = new List<string>(Actors.Keys);
            positions.Sort(string.CompareOrdinal);
            foreach (string position in positions)
            {
                ActorState actor = Actors[position];
                yield return context.Dialogue.ShowActor(actor.CharacterId, actor.Emotion, position.Length == 0 ? null : position);
            }
        }

        public void CopyFrom(SceneState other)
        {
            Background = other?.Background;
            Cg = other?.Cg;
            Music = other?.Music;
            Actors ??= new Dictionary<string, ActorState>();
            Actors.Clear();
            if (other?.Actors == null)
                return;
            foreach (var pair in other.Actors)
            {
                if (pair.Value != null)
                    Actors[pair.Key] = new ActorState { CharacterId = pair.Value.CharacterId, Emotion = pair.Value.Emotion };
            }
        }
    }
}
