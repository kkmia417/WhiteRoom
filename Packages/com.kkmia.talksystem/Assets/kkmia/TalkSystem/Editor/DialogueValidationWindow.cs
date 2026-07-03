using System.Linq;
using UnityEditor;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    public sealed class DialogueValidationWindow : EditorWindow
    {
        private TextAsset _csvFile;
        private DialogueValidationProfile _profile;
        private Vector2 _scroll;
        private DialogueValidationReport _report = new DialogueValidationReport();
        private DialogueValidationSeverity _minimumSeverity = DialogueValidationSeverity.Info;

        [MenuItem("Tools/kkmia/Dialogue Validator")]
        public static void Open()
        {
            GetWindow<DialogueValidationWindow>("Dialogue Validator");
        }

        public static void Open(TextAsset csvFile)
        {
            var window = GetWindow<DialogueValidationWindow>("Dialogue Validator");
            window._csvFile = csvFile;
            window.Validate();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            _csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", _csvFile, typeof(TextAsset), false);
            _profile = (DialogueValidationProfile)EditorGUILayout.ObjectField("Validation Profile", _profile, typeof(DialogueValidationProfile), false);
            _minimumSeverity = (DialogueValidationSeverity)EditorGUILayout.EnumPopup("Minimum Severity", _minimumSeverity);

            if (GUILayout.Button("Validate"))
                Validate();

            EditorGUILayout.Space();
            DrawSummary();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var message in _report.Messages.Where(IsVisible))
                DrawMessage(message);
            EditorGUILayout.EndScrollView();
        }

        private void Validate()
        {
            if (_profile != null && _csvFile == null)
                _report = DialogueValidationRunner.ValidateProfile(_profile);
            else
                _report = _csvFile != null ? DialogueValidator.ValidateCsv(_csvFile.text, null, _profile) : new DialogueValidationReport();

            Repaint();
        }

        private void DrawSummary()
        {
            var errors = _report.Messages.Count(m => m.Severity == DialogueValidationSeverity.Error);
            var warnings = _report.Messages.Count(m => m.Severity == DialogueValidationSeverity.Warning);
            var info = _report.Messages.Count(m => m.Severity == DialogueValidationSeverity.Info);
            EditorGUILayout.LabelField("Errors: " + errors + "  Warnings: " + warnings + "  Info: " + info, EditorStyles.boldLabel);
        }

        private void DrawMessage(DialogueValidationMessage message)
        {
            var style = message.Severity == DialogueValidationSeverity.Error
                ? EditorStyles.helpBox
                : EditorStyles.label;

            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.LabelField(message.ToString(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private bool IsVisible(DialogueValidationMessage message)
        {
            return message.Severity >= _minimumSeverity;
        }
    }
}
