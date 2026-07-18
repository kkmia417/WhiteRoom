using System;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [Serializable]
    public class DialogueData
    {
        [field: SerializeField] public int Id { get; internal set; }
        [field: SerializeField] public string Speaker { get; internal set; }
        [field: SerializeField] public string Text { get; internal set; }
        [field: SerializeField] public int NextId { get; internal set; } = -1;
        [field: SerializeField] public string EmotionKey { get; internal set; }
        [field: SerializeField] public string TriggerKey { get; internal set; }
        [field: SerializeField] public string ConditionKey { get; internal set; }
        [field: SerializeField] public string EventKey { get; internal set; }
        [field: SerializeField] public string ChoicesRaw { get; internal set; }
        [field: SerializeField] public float AutoNextSeconds { get; internal set; } = -1f;

        // 演出（ステージ）列。すべて任意。詳細な解釈は演出層に委ね、ここでは生値と解析アクセサのみを提供する。
        [field: SerializeField] public string Background { get; internal set; }
        [field: SerializeField] public string Bgm { get; internal set; }
        [field: SerializeField] public string Se { get; internal set; }
        [field: SerializeField] public string Voice { get; internal set; }
        [field: SerializeField] public string CharactersRaw { get; internal set; }
        [field: SerializeField] public string ChapterKey { get; internal set; }
        [field: SerializeField] public string RouteKey { get; internal set; }
        [field: SerializeField] public string EndingKey { get; internal set; }

        public int RowNumber { get; internal set; }

        // TalkSystem が解釈しない CSV カラム（ヘッダー名 → セル値）。ロード時に確定し、以後は
        // 読み取り専用。JsonUtility では直列化されない（履歴はフィールド展開型で保存されるため
        // セーブ互換に影響しない）。
        [NonSerialized] private Dictionary<string, string> _extraColumns;
        private static readonly IReadOnlyDictionary<string, string> EmptyExtraColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ChoicesRaw はロード/構築時に確定する前提で、パース結果をキャッシュして
        // 行送り・選択のたびの再パースを避ける。
        [NonSerialized] private IReadOnlyList<DialogueChoice> _parsedChoices;

        /// <summary>
        /// 既知スキーマ外のカラム値。ゲーム固有のメタデータ（カメラ指示・演出タグなど）を
        /// CSV に足しても TalkSystem 側の対応を待たずに読み出せる。ヘッダー名は大文字小文字を区別しない。
        /// </summary>
        public IReadOnlyDictionary<string, string> ExtraColumns
        {
            get { return _extraColumns != null ? _extraColumns : EmptyExtraColumns; }
        }

        public bool HasExtraColumns
        {
            get { return _extraColumns != null && _extraColumns.Count > 0; }
        }

        /// <summary>拡張カラムの値を取得する。存在しない場合は false。</summary>
        public bool TryGetExtra(string column, out string value)
        {
            if (_extraColumns != null && !string.IsNullOrEmpty(column))
                return _extraColumns.TryGetValue(column, out value);

            value = null;
            return false;
        }

        internal void SetExtraColumns(Dictionary<string, string> columns)
        {
            _extraColumns = columns;
        }

        public bool HasTriggerKey
        {
            get { return !string.IsNullOrEmpty(TriggerKey); }
        }

        public bool HasConditionKey
        {
            get { return !string.IsNullOrEmpty(ConditionKey); }
        }

        public bool HasEventKey
        {
            get { return !string.IsNullOrEmpty(EventKey); }
        }

        public bool HasBackground
        {
            get { return !string.IsNullOrEmpty(Background); }
        }

        public bool HasBgm
        {
            get { return !string.IsNullOrEmpty(Bgm); }
        }

        public bool HasSe
        {
            get { return !string.IsNullOrEmpty(Se); }
        }

        public bool HasVoice
        {
            get { return !string.IsNullOrEmpty(Voice); }
        }

        public bool HasCharacters
        {
            get { return !string.IsNullOrEmpty(CharactersRaw); }
        }

        public bool HasChapterKey
        {
            get { return !string.IsNullOrEmpty(ChapterKey); }
        }

        public bool HasRouteKey
        {
            get { return !string.IsNullOrEmpty(RouteKey); }
        }

        public bool HasEndingKey
        {
            get { return !string.IsNullOrEmpty(EndingKey); }
        }

        public IReadOnlyList<DialogueChoice> GetChoices()
        {
            if (_parsedChoices == null)
                _parsedChoices = DialogueChoice.ParseList(ChoicesRaw);
            return _parsedChoices;
        }

        public DialogueMediaCue GetBackgroundCue()
        {
            return DialogueMediaCue.Parse(Background);
        }

        public DialogueMediaCue GetBgmCue()
        {
            return DialogueMediaCue.Parse(Bgm);
        }

        private static readonly IReadOnlyList<string> EmptySeKeys = new string[0];

        /// <summary>Se 列を <c>|</c> 区切りで分解した効果音キー列。空要素は除外する。</summary>
        public IReadOnlyList<string> GetSeKeys()
        {
            if (string.IsNullOrWhiteSpace(Se)) return EmptySeKeys;

            var result = new List<string>();

            foreach (var entry in Se.Split('|'))
            {
                var key = entry.Trim();
                if (key.Length > 0)
                    result.Add(key);
            }

            return result;
        }

        public IReadOnlyList<DialogueStageDirective> GetStageDirectives()
        {
            return DialogueStageDirective.ParseList(CharactersRaw);
        }

        public DialogueData WithResolvedText(string resolvedText)
        {
            return new DialogueData
            {
                Id = Id,
                Speaker = Speaker,
                Text = resolvedText,
                NextId = NextId,
                EmotionKey = EmotionKey,
                TriggerKey = TriggerKey,
                ConditionKey = ConditionKey,
                EventKey = EventKey,
                ChoicesRaw = ChoicesRaw,
                AutoNextSeconds = AutoNextSeconds,
                Background = Background,
                Bgm = Bgm,
                Se = Se,
                Voice = Voice,
                CharactersRaw = CharactersRaw,
                ChapterKey = ChapterKey,
                RouteKey = RouteKey,
                EndingKey = EndingKey,
                RowNumber = RowNumber,
                // 拡張カラムはロード後は読み取り専用なので、コピーではなく参照を共有する。
                _extraColumns = _extraColumns
            };
        }

        public override string ToString()
        {
            return $"[Dialogue {Id}] {Speaker}: {Text} (Next: {NextId}, Emotion: {EmotionKey}, Trigger: {TriggerKey}, Condition: {ConditionKey}, Event: {EventKey})";
        }
    }
}
