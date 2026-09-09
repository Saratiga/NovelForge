using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Character Definition", fileName = "CharacterDefinition")]
    public class CharacterDefinition : ScriptableObject
    {
        [Serializable]
        public struct Pose
        {
            public string emotion;
            public Sprite sprite;
        }

        [SerializeField] internal string id;
        [SerializeField] internal string displayName;
        [SerializeField] internal Color nameColor = Color.white;
        [SerializeField] internal Pose[] poses = Array.Empty<Pose>();

        public string Id => id;
        public string DisplayName => displayName;
        public Color NameColor => nameColor;

        public bool TryGetSprite(string emotion, out Sprite sprite)
        {
            foreach (var pose in poses)
            {
                if (pose.emotion == emotion && pose.sprite != null)
                {
                    sprite = pose.sprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }
    }
}
