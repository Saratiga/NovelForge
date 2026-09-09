using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ActorView : MonoBehaviour
    {
        [SerializeField] internal SpriteRenderer spriteRenderer;
        [SerializeField] internal float fadeSeconds = 0.3f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();

        public IEnumerator ShowSprite(Sprite sprite)
        {
            if (spriteRenderer == null)
            {
                Debug.LogError("NovelForge: ActorView is missing spriteRenderer — skipping.");
                yield break;
            }

            spriteRenderer.sprite = sprite;
            SetAlpha(0f);

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += TimeSource.DeltaTime;
                SetAlpha(Mathf.Clamp01(t / fadeSeconds));
                yield return null;
            }

            SetAlpha(1f);
        }

        private void SetAlpha(float alpha)
        {
            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }
}
