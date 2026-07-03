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
            return DialogueChoice.ParseList(ChoicesRaw);
        }

        public DialogueMediaCue GetBackgroundCue()
        {
            return DialogueMediaCue.Parse(Background);
        }

        public DialogueMediaCue GetBgmCue()
        {
            return DialogueMediaCue.Parse(Bgm);
        }

        /// <summary>Se 列を <c>|</c> 区切りで分解した効果音キー列。空要素は除外する。</summary>
        public IReadOnlyList<string> GetSeKeys()
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(Se)) return result;

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
                RowNumber = RowNumber
            };
        }

        public override string ToString()
        {
            return $"[Dialogue {Id}] {Speaker}: {Text} (Next: {NextId}, Emotion: {EmotionKey}, Trigger: {TriggerKey}, Condition: {ConditionKey}, Event: {EventKey})";
        }
    }
}
