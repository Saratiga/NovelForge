using System;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEditor;

namespace NovelForge.Editor
{
    public static class CharacterUsageValidator
    {
        public readonly struct Result
        {
            public IReadOnlyList<string> UsedButNotDefined { get; }
            public IReadOnlyList<string> DefinedButUnused { get; }

            public Result(IReadOnlyList<string> usedButNotDefined, IReadOnlyList<string> definedButUnused)
            {
                UsedButNotDefined = usedButNotDefined;
                DefinedButUnused = definedButUnused;
            }
        }

        public static Result Validate(CharacterDefinition character, IEnumerable<NovelScript> scripts)
        {
            var usedEmotions = new HashSet<string>();
            foreach (NovelScript script in scripts)
            {
                foreach (Command command in script.Commands)
                {
                    if (command is SayLineCommand say && say.CharacterId == character.Id && say.Emotion != null)
                        usedEmotions.Add(say.Emotion);
                }
            }

            var definedEmotions = new HashSet<string>(GetDefinedEmotions(character));

            var usedButNotDefined = new List<string>();
            foreach (string emotion in usedEmotions)
            {
                if (!character.TryGetSprite(emotion, out _))
                    usedButNotDefined.Add(emotion);
            }

            var definedButUnused = new List<string>();
            foreach (string emotion in definedEmotions)
            {
                if (!usedEmotions.Contains(emotion))
                    definedButUnused.Add(emotion);
            }

            usedButNotDefined.Sort(StringComparer.Ordinal);
            definedButUnused.Sort(StringComparer.Ordinal);

            return new Result(usedButNotDefined, definedButUnused);
        }

        // `poses` is `internal` on CharacterDefinition with no InternalsVisibleTo grant to
        // NovelForge.Editor (see Global Constraints) — SerializedObject is the only way to
        // enumerate it from here, the same mechanism CharacterEditorWindow uses to edit it.
        private static List<string> GetDefinedEmotions(CharacterDefinition character)
        {
            var emotions = new List<string>();
            var serializedObject = new SerializedObject(character);
            SerializedProperty posesProperty = serializedObject.FindProperty("poses");
            for (int i = 0; i < posesProperty.arraySize; i++)
            {
                string emotion = posesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("emotion").stringValue;
                if (!string.IsNullOrEmpty(emotion))
                    emotions.Add(emotion);
            }
            return emotions;
        }
    }
}
