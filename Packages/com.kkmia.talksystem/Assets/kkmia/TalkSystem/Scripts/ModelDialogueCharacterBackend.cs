using System.Collections.Generic;
using System;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>立ち絵スロット名と配置先 Transform の対応。</summary>
    [System.Serializable]
    public sealed class DialogueModelSlot
    {
        public string slot = DialogueStageSlot.Center;
        public Transform anchor;
    }

    /// <summary>
    /// <see cref="DialogueCharacterModel"/>（Live2D/Spine/プレハブ）を用いる立ち絵バックエンド。
    /// キーでモデルを引き、スロットの Transform 下へ配置して表示/退場/表情/アニメを反映する。
    /// SDK 非依存。<see cref="DialogueStageView"/> の characterBackend に割り当てて使う。
    /// </summary>
    public class ModelDialogueCharacterBackend : MonoBehaviour, IDialogueCharacterBackend, IDialoguePresentationIssueSource
    {
        [Tooltip("利用するキャラクターモデル。CharacterKey で参照される。")]
        [SerializeField] private List<DialogueCharacterModel> models = new List<DialogueCharacterModel>();

        [Tooltip("各スロットの配置先。anchor が設定されていればモデルをその子へ移動する。")]
        [SerializeField] private List<DialogueModelSlot> slots = new List<DialogueModelSlot>();

        [Tooltip("起動時に全モデルを非表示にする。")]
        [SerializeField] private bool hideAllOnAwake = true;

        // 表示中の slot -> model。退場・全消去で参照する。
        private readonly Dictionary<string, DialogueCharacterModel> _shown = new Dictionary<string, DialogueCharacterModel>();

        public event Action<DialoguePresentationIssueContext> PresentationIssueRaised;

        protected virtual void Awake()
        {
            if (!hideAllOnAwake) return;

            for (var i = 0; i < models.Count; i++)
            {
                if (models[i] != null)
                    models[i].gameObject.SetActive(false);
            }
        }

        public void RegisterModel(DialogueCharacterModel model)
        {
            if (model != null && !models.Contains(model))
                models.Add(model);
        }

        public bool TryGetModel(string characterKey, out DialogueCharacterModel model)
        {
            model = FindModel(characterKey);
            return model != null;
        }

        public void SetCharacter(string slot, string characterKey, string expression, string animation)
        {
            var model = FindModel(characterKey);
            if (model == null)
            {
                RaiseIssue(DialoguePresentationIssueKind.CharacterModel, characterKey,
                    "Character model \"" + characterKey + "\" could not be resolved.");
                Debug.LogWarning("[ModelDialogueCharacterBackend] モデル \"" + characterKey + "\" が見つかりません。");
                return;
            }

            var key = NormalizeSlot(slot);
            PlaceInSlot(model, key);

            // 同じモデルが別スロットに表示されていたら、そのスロット記録を消す。
            RemoveShownByModel(model, key);
            _shown[key] = model;

            model.Show(expression, animation);
        }

        public void RemoveCharacter(string slot, string characterKey, string animation)
        {
            var key = NormalizeSlot(slot);

            DialogueCharacterModel model;
            if (!_shown.TryGetValue(key, out model) || model == null)
            {
                // スロット記録が無ければキーから直接探す。
                model = FindModel(characterKey);
                if (model == null) return;
            }

            _shown.Remove(key);
            model.Hide(animation);
        }

        public void ClearCharacters()
        {
            foreach (var pair in _shown)
            {
                if (pair.Value != null)
                    pair.Value.Hide(null);
            }

            _shown.Clear();
        }

        private void PlaceInSlot(DialogueCharacterModel model, string slot)
        {
            var anchor = FindAnchor(slot);
            if (anchor != null && model.transform.parent != anchor)
            {
                model.transform.SetParent(anchor, false);
                model.transform.localPosition = Vector3.zero;
            }
        }

        private void RemoveShownByModel(DialogueCharacterModel model, string keepSlot)
        {
            string found = null;
            foreach (var pair in _shown)
            {
                if (pair.Value == model && pair.Key != keepSlot)
                {
                    found = pair.Key;
                    break;
                }
            }

            if (found != null)
                _shown.Remove(found);
        }

        private DialogueCharacterModel FindModel(string characterKey)
        {
            if (string.IsNullOrEmpty(characterKey)) return null;

            for (var i = 0; i < models.Count; i++)
            {
                if (models[i] != null && models[i].CharacterKey == characterKey)
                    return models[i];
            }

            return null;
        }

        private Transform FindAnchor(string slot)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].slot == slot)
                    return slots[i].anchor;
            }

            return null;
        }

        private static string NormalizeSlot(string slot)
        {
            return string.IsNullOrEmpty(slot) ? DialogueStageSlot.Center : slot;
        }

        private void RaiseIssue(DialoguePresentationIssueKind kind, string key, string message)
        {
            if (PresentationIssueRaised != null)
                PresentationIssueRaised(new DialoguePresentationIssueContext(kind, key, message));
        }
    }
}
