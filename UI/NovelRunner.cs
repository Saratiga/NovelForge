using System;
using System.Collections;
using NovelForge.Runtime;
using UnityEngine;

namespace NovelForge.UI
{
    public class NovelRunner : MonoBehaviour
    {
        [SerializeField] internal NovelScriptAsset script;
        [SerializeField] internal DialoguePresenter dialogue;
        [SerializeField] internal ChoiceView choices;
        [SerializeField] internal AudioPresenter audio;
        [SerializeField] internal BackgroundPresenter backgrounds;
        [SerializeField] internal TextAsset localizationJson;

        public StoryContext Context { get; private set; }
        public PlaybackController Playback { get; private set; }
        public ITimingPresenter Timing { get; set; } = new UnityTimingPresenter();
        public event Action OnFinished;

        public void Prepare(NovelScriptAsset scriptToPlay = null)
        {
            Context = null;
            Playback = null;

            NovelScriptAsset target = scriptToPlay != null ? scriptToPlay : script;
            if (target == null)
            {
                Debug.LogError("NovelForge: NovelRunner has no script assigned — call Prepare with a NovelScriptAsset.");
                return;
            }

            NovelScript compiled;
            try
            {
                compiled = new ScriptCompiler().Compile(target.Source);
            }
            catch (ParseException e)
            {
                Debug.LogError($"NovelForge: could not compile script '{target.name}' — {e.Message}");
                return;
            }

            Context = new StoryContext
            {
                Dialogue = dialogue,
                Choices = choices,
                Audio = audio,
                Backgrounds = backgrounds,
                Timing = Timing,
                Localization = localizationJson != null ? LocalizationTable.FromJson(localizationJson.text) : null,
            };
            Playback = new PlaybackController(compiled, Context);
        }

        public void Begin()
        {
            if (Playback == null)
            {
                Debug.LogError("NovelForge: NovelRunner.Begin() called before a successful Prepare().");
                return;
            }
            StartCoroutine(RunAndNotify());
        }

        public void Play(NovelScriptAsset scriptToPlay = null)
        {
            Prepare(scriptToPlay);
            if (Playback != null)
                Begin();
        }

        public void Stop() => StopAllCoroutines();

        internal IEnumerator RunAndNotify()
        {
            yield return Context.Scene.Replay(Context, routine => StartCoroutine(routine));
            yield return Playback.RunAll();
            OnFinished?.Invoke();
        }
    }
}
