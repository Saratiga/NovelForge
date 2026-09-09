using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Character Library", fileName = "CharacterLibrary")]
    public class CharacterLibrary : ScriptableObject
    {
        [SerializeField] internal CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();

        public bool TryGetCharacter(string id, out CharacterDefinition character)
        {
            foreach (var entry in characters)
            {
                if (entry != null && entry.Id == id)
                {
                    character = entry;
                    return true;
                }
            }

            character = null;
            return false;
        }
    }
}
