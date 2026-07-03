using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    [Serializable]
    public sealed class DialogueGraphNode
    {
        public int id;
        public string speaker = string.Empty;
        public string text = string.Empty;
        public int nextId = -1;
        public string emotionKey = string.Empty;
        public string triggerKey = string.Empty;
        public string conditionKey = string.Empty;
        public string eventKey = string.Empty;
        public string choicesRaw = string.Empty;
        public float autoNextSeconds = -1f;
        public string background = string.Empty;
        public string bgm = string.Empty;
        public string se = string.Empty;
        public string voice = string.Empty;
        public string charactersRaw = string.Empty;
        public string chapterKey = string.Empty;
        public string routeKey = string.Empty;
        public string endingKey = string.Empty;
        public Rect rect;

        public IReadOnlyList<DialogueChoice> Choices
        {
            get { return DialogueChoice.ParseList(choicesRaw); }
        }
    }

    public sealed class DialogueGraphEdge
    {
        public DialogueGraphEdge(int fromId, int toId, string label, bool isChoice, bool isBroken)
        {
            FromId = fromId;
            ToId = toId;
            Label = label ?? string.Empty;
            IsChoice = isChoice;
            IsBroken = isBroken;
        }

        public int FromId { get; private set; }
        public int ToId { get; private set; }
        public string Label { get; private set; }
        public bool IsChoice { get; private set; }
        public bool IsBroken { get; private set; }
    }

    public sealed class DialogueGraphModel
    {
        private readonly List<DialogueGraphNode> _nodes = new List<DialogueGraphNode>();
        private readonly List<DialogueGraphEdge> _edges = new List<DialogueGraphEdge>();
        private readonly DialogueValidationReport _diagnostics = new DialogueValidationReport();

        public IReadOnlyList<DialogueGraphNode> Nodes { get { return _nodes; } }
        public IReadOnlyList<DialogueGraphEdge> Edges { get { return _edges; } }
        public DialogueValidationReport Diagnostics { get { return _diagnostics; } }

        public DialogueGraphNode Find(int id)
        {
            return _nodes.FirstOrDefault(n => n.id == id);
        }

        public int NextAvailableId()
        {
            return _nodes.Count == 0 ? 1 : _nodes.Max(n => n.id) + 1;
        }

        internal void AddNode(DialogueGraphNode node)
        {
            _nodes.Add(node);
        }

        internal void AddEdge(DialogueGraphEdge edge)
        {
            _edges.Add(edge);
        }

        internal void AddDiagnostic(DialogueValidationMessage message)
        {
            _diagnostics.Add(message.Severity, message.RowNumber, message.FieldName, message.Message);
        }

        internal void ClearEdgesAndDiagnostics()
        {
            _edges.Clear();
            _diagnostics.Clear();
        }
    }

    public static class DialogueGraphMapper
    {
        public static DialogueGraphModel FromCsv(string csvText)
        {
            var model = new DialogueGraphModel();
            var data = CsvLoader.ParseText<DialogueData>(csvText).Values.OrderBy(d => d.Id).ToList();
            var index = 0;

            foreach (var item in data)
            {
                model.AddNode(new DialogueGraphNode
                {
                    id = item.Id,
                    speaker = item.Speaker,
                    text = item.Text,
                    nextId = item.NextId,
                    emotionKey = item.EmotionKey,
                    triggerKey = item.TriggerKey,
                    conditionKey = item.ConditionKey,
                    eventKey = item.EventKey,
                    choicesRaw = item.ChoicesRaw,
                    autoNextSeconds = item.AutoNextSeconds,
                    background = item.Background,
                    bgm = item.Bgm,
                    se = item.Se,
                    voice = item.Voice,
                    charactersRaw = item.CharactersRaw,
                    chapterKey = item.ChapterKey,
                    routeKey = item.RouteKey,
                    endingKey = item.EndingKey,
                    rect = new Rect(40 + (index % 4) * 300, 40 + (index / 4) * 230, 260, 170)
                });
                index++;
            }

            RebuildEdges(model);
            return model;
        }

        public static void RebuildEdges(DialogueGraphModel model)
        {
            var ids = new HashSet<int>(model.Nodes.Select(n => n.id));
            model.ClearEdgesAndDiagnostics();

            foreach (var node in model.Nodes)
            {
                if (node.nextId >= 0)
                    model.AddEdge(new DialogueGraphEdge(node.id, node.nextId, "Next", false, !ids.Contains(node.nextId)));

                foreach (var choice in node.Choices)
                    model.AddEdge(new DialogueGraphEdge(node.id, choice.NextId, choice.Text, true, !ids.Contains(choice.NextId)));
            }

            var csv = ToCsv(model);
            foreach (var message in DialogueValidator.ValidateCsv(csv).Messages)
                model.AddDiagnostic(message);
        }

        public static string ToCsv(DialogueGraphModel model)
        {
            var rows = model.Nodes
                .OrderBy(n => n.id)
                .Select(n => (IReadOnlyList<string>)new[]
                {
                    n.id.ToString(),
                    n.speaker ?? string.Empty,
                    n.text ?? string.Empty,
                    n.nextId >= 0 ? n.nextId.ToString() : "-1",
                    n.emotionKey ?? string.Empty,
                    n.triggerKey ?? string.Empty,
                    n.conditionKey ?? string.Empty,
                    n.eventKey ?? string.Empty,
                    n.choicesRaw ?? string.Empty,
                    n.autoNextSeconds >= 0f ? n.autoNextSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                    n.background ?? string.Empty,
                    n.bgm ?? string.Empty,
                    n.se ?? string.Empty,
                    n.voice ?? string.Empty,
                    n.charactersRaw ?? string.Empty,
                    n.chapterKey ?? string.Empty,
                    n.routeKey ?? string.Empty,
                    n.endingKey ?? string.Empty
                });

            return DialogueCsvCodec.Write(DialogueSchema.FullHeaders, rows);
        }
    }
}
