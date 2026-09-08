using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class BackgroundPresenter : MonoBehaviour, IBackgroundPresenter
    {
        [SerializeField] internal BackgroundLibrary library;
        [SerializeField] internal SpriteRenderer backgroundSlotA;
        [SerializeField] internal SpriteRenderer backgroundSlotB;
        [SerializeField] internal SpriteRenderer cgSlot;
        [SerializeField] internal float backgroundFadeSeconds = 0.5f;
        [SerializeField] internal float cgFadeSeconds = 0.3f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();

        private SpriteRenderer _activeBackgroundSlot;
        private int _backgroundFadeGeneration;

        public IEnumerator ShowBackground(string backgroundId)
        {
            if (library == null)
            {
                Debug.LogError("NovelForge: BackgroundPresenter is missing library — skipping background change.");
                yield break;
            }

            if (!library.TryGetBackgroundSprite(backgroundId, out var sprite))
            {
                Debug.LogError($"NovelForge: no background sprite registered for id '{backgroundId}' — skipping.");
                yield break;
            }

            if (backgroundSlotA == null || backgroundSlotB == null)
            {
                Debug.LogError("NovelForge: BackgroundPresenter is missing backgroundSlotA/backgroundSlotB — skipping background change.");
                yield break;
            }

            int generation = ++_backgroundFadeGeneration;

            _activeBackgroundSlot ??= backgroundSlotA;
            var from = _activeBackgroundSlot;
            var to = _activeBackgroundSlot == backgroundSlotA ? backgroundSlotB : backgroundSlotA;
            to.sprite = sprite;
            SetAlpha(to, 0f);

            float cgStartAlpha = cgSlot != null ? cgSlot.color.a : 0f;

            float t = 0f;
            while (t < backgroundFadeSeconds)
            {
                t += TimeSource.DeltaTime;
                float ratio = Mathf.Clamp01(t / backgroundFadeSeconds);
                SetAlpha(to, ratio);
                SetAlpha(from, 1f - ratio);
                if (cgSlot != null)
                    SetAlpha(cgSlot, cgStartAlpha * (1f - ratio));
                yield return null;
                if (generation != _backgroundFadeGeneration)
                    yield break;
            }

            if (generation != _backgroundFadeGeneration)
                yield break;

            SetAlpha(to, 1f);
            SetAlpha(from, 0f);
            if (cgSlot != null)
                SetAlpha(cgSlot, 0f);
            _activeBackgroundSlot = to;
        }

        public IEnumerator ShowCg(string cgId)
        {
            if (library == null)
            {
                Debug.LogError("NovelForge: BackgroundPresenter is missing library — skipping CG.");
                yield break;
            }

            if (!library.TryGetCgSprite(cgId, out var sprite))
            {
                Debug.LogError($"NovelForge: no CG sprite registered for id '{cgId}' — skipping.");
                yield break;
            }

            if (cgSlot == null)
            {
                Debug.LogError("NovelForge: BackgroundPresenter is missing cgSlot — skipping CG.");
                yield break;
            }

            cgSlot.sprite = sprite;
            SetAlpha(cgSlot, 0f);

            float t = 0f;
            while (t < cgFadeSeconds)
            {
                t += TimeSource.DeltaTime;
                SetAlpha(cgSlot, Mathf.Clamp01(t / cgFadeSeconds));
                yield return null;
            }

            SetAlpha(cgSlot, 1f);
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }
}
