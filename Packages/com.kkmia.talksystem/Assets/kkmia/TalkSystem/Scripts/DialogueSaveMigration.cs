using System;

namespace kkmia.TalkSystem
{
    public static class DialogueSaveSchema
    {
        public const int CurrentVersion = 2;
    }

    public enum DialogueMissingDialoguePolicy
    {
        Fail,
        RestoreEnded,
        UseFallbackDialogueId
    }

    [Serializable]
    public sealed class DialogueSaveServiceOptions
    {
        public int CurrentSchemaVersion = DialogueSaveSchema.CurrentVersion;
        public string ContentVersion = string.Empty;
        public string ProductChannel = string.Empty;
        public DialogueMissingDialoguePolicy MissingDialoguePolicy = DialogueMissingDialoguePolicy.Fail;
        public int FallbackDialogueId = -1;

        [NonSerialized] public IDialogueRepository Repository;

        public DialogueSaveServiceOptions Clone()
        {
            return new DialogueSaveServiceOptions
            {
                CurrentSchemaVersion = CurrentSchemaVersion > 0 ? CurrentSchemaVersion : DialogueSaveSchema.CurrentVersion,
                ContentVersion = ContentVersion ?? string.Empty,
                ProductChannel = ProductChannel ?? string.Empty,
                MissingDialoguePolicy = MissingDialoguePolicy,
                FallbackDialogueId = FallbackDialogueId,
                Repository = Repository
            };
        }
    }

    public sealed class DialogueSaveMigrationContext
    {
        public DialogueSaveMigrationContext(DialogueSaveServiceOptions options, int fromSchemaVersion, int toSchemaVersion)
        {
            Options = options != null ? options.Clone() : new DialogueSaveServiceOptions();
            FromSchemaVersion = fromSchemaVersion;
            ToSchemaVersion = toSchemaVersion;
        }

        public DialogueSaveServiceOptions Options { get; private set; }
        public int FromSchemaVersion { get; private set; }
        public int ToSchemaVersion { get; private set; }
        public string ContentVersion { get { return Options.ContentVersion; } }
        public string ProductChannel { get { return Options.ProductChannel; } }
        public IDialogueRepository Repository { get { return Options.Repository; } }
    }

    public interface IDialogueSaveDataMigration
    {
        int FromSchemaVersion { get; }
        int ToSchemaVersion { get; }
        void Migrate(DialogueSaveData data, DialogueSaveMigrationContext context);
    }

    public interface IDialogueSaveSlotMigration
    {
        int FromSchemaVersion { get; }
        int ToSchemaVersion { get; }
        void Migrate(DialogueSaveSlot slot, DialogueSaveMigrationContext context);
    }

    public interface IDialogueSaveStorageProvider
    {
        IDialogueSaveStorage CreateStorage();
    }
}
