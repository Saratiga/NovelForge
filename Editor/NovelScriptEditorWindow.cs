using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NovelForge.Editor
{
    public class NovelScriptEditorWindow : EditorWindow
    {
        private const string TextAreaControlName = "NovelScriptEditorTextArea";
        private static readonly Dictionary<string, NovelScriptEditorWindow> OpenWindows = new();

        [SerializeField] private string _assetPath;
        [SerializeField] private string _text = string.Empty;
        [SerializeField] private bool _isDirty;
        [SerializeField] private bool _previewMode;
        private string _lastParsedText;
        private ParseException _currentError;
        private Vector2 _scrollPosition;

        // Unity 6000.5+ deprecated the int-based instanceID overloads used across the Editor API
        // (EditorUtility.InstanceIDToObject, AssetDatabase.GetAssetPath(int), and the implicit
        // int->EntityId conversion are all hard compile errors as of this Unity version). The
        // [OnOpenAsset] reflection invoker accepts an EntityId-typed first parameter in place of
        // the historical int, which sidesteps the obsolete conversion entirely.
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(EntityId instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not NovelScriptAsset)
                return false;

            Open(AssetDatabase.GetAssetPath(instanceId));
            return true;
        }

        private static void Open(string assetPath)
        {
            if (OpenWindows.TryGetValue(assetPath, out var existing) && existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<NovelScriptEditorWindow>();
            window._assetPath = assetPath;
            window._text = File.ReadAllText(assetPath);
            window._lastParsedText = null;
            window._isDirty = false;
            window._previewMode = false;
            window.UpdateTitle();
            OpenWindows[assetPath] = window;
            window.Show();
        }

        private void UpdateTitle()
        {
            string fileName = Path.GetFileName(_assetPath);
            titleContent = new GUIContent(_isDirty ? fileName + " *" : fileName);
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_assetPath))
                OpenWindows[_assetPath] = this;
        }

        private void OnLostFocus() => SaveIfDirty();

        private void OnDestroy()
        {
            SaveIfDirty();
            if (_assetPath != null && OpenWindows.TryGetValue(_assetPath, out var registered) && registered == this)
                OpenWindows.Remove(_assetPath);
        }

        private void OnGUI()
        {
            HandleKeyboardShortcuts();
            ReparseIfNeeded();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(_assetPath, EditorStyles.miniLabel);
                _previewMode = GUILayout.Toggle(_previewMode, "Preview", EditorStyles.toolbarButton, GUILayout.Width(70));
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_previewMode)
                DrawPreview();
            else
                DrawEditableTextArea();
            EditorGUILayout.EndScrollView();

            DrawErrorPanel();
        }

        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            if (e.keyCode == KeyCode.S && (e.control || e.command))
            {
                SaveIfDirty();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Tab && GUI.GetNameOfFocusedControl() == TextAreaControlName)
            {
                ShowAutocomplete();
                e.Use();
            }
        }

        // Two separate, single-purpose controls instead of a transparent EditorGUI.TextArea
        // layered over a rich-text GUI.Label at the same Rect: that overlay combination proved
        // unreliable in this environment (confirmed via the IMGUI Debugger — the draw
        // instructions registered correctly, valid non-empty Rect, valid style, but the content
        // never actually rendered on screen — root cause not conclusively identified). This
        // design instead uses only plain, single-purpose EditorGUILayout controls, matching the
        // ones already proven to render correctly elsewhere in this window (the path label, the
        // error HelpBox, the "Go to line" button).
        private void DrawEditableTextArea()
        {
            GUI.SetNextControlName(TextAreaControlName);
            string newText = EditorGUILayout.TextArea(_text, GUILayout.ExpandHeight(true));
            if (newText != _text)
            {
                _text = newText;
                _isDirty = true;
                UpdateTitle();
            }
        }

        private void DrawPreview()
        {
            var previewStyle = new GUIStyle(EditorStyles.textArea) { richText = true, wordWrap = true };
            EditorGUILayout.LabelField(DslSyntaxHighlighter.ToRichText(_text), previewStyle, GUILayout.ExpandHeight(true));
        }

        private void DrawErrorPanel()
        {
            if (_currentError == null)
            {
                EditorGUILayout.HelpBox("No errors.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(_currentError.Message, MessageType.Error);
            if (GUILayout.Button("Go to line", GUILayout.Width(100)))
                JumpToLine(_currentError.LineNumber);
        }

        private void ReparseIfNeeded()
        {
            if (_text == _lastParsedText)
                return;

            _lastParsedText = _text;
            try
            {
                new NovelForge.Runtime.ScriptCompiler().Compile(_text);
                _currentError = null;
            }
            catch (ParseException e)
            {
                _currentError = e;
            }
        }

        private void JumpToLine(int lineNumber)
        {
            // "Go to line" only makes sense against the editable control — switch out of
            // preview automatically so the user always lands somewhere they can actually see
            // and act on the caret position.
            _previewMode = false;

            string[] lines = _text.Replace("\r\n", "\n").Split('\n');
            int targetIndex = Mathf.Clamp(lineNumber - 1, 0, lines.Length - 1);

            int caret = 0;
            for (int i = 0; i < targetIndex; i++)
                caret += lines[i].Length + 1;

            EditorGUI.FocusTextInControl(TextAreaControlName);
            SetCaretPosition(caret);
            Repaint();
        }

        private void ShowAutocomplete()
        {
            int caret = GetCaretPosition();
            IEnumerable<string> commands = CommandRegistry.CreateDefault().RegisteredNames;
            List<string> characters = FindCharacterIds();
            IReadOnlyList<string> candidates = DslAutocompleteProvider.GetCandidates(_text, caret, commands, characters);

            if (candidates.Count == 0)
                return;

            var menu = new GenericMenu();
            foreach (string candidate in candidates)
            {
                string captured = candidate;
                menu.AddItem(new GUIContent(captured), false, () => InsertCandidate(captured, caret));
            }
            menu.ShowAsContext();
        }

        private void InsertCandidate(string candidate, int caret)
        {
            int end = Mathf.Clamp(caret, 0, _text.Length);
            int start = end;
            while (start > 0 && (char.IsLetterOrDigit(_text[start - 1]) || _text[start - 1] == '_'))
                start--;

            _text = _text.Substring(0, start) + candidate + _text.Substring(end);
            _isDirty = true;
            UpdateTitle();
            Repaint();
        }

        private static List<string> FindCharacterIds()
        {
            var ids = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    ids.Add(def.Id);
            }
            return ids;
        }

        // Reading/writing the live cursor position inside an IMGUI TextArea has no public API — this
        // uses UnityEditor's internal recycled TextEditor via reflection, the long-standing community
        // pattern for this. If the field name differs in this Unity version, GetValue returns null and
        // every caller falls back to a safe default (act as if the cursor were at the end of the text)
        // instead of throwing.
        private int GetCaretPosition()
        {
            if (GUI.GetNameOfFocusedControl() != TextAreaControlName)
                return _text.Length;

            if (TryGetRecycledEditor(out TextEditor editor))
                return Mathf.Clamp(editor.cursorIndex, 0, _text.Length);

            return _text.Length;
        }

        private void SetCaretPosition(int position)
        {
            if (!TryGetRecycledEditor(out TextEditor editor))
                return;

            int clamped = Mathf.Clamp(position, 0, _text.Length);
            editor.cursorIndex = clamped;
            editor.selectIndex = clamped;
        }

        private static bool TryGetRecycledEditor(out TextEditor editor)
        {
            FieldInfo field = typeof(EditorGUI).GetField("s_RecycledEditor", BindingFlags.NonPublic | BindingFlags.Static);
            editor = field?.GetValue(null) as TextEditor;
            return editor != null;
        }

        private void SaveIfDirty()
        {
            if (!_isDirty || string.IsNullOrEmpty(_assetPath))
                return;

            File.WriteAllText(_assetPath, _text);
            AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate);
            _isDirty = false;
            UpdateTitle();
        }
    }
}
