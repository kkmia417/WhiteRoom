using System.Collections.Generic;
using UnityEngine;
using TMPro;
using kkmia.TalkSystem;

public sealed class FeatureTourSampleController : MonoBehaviour, IDialogueVariableResolver, IDialogueConditionEvaluator
{
    [SerializeField] private string playerName = "Player";
    [SerializeField] private bool canTakeLeftRoute = true;
    [SerializeField] private TMP_Text eventLogText;
    [SerializeField] private TMP_Text backlogText;
    [SerializeField] private GameObject questFlagObject;
    [SerializeField] private GameObject timelineMarkerObject;
    [SerializeField] private Animator sampleAnimator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip routeSelectedClip;

    private readonly List<string> _events = new List<string>();
    private DialogueSaveData _savedState;
    private bool _questFlag;
    private bool _japaneseActive;

    private void Start()
    {
        if (DialogueManager.Instance == null) return;

        DialogueManager.Instance.SetVariableResolver(this);
        DialogueManager.Instance.SetConditionEvaluator(this);
        DialogueManager.Instance.SetTextResolver(new LocalizedDialogueTextResolver(BuildTranslations()));
        DialogueManager.Instance.SetLanguage(string.Empty); // 既定は CSV（英語）。L キーで ja に切替。
        DialogueManager.Instance.DialogueEventTriggered += OnDialogueEvent;
        DialogueManager.Instance.LineCompleted += OnLineCompleted;
        DialogueManager.Instance.StartDialogueForState("SampleStart");
        RefreshSampleState();
    }

    private void Update()
    {
        if (DialogueManager.Instance == null) return;

        // サンプル操作（実プロジェクトでは DialogueInputRouter / UI ボタンに割り当て推奨）。
        if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.PageUp))
            Rollback();
        if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.L))
            ToggleLanguage();
        if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.F5))
            SaveDialogue();
        if (DialogueKeyboard.GetKeyDown(DialogueKeyCode.F9))
            RestoreDialogue();
    }

    // 多言語デモ用の翻訳テーブル（ja のみ。未登録/英語時は CSV の本文にフォールバック）。
    private static DialogueTranslationTable BuildTranslations()
    {
        var table = new DialogueTranslationTable();
        table.Add(1, "ja", "ようこそ、{playerName}。このサンプルは[color=#ffcc00]Talk System[/color]の機能を紹介します。");
        table.Add(2, "ja", "ルートを選んでください。[w=0.3] [ruby=ひだり]左[/ruby]ルートには条件が必要です。");
        table.Add(10, "ja", "条件を満たしたので[color=#88ccff]左[/color]ルートが開きました。");
        table.Add(20, "ja", "右ルートは常に選べます。");
        table.Add(30, "ja", "この行はセーブ・復元・巻き戻し・バックログ表示ができます。[w=0.2] L キーで言語を切り替えられます。");
        return table;
    }

    private void OnDestroy()
    {
        if (DialogueManager.Instance == null) return;

        DialogueManager.Instance.DialogueEventTriggered -= OnDialogueEvent;
        DialogueManager.Instance.LineCompleted -= OnLineCompleted;
    }

    public bool TryResolve(string variableName, DialogueData data, out string value)
    {
        if (variableName == "playerName")
        {
            value = playerName;
            return true;
        }

        value = null;
        return false;
    }

    public bool Evaluate(string conditionKey, DialogueData data)
    {
        if (conditionKey == "can_take_left")
            return canTakeLeftRoute;

        return true;
    }

    public void SaveDialogue()
    {
        if (DialogueManager.Instance != null)
        {
            _savedState = DialogueManager.Instance.CaptureState();
            AddEventLog("saved_state");
        }
    }

    public void RestoreDialogue()
    {
        if (DialogueManager.Instance != null && _savedState != null)
        {
            DialogueManager.Instance.RestoreState(_savedState);
            AddEventLog("restored_state");
            RefreshBacklog();
        }
    }

    public void ToggleLeftRoute()
    {
        canTakeLeftRoute = !canTakeLeftRoute;
        AddEventLog(canTakeLeftRoute ? "left_route_enabled" : "left_route_disabled");
    }

    public void Rollback()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.Rollback())
        {
            AddEventLog("rollback");
            RefreshBacklog();
        }
    }

    public void ToggleLanguage()
    {
        _japaneseActive = !_japaneseActive;
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.SetLanguage(_japaneseActive ? "ja" : string.Empty);
        AddEventLog(_japaneseActive ? "language_ja" : "language_default");
    }

    private void OnDialogueEvent(DialogueEventContext context)
    {
        if (context != null && !string.IsNullOrEmpty(context.EventKey))
        {
            ApplySampleEvent(context.EventKey);
            AddEventLog(context.EventKey);
        }
    }

    private void OnLineCompleted(DialogueEventContext context)
    {
        RefreshBacklog();
    }

    private void ApplySampleEvent(string eventKey)
    {
        if (eventKey == "sample_started")
        {
            _questFlag = false;
            if (timelineMarkerObject != null)
                timelineMarkerObject.SetActive(false);
        }
        else if (eventKey == "left_route" || eventKey == "right_route")
        {
            _questFlag = true;
            if (audioSource != null && routeSelectedClip != null)
                audioSource.PlayOneShot(routeSelectedClip);
        }
        else if (eventKey == "sample_finished")
        {
            if (timelineMarkerObject != null)
                timelineMarkerObject.SetActive(true);

            if (sampleAnimator != null)
                sampleAnimator.SetTrigger("DialogueFinished");
        }

        RefreshSampleState();
    }

    private void RefreshSampleState()
    {
        if (questFlagObject != null)
            questFlagObject.SetActive(_questFlag);
    }

    private void AddEventLog(string eventKey)
    {
        _events.Add(eventKey);

        if (eventLogText == null) return;
        eventLogText.text = string.Join("\n", _events.ToArray());
    }

    private void RefreshBacklog()
    {
        if (backlogText == null || DialogueManager.Instance == null) return;

        // 新しい DialogueBacklog モデルで履歴を表示モデルへ変換する。
        var entries = DialogueBacklog.Build(DialogueManager.Instance.History);
        var lines = new List<string>();
        foreach (var entry in entries)
            lines.Add(entry.Speaker + ": " + entry.Text);

        backlogText.text = string.Join("\n", lines.ToArray());
    }
}
