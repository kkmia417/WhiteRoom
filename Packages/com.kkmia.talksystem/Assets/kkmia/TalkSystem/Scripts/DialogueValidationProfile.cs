using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [CreateAssetMenu(menuName = "kkmia/Talk System/Dialogue Validation Profile")]
    public sealed class DialogueValidationProfile : ScriptableObject
    {
        [SerializeField] private List<TextAsset> csvFiles = new List<TextAsset>();
        [SerializeField] private CharacterExpressionDatabase characterDatabase;
        [SerializeField] private BackgroundDatabase backgroundDatabase;
        [SerializeField] private AudioDatabase audioDatabase;
        [SerializeField] private DialogueKeyCatalog eventKeyCatalog;
        [SerializeField] private DialogueKeyCatalog conditionKeyCatalog;
        [SerializeField] private DialogueKeyCatalog variableCatalog;
        [SerializeField] private DialogueKeyCatalog chapterKeyCatalog;
        [SerializeField] private DialogueKeyCatalog routeKeyCatalog;
        [SerializeField] private DialogueKeyCatalog endingKeyCatalog;
        [SerializeField] private List<TextAsset> translationCsvFiles = new List<TextAsset>();
        [SerializeField] private List<string> localizationLanguageKeys = new List<string>();
        [SerializeField] private string fallbackLanguageKey = string.Empty;
        [SerializeField] private DialogueValidationSeverity localizationSeverity = DialogueValidationSeverity.Error;
        [SerializeField] private DialogueValidationSeverity missingReferenceSeverity = DialogueValidationSeverity.Warning;
        [SerializeField] private bool runAsBuildGate = true;
        [SerializeField] private bool failBuildOnErrors = true;

        public IReadOnlyList<TextAsset> CsvFiles
        {
            get { return csvFiles; }
        }

        public CharacterExpressionDatabase CharacterDatabase
        {
            get { return characterDatabase; }
            set { characterDatabase = value; }
        }

        public BackgroundDatabase BackgroundDatabase
        {
            get { return backgroundDatabase; }
            set { backgroundDatabase = value; }
        }

        public AudioDatabase AudioDatabase
        {
            get { return audioDatabase; }
            set { audioDatabase = value; }
        }

        public DialogueKeyCatalog EventKeyCatalog
        {
            get { return eventKeyCatalog; }
            set { eventKeyCatalog = value; }
        }

        public DialogueKeyCatalog ConditionKeyCatalog
        {
            get { return conditionKeyCatalog; }
            set { conditionKeyCatalog = value; }
        }

        public DialogueKeyCatalog VariableCatalog
        {
            get { return variableCatalog; }
            set { variableCatalog = value; }
        }

        public DialogueKeyCatalog ChapterKeyCatalog
        {
            get { return chapterKeyCatalog; }
            set { chapterKeyCatalog = value; }
        }

        public DialogueKeyCatalog RouteKeyCatalog
        {
            get { return routeKeyCatalog; }
            set { routeKeyCatalog = value; }
        }

        public DialogueKeyCatalog EndingKeyCatalog
        {
            get { return endingKeyCatalog; }
            set { endingKeyCatalog = value; }
        }

        public IReadOnlyList<TextAsset> TranslationCsvFiles
        {
            get { return translationCsvFiles; }
        }

        public IReadOnlyList<string> LocalizationLanguageKeys
        {
            get { return localizationLanguageKeys; }
        }

        public string FallbackLanguageKey
        {
            get { return fallbackLanguageKey; }
            set { fallbackLanguageKey = value ?? string.Empty; }
        }

        public DialogueValidationSeverity LocalizationSeverity
        {
            get { return localizationSeverity; }
            set { localizationSeverity = value; }
        }

        public DialogueValidationSeverity MissingReferenceSeverity
        {
            get { return missingReferenceSeverity; }
            set { missingReferenceSeverity = value; }
        }

        public bool RunAsBuildGate
        {
            get { return runAsBuildGate; }
            set { runAsBuildGate = value; }
        }

        public bool FailBuildOnErrors
        {
            get { return failBuildOnErrors; }
            set { failBuildOnErrors = value; }
        }

        public void SetCsvFiles(IEnumerable<TextAsset> files)
        {
            csvFiles.Clear();
            if (files == null) return;

            foreach (var file in files)
            {
                if (file != null)
                    csvFiles.Add(file);
            }
        }

        public void SetTranslationCsvFiles(IEnumerable<TextAsset> files)
        {
            translationCsvFiles.Clear();
            if (files == null) return;

            foreach (var file in files)
            {
                if (file != null)
                    translationCsvFiles.Add(file);
            }
        }

        public void SetLocalizationLanguageKeys(IEnumerable<string> languageKeys)
        {
            localizationLanguageKeys.Clear();
            if (languageKeys == null) return;

            foreach (var languageKey in languageKeys)
            {
                if (!string.IsNullOrWhiteSpace(languageKey))
                    localizationLanguageKeys.Add(languageKey.Trim());
            }
        }
    }
}
