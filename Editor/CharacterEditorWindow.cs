using System;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NovelForge.Editor
{
    public class CharacterEditorWindow : EditorWindow
    {
        private CharacterDefinition[] _allCharacters = Array.Empty<CharacterDefinition>();
        [SerializeField] private CharacterDefinition _selected;
        private SerializedObject _selectedSerializedObject;
        private CharacterUsageValidator.Result _usageResult;
        private Vector2 _listScrollPosition;
        private Vector2 _detailScrollPosition;

        [MenuItem("NovelForge/Character Editor")]
        public static void ShowWindow()
        {
            GetWindow<CharacterEditorWindow>("Character Editor").Show();
        }

        // Same EntityId-based [OnOpenAsset] shape as NovelScriptEditorWindow's — Unity 6000.5+
        // made the int-based instanceID overloads hard compile errors, EntityId is the replacement.
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(EntityId instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not CharacterDefinition character)
                return false;

            var window = GetWindow<CharacterEditorWindow>("Character Editor");
            window.SelectCharacter(character);
            window.Show();
            return true;
        }

        private void OnEnable()
        {
            RefreshCharacterList();
            if (_selected != null)
                SelectCharacter(_selected);
        }

        private void OnFocus() => RefreshCharacterList();

        private void RefreshCharacterList()
        {
            var characters = new List<CharacterDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (character != null)
                    characters.Add(character);
            }
            characters.Sort((a, b) => string.Compare(DisplayLabel(a), DisplayLabel(b), StringComparison.OrdinalIgnoreCase));
            _allCharacters = characters.ToArray();
        }

        private static string DisplayLabel(CharacterDefinition character)
        {
            if (!string.IsNullOrEmpty(character.DisplayName))
                return character.DisplayName;
            if (!string.IsNullOrEmpty(character.Id))
                return character.Id;
            return character.name;
        }

        private void SelectCharacter(CharacterDefinition character)
        {
            _selected = character;
            _selectedSerializedObject = character != null ? new SerializedObject(character) : null;
            RefreshUsage();
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCharacterList();
                DrawDetailPanel();
            }
        }

        private void DrawCharacterList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
            {
                _listScrollPosition = EditorGUILayout.BeginScrollView(_listScrollPosition);
                foreach (var character in _allCharacters)
                {
                    if (character == null)
                        continue;
                    string label = character == _selected ? "> " + DisplayLabel(character) : DisplayLabel(character);
                    if (GUILayout.Button(label))
                        SelectCharacter(character);
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("New Character"))
                    CreateNewCharacter();
            }
        }

        private void CreateNewCharacter()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Character", "CharacterDefinition", "asset", "Choose where to save the new character.");
            if (string.IsNullOrEmpty(path))
                return;

            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            AssetDatabase.CreateAsset(character, path);
            AssetDatabase.SaveAssets();

            RefreshCharacterList();
            SelectCharacter(character);
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("Select a character on the left, or create a new one.", MessageType.Info);
                    return;
                }

                _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

                _selectedSerializedObject.Update();

                EditorGUILayout.PropertyField(_selectedSerializedObject.FindProperty("id"));
                EditorGUILayout.PropertyField(_selectedSerializedObject.FindProperty("displayName"));
                EditorGUILayout.PropertyField(_selectedSerializedObject.FindProperty("nameColor"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Poses", EditorStyles.boldLabel);
                DrawPoses(_selectedSerializedObject.FindProperty("poses"));

                if (_selectedSerializedObject.ApplyModifiedProperties())
                    RefreshUsage();

                EditorGUILayout.Space();
                DrawUsage();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPoses(SerializedProperty posesProperty)
        {
            for (int i = 0; i < posesProperty.arraySize; i++)
            {
                SerializedProperty element = posesProperty.GetArrayElementAtIndex(i);
                SerializedProperty emotionProp = element.FindPropertyRelative("emotion");
                SerializedProperty spriteProp = element.FindPropertyRelative("sprite");

                using (new EditorGUILayout.HorizontalScope())
                {
                    var sprite = spriteProp.objectReferenceValue as Sprite;
                    Rect previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
                    if (sprite != null && sprite.texture != null)
                    {
                        Rect uv = new Rect(
                            sprite.rect.x / sprite.texture.width,
                            sprite.rect.y / sprite.texture.height,
                            sprite.rect.width / sprite.texture.width,
                            sprite.rect.height / sprite.texture.height);
                        GUI.DrawTextureWithTexCoords(previewRect, sprite.texture, uv);
                    }
                    else
                        EditorGUI.DrawRect(previewRect, new Color(0f, 0f, 0f, 0.1f));

                    EditorGUILayout.PropertyField(emotionProp, GUIContent.none, GUILayout.Width(120));
                    EditorGUILayout.PropertyField(spriteProp, GUIContent.none);

                    // Deleting a SerializedProperty array element mid-loop invalidates the
                    // remaining elements this same OnGUI pass — apply the change and abort
                    // immediately via ExitGUI (Unity's documented mechanism for this exact
                    // situation) rather than continuing to iterate a stale array.
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        posesProperty.DeleteArrayElementAtIndex(i);
                        _selectedSerializedObject.ApplyModifiedProperties();
                        RefreshUsage();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Pose"))
            {
                posesProperty.InsertArrayElementAtIndex(posesProperty.arraySize);
                SerializedProperty newElement = posesProperty.GetArrayElementAtIndex(posesProperty.arraySize - 1);
                newElement.FindPropertyRelative("emotion").stringValue = string.Empty;
                newElement.FindPropertyRelative("sprite").objectReferenceValue = null;
            }
        }

        private void RefreshUsage()
        {
            if (_selected == null)
            {
                _usageResult = default;
                return;
            }

            var scripts = new List<NovelScript>();
            foreach (string guid in AssetDatabase.FindAssets("t:NovelScriptAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(path);
                if (asset == null)
                    continue;

                try
                {
                    scripts.Add(new NovelForge.Runtime.ScriptCompiler().Compile(asset.Source));
                }
                catch (ParseException e)
                {
                    Debug.LogWarning($"NovelForge: skipping '{path}' for character usage validation — {e.Message}");
                }
            }

            _usageResult = CharacterUsageValidator.Validate(_selected, scripts);
        }

        private void DrawUsage()
        {
            if (_usageResult.UsedButNotDefined == null)
                return;
            if (_usageResult.UsedButNotDefined.Count == 0 && _usageResult.DefinedButUnused.Count == 0)
                return;

            EditorGUILayout.LabelField("Usage", EditorStyles.boldLabel);
            foreach (string emotion in _usageResult.UsedButNotDefined)
                EditorGUILayout.HelpBox($"Used in scripts but not defined: '{emotion}'", MessageType.Warning);
            foreach (string emotion in _usageResult.DefinedButUnused)
                EditorGUILayout.HelpBox($"Defined but never used: '{emotion}'", MessageType.Info);
        }
    }
}
