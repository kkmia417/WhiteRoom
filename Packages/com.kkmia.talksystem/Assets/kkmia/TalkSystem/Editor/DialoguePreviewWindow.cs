using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    public sealed class DialoguePreviewWindow : EditorWindow
    {
        private sealed class BoolSetting
        {
            public string Key = string.Empty;
            public bool Value = true;
        }

        private sealed class StringSetting
        {
            public string Key = string.Empty;
            public string Value = string.Empty;
        }

        private sealed class PreviewConditionEvaluator : IDialogueConditionEvaluator
        {
            private readonly Dictionary<string, bool> _values;
            private readonly bool _defaultValue;

            public PreviewConditionEvaluator(Dictionary<string, bool> values, bool defaultValue)
            {
                _values = values ?? new Dictionary<string, bool>();
                _defaultValue = defaultValue;
            }

            public bool Evaluate(string conditionKey, DialogueData data)
            {
                if (string.IsNullOrEmpty(conditionKey))
                    return true;

                bool value;
                return _values.TryGetValue(conditionKey, out value) ? value : _defaultValue;
            }
        }

        private sealed class PreviewVariableResolver : IDialogueVariableResolver
        {
            private readonly Dictionary<string, string> _values;

            public PreviewVariableResolver(Dictionary<string, string> values)
            {
                _values = values ?? new Dictionary<string, string>();
            }

            public bool TryResolve(string variableName, DialogueData data, out string value)
            {
                if (!string.IsNullOrEmpty(variableName) && _values.TryGetValue(variableName, out value))
                    return true;

                value = null;
                return false;
            }
        }

        private static readonly Regex VariablePattern = new Regex(@"\{([A-Za-z0-9_.-]+)\}", RegexOptions.Compiled);

        private TextAsset _csvFile;
        private TextAsset _translationCsvFile;
        private DialogueValidationProfile _validationProfile;
        private int _startId = 1;
        private string _startTriggerKey = string.Empty;
        private string _languageKey = string.Empty;
        private string _fallbackLanguageKey = string.Empty;
        private bool _unknownConditionsPass = true;
        private DialogueRepository _repository;
        private DialogueSession _session;
        private DialogueTranslationTable _translationTable = new DialogueTranslationTable();
        private DialogueValidationReport _validationReport = new DialogueValidationReport();
        private readonly List<BoolSetting> _conditionSettings = new List<BoolSetting>();
        private readonly List<StringSetting> _variableSettings = new List<StringSetting>();
        private Vector2 _scroll;

        [MenuItem("Tools/kkmia/Dialogue Preview")]
        public static void Open()
        {
            GetWindow<DialoguePreviewWindow>("Dialogue Preview");
        }

        public static void Open(TextAsset csvFile)
        {
            var window = GetWindow<DialoguePreviewWindow>("Dialogue Preview");
            window._csvFile = csvFile;
            window.Reload();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            _csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", _csvFile, typeof(TextAsset), false);
            if (EditorGUI.EndChangeCheck())
                Reload();

            EditorGUI.BeginChangeCheck();
            _translationCsvFile = (TextAsset)EditorGUILayout.ObjectField("Translation CSV", _translationCsvFile, typeof(TextAsset), false);
            _languageKey = EditorGUILayout.TextField("Language", _languageKey);
            _fallbackLanguageKey = EditorGUILayout.TextField("Fallback Language", _fallbackLanguageKey);
            _validationProfile = (DialogueValidationProfile)EditorGUILayout.ObjectField("Validation Profile", _validationProfile, typeof(DialogueValidationProfile), false);
            _unknownConditionsPass = EditorGUILayout.Toggle("Unknown Conditions Pass", _unknownConditionsPass);
            if (EditorGUI.EndChangeCheck())
                RefreshPreviewInputs();

            EditorGUILayout.BeginHorizontal();
            _startId = EditorGUILayout.IntField("Start ID", _startId);
            if (GUILayout.Button("Reload"))
                Reload();
            if (GUILayout.Button("Start"))
                StartPreview();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _startTriggerKey = EditorGUILayout.TextField("Start Trigger", _startTriggerKey);
            if (GUILayout.Button("Start Trigger"))
                StartPreviewFromTrigger();
            EditorGUILayout.EndHorizontal();

            if (_session == null)
                return;

            DrawProfiles();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawCurrentLine();
            EditorGUILayout.EndScrollView();
        }

        private void Reload()
        {
            _repository = _csvFile != null ? new DialogueRepository(_csvFile) : null;
            _session = _repository != null ? new DialogueSession(_repository) : null;
            RefreshPreviewInputs();
            Repaint();
        }

        private void StartPreview()
        {
            if (_session == null)
                Reload();

            if (_session != null)
            {
                ApplySessionConfiguration();
                _session.Start(_startId);
                _session.MarkLineReady();
            }
        }

        private void StartPreviewFromTrigger()
        {
            if (_session == null)
                Reload();

            if (_repository == null || _session == null)
                return;

            var data = _repository.GetByTriggerKey(_startTriggerKey);
            if (data == null)
            {
                Debug.LogWarning("[Dialogue Preview] TriggerKey was not found: " + _startTriggerKey);
                return;
            }

            ApplySessionConfiguration();
            _session.Start(data.Id, _startTriggerKey);
            _session.MarkLineReady();
        }

        private void DrawCurrentLine()
        {
            var data = _session.CurrentData;
            if (data == null)
            {
                EditorGUILayout.HelpBox("No active dialogue line.", MessageType.Info);
                return;
            }

            var variableResolver = new PreviewVariableResolver(BuildVariableMap());
            var textResolver = CreateTextResolver();
            var resolvedText = textResolver.Resolve(data, _languageKey, variableResolver);

            EditorGUILayout.LabelField("State", _session.State.ToString());
            EditorGUILayout.LabelField("ID", data.Id.ToString());
            EditorGUILayout.LabelField("Row", data.RowNumber.ToString());
            EditorGUILayout.LabelField("Speaker", data.Speaker);
            EditorGUILayout.LabelField("Resolved Text", resolvedText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Raw Text", data.Text, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("NextId", data.NextId.ToString());
            EditorGUILayout.LabelField("ConditionKey", data.ConditionKey);

            DrawLocalizationWarning(data);
            DrawCueSummary(data);
            DrawValidationMessages(data);

            var choices = _session.CurrentChoices.ToList();
            if (choices.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Active Choices", EditorStyles.boldLabel);
                for (var i = 0; i < choices.Count; i++)
                {
                    if (GUILayout.Button(choices[i].ToString()))
                    {
                        ApplySessionConfiguration();
                        _session.SelectChoice(i);
                        _session.MarkLineReady();
                    }
                }
            }

            DrawAllChoices(data, choices);

            if (choices.Count == 0)
            {
                if (GUILayout.Button(data.NextId >= 0 ? "Next" : "End"))
                {
                    ApplySessionConfiguration();
                    _session.Advance();
                    _session.MarkLineReady();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Seen", string.Join(", ", _session.SeenLineIds.Select(id => id.ToString()).ToArray()));
        }

        private void DrawProfiles()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Condition Profile", EditorStyles.boldLabel);
            if (_conditionSettings.Count == 0)
            {
                EditorGUILayout.HelpBox("No ConditionKey values were found in the loaded CSV.", MessageType.Info);
            }
            else
            {
                for (var i = 0; i < _conditionSettings.Count; i++)
                {
                    var setting = _conditionSettings[i];
                    setting.Value = EditorGUILayout.Toggle(setting.Key, setting.Value);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variable Profile", EditorStyles.boldLabel);
            if (_variableSettings.Count == 0)
            {
                EditorGUILayout.HelpBox("No {variable} placeholders were found in the loaded CSV.", MessageType.Info);
            }
            else
            {
                for (var i = 0; i < _variableSettings.Count; i++)
                {
                    var setting = _variableSettings[i];
                    setting.Value = EditorGUILayout.TextField(setting.Key, setting.Value);
                }
            }
        }

        private void DrawCueSummary(DialogueData data)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cues", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("EventKey", data.EventKey);
            DrawMediaCue("Background", data.GetBackgroundCue());
            DrawMediaCue("Bgm", data.GetBgmCue());
            EditorGUILayout.LabelField("Se", string.Join(", ", data.GetSeKeys().ToArray()));
            EditorGUILayout.LabelField("Voice", data.Voice);

            var directives = data.GetStageDirectives();
            if (directives.Count == 0)
            {
                EditorGUILayout.LabelField("Characters", string.Empty);
                return;
            }

            EditorGUILayout.LabelField("Characters", data.CharactersRaw);
            for (var i = 0; i < directives.Count; i++)
                EditorGUILayout.LabelField("  " + (i + 1), directives[i].ToString());
        }

        private static void DrawMediaCue(string label, DialogueMediaCue cue)
        {
            if (!cue.HasValue)
            {
                EditorGUILayout.LabelField(label, string.Empty);
                return;
            }

            var value = cue.IsClear ? "clear" : cue.Key;
            if (!string.IsNullOrEmpty(cue.Transition))
                value += " #" + cue.Transition;
            if (cue.HasDuration)
                value += " :" + cue.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture);

            EditorGUILayout.LabelField(label, value);
        }

        private void DrawAllChoices(DialogueData data, List<DialogueChoice> activeChoices)
        {
            var allChoices = data.GetChoices().ToList();
            if (allChoices.Count == 0)
                return;

            var active = new HashSet<string>(activeChoices.Select(choice => choice.ToString()));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("All Choices", EditorStyles.boldLabel);
            for (var i = 0; i < allChoices.Count; i++)
            {
                var choice = allChoices[i];
                var isActive = active.Contains(choice.ToString());
                EditorGUILayout.LabelField(isActive ? "Active" : "Hidden", choice.ToString());
            }
        }

        private void DrawLocalizationWarning(DialogueData data)
        {
            if (string.IsNullOrEmpty(_languageKey) || _translationCsvFile == null)
                return;

            string text;
            if (_translationTable.TryGet(data.Id, _languageKey, out text) && !string.IsNullOrWhiteSpace(text))
                return;

            if (!string.IsNullOrEmpty(_fallbackLanguageKey) &&
                _translationTable.TryGet(data.Id, _fallbackLanguageKey, out text) &&
                !string.IsNullOrWhiteSpace(text))
            {
                EditorGUILayout.HelpBox("Language \"" + _languageKey + "\" is empty for Id " + data.Id + "; preview is using fallback \"" + _fallbackLanguageKey + "\".", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("Language \"" + _languageKey + "\" is missing or empty for Id " + data.Id + ".", MessageType.Warning);
        }

        private void DrawValidationMessages(DialogueData data)
        {
            if (_validationReport == null || _validationReport.Messages.Count == 0)
                return;

            var rowMessages = _validationReport.Messages
                .Where(message => message.RowNumber == 0 || message.RowNumber == data.RowNumber)
                .Take(8)
                .ToList();

            if (rowMessages.Count == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            for (var i = 0; i < rowMessages.Count; i++)
                EditorGUILayout.HelpBox(rowMessages[i].ToString(), ToMessageType(rowMessages[i].Severity));
        }

        private static MessageType ToMessageType(DialogueValidationSeverity severity)
        {
            switch (severity)
            {
                case DialogueValidationSeverity.Error:
                    return MessageType.Error;
                case DialogueValidationSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }

        private void RefreshPreviewInputs()
        {
            _translationTable = _translationCsvFile != null
                ? DialogueTranslationTable.FromCsv(_translationCsvFile.text)
                : new DialogueTranslationTable();

            RefreshConditionSettings();
            RefreshVariableSettings();
            RefreshValidationReport();
            ApplySessionConfiguration();
        }

        private void RefreshConditionSettings()
        {
            var previous = _conditionSettings.ToDictionary(setting => setting.Key, setting => setting.Value);
            _conditionSettings.Clear();

            foreach (var key in CollectConditionKeys())
            {
                bool value;
                _conditionSettings.Add(new BoolSetting
                {
                    Key = key,
                    Value = previous.TryGetValue(key, out value) ? value : true
                });
            }
        }

        private void RefreshVariableSettings()
        {
            var previous = _variableSettings.ToDictionary(setting => setting.Key, setting => setting.Value);
            _variableSettings.Clear();

            foreach (var key in CollectVariableKeys())
            {
                string value;
                _variableSettings.Add(new StringSetting
                {
                    Key = key,
                    Value = previous.TryGetValue(key, out value) ? value : string.Empty
                });
            }
        }

        private void RefreshValidationReport()
        {
            if (_repository == null || _validationProfile == null)
            {
                _validationReport = new DialogueValidationReport();
                return;
            }

            _validationReport = DialogueValidator.ValidateData(_repository.GetAll(), null, _validationProfile);
        }

        private void ApplySessionConfiguration()
        {
            if (_session == null)
                return;

            _session.ConditionEvaluator = new PreviewConditionEvaluator(BuildConditionMap(), _unknownConditionsPass);
        }

        private IDialogueTextResolver CreateTextResolver()
        {
            if (_translationCsvFile != null)
                return new LocalizedDialogueTextResolver(_translationTable, _fallbackLanguageKey);

            return new DefaultDialogueTextResolver();
        }

        private Dictionary<string, bool> BuildConditionMap()
        {
            var result = new Dictionary<string, bool>();
            for (var i = 0; i < _conditionSettings.Count; i++)
            {
                var setting = _conditionSettings[i];
                if (!string.IsNullOrEmpty(setting.Key))
                    result[setting.Key] = setting.Value;
            }

            return result;
        }

        private Dictionary<string, string> BuildVariableMap()
        {
            var result = new Dictionary<string, string>();
            for (var i = 0; i < _variableSettings.Count; i++)
            {
                var setting = _variableSettings[i];
                if (!string.IsNullOrEmpty(setting.Key))
                    result[setting.Key] = setting.Value ?? string.Empty;
            }

            return result;
        }

        private IEnumerable<string> CollectConditionKeys()
        {
            if (_repository == null)
                return new List<string>();

            var keys = new SortedSet<string>();
            foreach (var row in _repository.GetAll())
            {
                AddKey(keys, row.ConditionKey);
                foreach (var choice in row.GetChoices())
                    AddKey(keys, choice.ConditionKey);
            }

            return keys;
        }

        private IEnumerable<string> CollectVariableKeys()
        {
            if (_repository == null)
                return new List<string>();

            var keys = new SortedSet<string>();
            foreach (var row in _repository.GetAll())
            {
                CollectVariables(row.Text, keys);

                foreach (var language in _translationTable.LanguageKeys)
                {
                    string localized;
                    if (_translationTable.TryGet(row.Id, language, out localized))
                        CollectVariables(localized, keys);
                }
            }

            return keys;
        }

        private static void CollectVariables(string text, ISet<string> keys)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (Match match in VariablePattern.Matches(text))
                AddKey(keys, match.Groups[1].Value);
        }

        private static void AddKey(ISet<string> keys, string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                keys.Add(key.Trim());
        }
    }
}
