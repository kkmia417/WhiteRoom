using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// 複数スロットのセーブ/ロードを束ねるエントリポイント。会話本体は <see cref="DialogueManager"/> から
    /// capture/restore し、演出系の完全復元は <see cref="IDialogueSaveContributor"/> に委ねる。
    /// 既定では <see cref="FileDialogueSaveStorage"/>（persistentDataPath）を使う。
    /// </summary>
    public class DialogueSaveSystem : MonoBehaviour
    {
        /// <summary>オートセーブ専用スロット番号。</summary>
        public const int AutosaveSlot = DialogueSaveSlotConventions.AutosaveSlot;
        public const int QuickSaveSlot = DialogueSaveSlotConventions.QuickSaveSlot;

        [Tooltip("未設定なら DialogueManager.Instance を使う。")]
        [SerializeField] private DialogueManager manager;

        [Tooltip("IDialogueSaveContributor を実装したコンポーネント（ステージ/音声の完全復元用）。")]
        [SerializeField] private List<MonoBehaviour> contributors = new List<MonoBehaviour>();

        [Header("Storage")]
        [Tooltip("任意。IDialogueSaveStorageProvider を実装した MonoBehaviour を指定すると保存先を差し替えられる。")]
        [SerializeField] private MonoBehaviour storageProvider;

        [Tooltip("空なら Application.persistentDataPath/dialogue_saves。FileDialogueSaveStorage の保存ルートを差し替える。")]
        [SerializeField] private string fileSaveDirectoryOverride = string.Empty;

        [Header("Versioning")]
        [Tooltip("ゲーム側コンテンツのバージョン。セーブ互換性判定や移行に使う。")]
        [SerializeField] private string contentVersion = string.Empty;

        [Tooltip("demo/full/steam など、ゲーム側のビルドチャンネル。TalkSystem は値の意味を解釈しない。")]
        [SerializeField] private string productChannel = string.Empty;

        [Tooltip("保存中の dialogue ID が現在のリポジトリから消えていた場合の扱い。")]
        [SerializeField] private DialogueMissingDialoguePolicy missingDialoguePolicy = DialogueMissingDialoguePolicy.Fail;

        [Tooltip("Missing Dialogue Policy が UseFallbackDialogueId の場合に復元する行 ID。")]
        [SerializeField] private int fallbackDialogueId = -1;

        private DialogueSaveService _service;
        private IDialogueSaveStorage _storage;
        private IDialogueSaveStorage _configuredStorage;
        private readonly List<IDialogueSaveContributor> _runtimeContributors = new List<IDialogueSaveContributor>();
        private readonly List<IDialogueSaveDataMigration> _dataMigrations = new List<IDialogueSaveDataMigration>();
        private readonly List<IDialogueSaveSlotMigration> _slotMigrations = new List<IDialogueSaveSlotMigration>();
        private DialogueSaveOperationResult _lastOperationResult;

        public DialogueSaveService Service
        {
            get { return _service; }
        }

        public DialogueSaveOperationResult LastOperationResult
        {
            get { return _service != null && _service.LastResult != null ? _service.LastResult : _lastOperationResult; }
        }

        public event Action<DialogueSaveOperationResult> OperationCompleted;
        public event Action<DialogueSaveOperationResult> OperationFailed;

        private void Awake()
        {
            RebuildService();
        }

        public void SetStorage(IDialogueSaveStorage storage)
        {
            _configuredStorage = storage;
            RebuildService();
        }

        public void ConfigureFileStorageRoot(string directory)
        {
            _configuredStorage = null;
            fileSaveDirectoryOverride = directory ?? string.Empty;
            RebuildService();
        }

        public void SetSaveMetadata(string newContentVersion, string newProductChannel)
        {
            contentVersion = newContentVersion ?? string.Empty;
            productChannel = newProductChannel ?? string.Empty;
            ApplyServiceOptions(Manager);
        }

        public void SetMissingDialoguePolicy(DialogueMissingDialoguePolicy policy, int fallbackId = -1)
        {
            missingDialoguePolicy = policy;
            fallbackDialogueId = fallbackId;
            ApplyServiceOptions(Manager);
        }

        public void RegisterDataMigration(IDialogueSaveDataMigration migration)
        {
            if (migration == null || _dataMigrations.Contains(migration)) return;
            _dataMigrations.Add(migration);
            if (_service != null)
                _service.AddDataMigration(migration);
        }

        public void RegisterSlotMigration(IDialogueSaveSlotMigration migration)
        {
            if (migration == null || _slotMigrations.Contains(migration)) return;
            _slotMigrations.Add(migration);
            if (_service != null)
                _service.AddSlotMigration(migration);
        }

        private void RebuildService()
        {
            if (_service != null)
                _service.OperationCompleted -= HandleServiceOperationCompleted;

            _storage = ResolveStorage();
            _service = new DialogueSaveService(_storage, null, BuildOptions());
            _service.OperationCompleted += HandleServiceOperationCompleted;

            foreach (var component in contributors)
            {
                var contributor = component as IDialogueSaveContributor;
                if (contributor != null)
                    _service.AddContributor(contributor);
            }

            for (var i = 0; i < _runtimeContributors.Count; i++)
                _service.AddContributor(_runtimeContributors[i]);

            for (var i = 0; i < _dataMigrations.Count; i++)
                _service.AddDataMigration(_dataMigrations[i]);

            for (var i = 0; i < _slotMigrations.Count; i++)
                _service.AddSlotMigration(_slotMigrations[i]);
        }

        public void RegisterContributor(IDialogueSaveContributor contributor)
        {
            if (contributor != null && !_runtimeContributors.Contains(contributor))
                _runtimeContributors.Add(contributor);

            if (_service != null)
                _service.AddContributor(contributor);
        }

        private DialogueManager Manager
        {
            get { return manager != null ? manager : DialogueManager.Instance; }
        }

        /// <summary>指定スロットへ保存する（サムネイル無し）。</summary>
        public DialogueSaveSlot Save(int slot, bool isAutosave = false, string title = null)
        {
            EnsureService();
            var mgr = Manager;
            if (mgr == null)
            {
                var result = DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Save,
                    slot,
                    "DialogueManager was not found.");
                ReportLocal(result);
                Debug.LogError("[DialogueSaveSystem] DialogueManager が見つかりません。");
                return null;
            }

            ApplyServiceOptions(mgr);
            var data = mgr.CaptureState();
            var resolvedTitle = string.IsNullOrEmpty(title) ? BuildTitle(mgr) : title;
            return _service.Save(slot, data, resolvedTitle, isAutosave, NowUnix());
        }

        /// <summary>画面キャプチャ付きで保存する（フレーム終端まで待つためコルーチン）。</summary>
        public void SaveWithThumbnail(int slot, bool isAutosave = false, string title = null)
        {
            if (Save(slot, isAutosave, title) != null)
                StartCoroutine(CaptureThumbnail(slot));
        }

        public DialogueSaveSlot QuickSave(string title = null)
        {
            return Save(QuickSaveSlot, false, title);
        }

        public void QuickSaveWithThumbnail(string title = null)
        {
            SaveWithThumbnail(QuickSaveSlot, false, title);
        }

        /// <summary>指定スロットを読み込み、会話本体と演出系を復元する。</summary>
        public bool Load(int slot)
        {
            EnsureService();
            var mgr = Manager;
            if (mgr == null)
            {
                ReportLocal(DialogueSaveOperationResult.Failure(
                    DialogueSaveOperation.Load,
                    slot,
                    "DialogueManager was not found."));
                return false;
            }

            ApplyServiceOptions(mgr);
            var saveSlot = _service.Load(slot);
            if (saveSlot == null || saveSlot.Data == null)
                return false;

            if (!mgr.RestoreState(saveSlot.Data))
                return false;

            _service.ApplyRestore(saveSlot.Data);
            return true;
        }

        public bool QuickLoad()
        {
            return Load(QuickSaveSlot);
        }

        public bool Exists(int slot)
        {
            EnsureService();
            return _service != null && _service.Exists(slot);
        }

        public void Delete(int slot)
        {
            EnsureService();
            if (_service != null)
                _service.Delete(slot);
        }

        public List<int> ListSlots()
        {
            EnsureService();
            return _service != null ? _service.ListSlots() : new List<int>();
        }

        public DialogueSaveSlot Peek(int slot)
        {
            EnsureService();
            return _service != null ? _service.Load(slot) : null;
        }

        public DialogueSaveSlotViewModel GetSlotViewModel(int slot, bool includeThumbnail = true)
        {
            EnsureService();
            return _service != null
                ? _service.GetSlotViewModel(slot, includeThumbnail)
                : DialogueSaveSlotViewModel.Empty(slot, "Dialogue save service is not configured.");
        }

        public List<DialogueSaveSlotViewModel> GetSlotViewModels(IEnumerable<int> slots, bool includeThumbnail = true)
        {
            EnsureService();
            return _service != null ? _service.GetSlotViewModels(slots, includeThumbnail) : new List<DialogueSaveSlotViewModel>();
        }

        public List<DialogueSaveSlotViewModel> ListSlotViewModels(bool includeThumbnail = true)
        {
            EnsureService();
            return _service != null ? _service.ListSlotViewModels(includeThumbnail) : new List<DialogueSaveSlotViewModel>();
        }

        public DialogueSaveSlotViewModel GetLatestContinueCandidate(
            bool includeAutosaves = true,
            bool includeQuickSaves = true,
            bool includeThumbnail = true)
        {
            EnsureService();
            return _service != null
                ? _service.GetLatestContinueCandidate(includeAutosaves, includeQuickSaves, includeThumbnail)
                : null;
        }

        /// <summary>スロットのサムネイルを Texture2D として取得する。無ければ null。</summary>
        public Texture2D LoadThumbnail(int slot)
        {
            EnsureService();
            var bytes = _service != null ? _service.LoadThumbnail(slot) : null;
            if (bytes == null || bytes.Length == 0) return null;

            var texture = new Texture2D(2, 2);
            return texture.LoadImage(bytes) ? texture : null;
        }

        private IEnumerator CaptureThumbnail(int slot)
        {
            yield return new WaitForEndOfFrame();

            Texture2D screenshot = null;
            try
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                var png = screenshot.EncodeToPNG();
                if (_service != null)
                    _service.SaveThumbnail(slot, png);
            }
            finally
            {
                if (screenshot != null)
                    Destroy(screenshot);
            }
        }

        private static string BuildTitle(DialogueManager mgr)
        {
            var history = mgr.History;
            if (history != null && history.Count > 0)
            {
                var last = history[history.Count - 1];
                var text = last != null ? last.Text : string.Empty;
                if (!string.IsNullOrEmpty(text))
                    return text.Length > 40 ? text.Substring(0, 40) + "…" : text;
            }

            return "Save";
        }

        private IDialogueSaveStorage ResolveStorage()
        {
            if (_configuredStorage != null)
                return _configuredStorage;

            var provider = storageProvider as IDialogueSaveStorageProvider;
            if (provider != null)
            {
                var provided = provider.CreateStorage();
                if (provided != null)
                    return provided;

                Debug.LogError("[DialogueSaveSystem] storageProvider returned null. Falling back to FileDialogueSaveStorage.");
            }
            else if (storageProvider != null)
            {
                Debug.LogError("[DialogueSaveSystem] storageProvider must implement IDialogueSaveStorageProvider.");
            }

            return new FileDialogueSaveStorage(string.IsNullOrEmpty(fileSaveDirectoryOverride) ? null : fileSaveDirectoryOverride);
        }

        private DialogueSaveServiceOptions BuildOptions()
        {
            return new DialogueSaveServiceOptions
            {
                CurrentSchemaVersion = DialogueSaveSchema.CurrentVersion,
                ContentVersion = contentVersion ?? string.Empty,
                ProductChannel = productChannel ?? string.Empty,
                MissingDialoguePolicy = missingDialoguePolicy,
                FallbackDialogueId = fallbackDialogueId
            };
        }

        private void EnsureService()
        {
            if (_service == null)
                RebuildService();
        }

        private void ApplyServiceOptions(DialogueManager mgr)
        {
            if (_service == null) return;
            _service.Options.CurrentSchemaVersion = DialogueSaveSchema.CurrentVersion;
            _service.Options.ContentVersion = contentVersion ?? string.Empty;
            _service.Options.ProductChannel = productChannel ?? string.Empty;
            _service.Options.MissingDialoguePolicy = missingDialoguePolicy;
            _service.Options.FallbackDialogueId = fallbackDialogueId;
            _service.Options.Repository = mgr != null ? mgr.Repository : null;
        }

        private void HandleServiceOperationCompleted(DialogueSaveOperationResult result)
        {
            ReportLocal(result);
        }

        private void ReportLocal(DialogueSaveOperationResult result)
        {
            _lastOperationResult = result;

            // 失敗はイベント購読の有無に関係なくログへ残す。スタックトレース付きの元例外が
            // あれば LogException で出し、フィールドでの障害調査（どのスロット・どの操作か）を
            // ログだけで追えるようにする。
            if (result != null && result.Failed)
            {
                Debug.LogError(
                    "[DialogueSaveSystem] " + result.Operation + " failed for slot " + result.SlotIndex + ": " + result.Message,
                    this);
                if (result.Exception != null)
                    Debug.LogException(result.Exception, this);
            }

            if (OperationCompleted != null)
                OperationCompleted(result);
            if (result != null && result.Failed && OperationFailed != null)
                OperationFailed(result);
        }

        private static long NowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
