using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    public sealed class DialogueGraphEditorWindow : EditorWindow
    {
        private const float NodeWidth = 260f;
        private const float NodeHeight = 170f;
        private const string LayoutPrefsPrefix = "kkmia.TalkSystem.GraphLayout.";

        private TextAsset _csvFile;
        private DialogueGraphModel _model = new DialogueGraphModel();
        private DialogueGraphNode _selected;
        private Vector2 _graphScroll;
        private Vector2 _inspectorScroll;
        private string _search = string.Empty;
        private bool _showOnlyProblems;
        private bool _showOnlyBrokenLinks;
        private bool _showOnlyTriggerEntries;
        private string _speakerFilter = string.Empty;
        private float _zoom = 1f;

        [MenuItem("Tools/kkmia/Dialogue Graph Editor")]
        public static void Open()
        {
            GetWindow<DialogueGraphEditorWindow>("Dialogue Graph");
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawGraphPanel();
            DrawInspectorPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _csvFile = (TextAsset)EditorGUILayout.ObjectField(_csvFile, typeof(TextAsset), false, GUILayout.Width(220));

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Load();

            GUI.enabled = _csvFile != null && _model.Nodes.Count > 0;
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Save();
            GUI.enabled = true;

            if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(80)))
                AddNode();

            _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(160));
            if (GUILayout.Button("Find", EditorStyles.toolbarButton, GUILayout.Width(48)))
                FocusFirstSearchResult();

            _showOnlyProblems = GUILayout.Toggle(_showOnlyProblems, "Problems", EditorStyles.toolbarButton, GUILayout.Width(80));
            _showOnlyBrokenLinks = GUILayout.Toggle(_showOnlyBrokenLinks, "Broken", EditorStyles.toolbarButton, GUILayout.Width(68));
            _showOnlyTriggerEntries = GUILayout.Toggle(_showOnlyTriggerEntries, "Triggers", EditorStyles.toolbarButton, GUILayout.Width(72));
            GUILayout.Label("Speaker", GUILayout.Width(52));
            _speakerFilter = GUILayout.TextField(_speakerFilter, EditorStyles.toolbarTextField, GUILayout.Width(110));
            GUILayout.Label("Zoom", GUILayout.Width(36));
            _zoom = GUILayout.HorizontalSlider(_zoom, 0.6f, 1.4f, GUILayout.Width(90));

            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                AutoLayout();
                SaveLayoutPrefs();
            }

            if (GUILayout.Button("Save Layout", EditorStyles.toolbarButton, GUILayout.Width(88)))
                SaveLayoutPrefs();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphPanel()
        {
            var panelRect = GUILayoutUtility.GetRect(position.width * 0.68f, position.height - 24, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(panelRect, GUIContent.none);

            var contentRect = CalculateContentRect();
            _graphScroll = GUI.BeginScrollView(panelRect, _graphScroll, contentRect);

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_zoom, _zoom, 1f));

            DrawEdges();
            BeginWindows();
            foreach (var node in _model.Nodes.Where(IsVisible))
                node.rect = GUI.Window(node.id, node.rect, DrawNodeWindow, BuildNodeTitle(node));
            EndWindows();

            if (Event.current.type == EventType.MouseUp)
                SaveLayoutPrefs();

            GUI.matrix = oldMatrix;
            GUI.EndScrollView();

            DrawMinimap(panelRect, contentRect);
        }

        private void DrawEdges()
        {
            Handles.BeginGUI();
            foreach (var edge in _model.Edges)
            {
                var from = _model.Find(edge.FromId);
                var to = _model.Find(edge.ToId);
                if (from == null) continue;
                if (!IsVisible(from)) continue;
                if (to != null && !IsVisible(to) && !edge.IsBroken) continue;

                var start = new Vector3(from.rect.xMax, from.rect.center.y, 0);
                var end = to != null ? new Vector3(to.rect.xMin, to.rect.center.y, 0) : new Vector3(from.rect.xMax + 120, from.rect.center.y + 60, 0);
                var color = edge.IsBroken ? Color.red : edge.IsChoice ? new Color(0.1f, 0.5f, 1f) : Color.white;
                Handles.DrawBezier(start, end, start + Vector3.right * 70, end + Vector3.left * 70, color, null, edge.IsChoice ? 3f : 2f);

                var labelPos = Vector3.Lerp(start, end, 0.5f);
                GUI.color = color;
                GUI.Label(new Rect(labelPos.x - 45, labelPos.y - 14, 120, 20), edge.Label);
                GUI.color = Color.white;
            }
            Handles.EndGUI();
        }

        private void DrawNodeWindow(int id)
        {
            var node = _model.Find(id);
            if (node == null) return;

            if (GUILayout.Button("Select"))
                _selected = node;

            EditorGUILayout.LabelField("Speaker", Trim(node.speaker), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Text", Trim(node.text), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Next", node.nextId.ToString(), EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(node.choicesRaw))
                EditorGUILayout.LabelField("Choices", Trim(node.choicesRaw), EditorStyles.wordWrappedMiniLabel);

            GUI.DragWindow();
        }

        private void DrawInspectorPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(360), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            DrawDiagnostics();

            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a node to edit it.", MessageType.Info);
            }
            else
            {
                DrawSelectedNode();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDiagnostics()
        {
            var errors = _model.Diagnostics.Messages.Count(m => m.Severity == DialogueValidationSeverity.Error);
            var warnings = _model.Diagnostics.Messages.Count(m => m.Severity == DialogueValidationSeverity.Warning);
            EditorGUILayout.HelpBox("Errors: " + errors + "  Warnings: " + warnings, errors > 0 ? MessageType.Error : MessageType.Info);
        }

        private void DrawSelectedNode()
        {
            EditorGUI.BeginChangeCheck();
            _selected.id = EditorGUILayout.IntField("Id", _selected.id);
            _selected.speaker = EditorGUILayout.TextField("Speaker", _selected.speaker);
            EditorGUILayout.LabelField("Text");
            _selected.text = EditorGUILayout.TextArea(_selected.text, GUILayout.MinHeight(72));
            _selected.nextId = EditorGUILayout.IntField("NextId", _selected.nextId);
            _selected.emotionKey = EditorGUILayout.TextField("EmotionKey", _selected.emotionKey);
            _selected.triggerKey = EditorGUILayout.TextField("TriggerKey", _selected.triggerKey);
            _selected.conditionKey = EditorGUILayout.TextField("ConditionKey", _selected.conditionKey);
            _selected.eventKey = EditorGUILayout.TextField("EventKey", _selected.eventKey);
            _selected.choicesRaw = EditorGUILayout.TextField("Choices", _selected.choicesRaw);
            _selected.autoNextSeconds = EditorGUILayout.FloatField("AutoNextSeconds", _selected.autoNextSeconds);

            if (EditorGUI.EndChangeCheck())
            {
                DialogueGraphMapper.RebuildEdges(_model);
                SaveLayoutPrefs();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Node"))
            {
                var nodes = (System.Collections.Generic.List<DialogueGraphNode>)_model.Nodes;
                nodes.Remove(_selected);
                _selected = null;
                DialogueGraphMapper.RebuildEdges(_model);
            }
        }

        private void Load()
        {
            if (_csvFile == null) return;
            _model = DialogueGraphMapper.FromCsv(_csvFile.text);
            RestoreLayoutPrefs();
            _selected = _model.Nodes.FirstOrDefault();
            FocusSelectedNode();
        }

        private void Save()
        {
            if (_csvFile == null) return;
            var path = AssetDatabase.GetAssetPath(_csvFile);
            File.WriteAllText(path, DialogueGraphMapper.ToCsv(_model), System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private void AddNode()
        {
            var node = new DialogueGraphNode
            {
                id = _model.NextAvailableId(),
                speaker = "Speaker",
                text = "New dialogue line",
                nextId = -1,
                rect = new Rect(_graphScroll.x / _zoom + 80, _graphScroll.y / _zoom + 80, NodeWidth, NodeHeight)
            };

            ((System.Collections.Generic.List<DialogueGraphNode>)_model.Nodes).Add(node);
            _selected = node;
            DialogueGraphMapper.RebuildEdges(_model);
            SaveLayoutPrefs();
        }

        private bool IsVisible(DialogueGraphNode node)
        {
            if (_showOnlyProblems)
            {
                if (!NodeHasProblem(node)) return false;
            }

            if (_showOnlyBrokenLinks && !_model.Edges.Any(e => e.IsBroken && e.FromId == node.id))
                return false;

            if (_showOnlyTriggerEntries && string.IsNullOrEmpty(node.triggerKey))
                return false;

            if (!string.IsNullOrWhiteSpace(_speakerFilter))
            {
                var speaker = node.speaker ?? string.Empty;
                if (speaker.IndexOf(_speakerFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return MatchesSearch(node, _search);
        }

        private bool MatchesSearch(DialogueGraphNode node, string search)
        {
            if (node == null || string.IsNullOrWhiteSpace(search)) return true;

            var haystack = node.id + " " + node.speaker + " " + node.text + " " + node.triggerKey + " " + node.conditionKey + " " + node.eventKey + " " + node.choicesRaw;
            return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool NodeHasProblem(DialogueGraphNode node)
        {
            return _model.Edges.Any(e => e.IsBroken && e.FromId == node.id) ||
                   _model.Diagnostics.Messages.Any(m => m.Message.IndexOf(node.id.ToString(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void FocusFirstSearchResult()
        {
            var node = _model.Nodes.FirstOrDefault(IsVisible);
            if (node == null) return;

            _selected = node;
            FocusSelectedNode();
        }

        private void FocusSelectedNode()
        {
            if (_selected == null) return;

            _graphScroll = new Vector2(
                Mathf.Max(0f, _selected.rect.x * _zoom - position.width * 0.25f),
                Mathf.Max(0f, _selected.rect.y * _zoom - position.height * 0.35f));
        }

        private Rect CalculateContentRect()
        {
            if (_model.Nodes.Count == 0)
                return new Rect(0, 0, 1400, 1400);

            var maxX = _model.Nodes.Max(n => n.rect.xMax) * _zoom + 400;
            var maxY = _model.Nodes.Max(n => n.rect.yMax) * _zoom + 400;
            return new Rect(0, 0, Mathf.Max(1400, maxX), Mathf.Max(1400, maxY));
        }

        private void AutoLayout()
        {
            if (_model.Nodes.Count == 0) return;

            var ordered = _model.Nodes
                .OrderByDescending(n => !string.IsNullOrEmpty(n.triggerKey))
                .ThenBy(n => n.id)
                .ToList();

            var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(ordered.Count)));
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].rect = new Rect(40 + (i % columns) * 310, 40 + (i / columns) * 230, NodeWidth, NodeHeight);
            }
        }

        private void DrawMinimap(Rect panelRect, Rect contentRect)
        {
            if (_model.Nodes.Count == 0) return;

            var minimap = new Rect(panelRect.xMax - 184, panelRect.y + 12, 168, 128);
            GUI.Box(minimap, "Overview");

            var graphBounds = CalculateNodeBounds();
            if (graphBounds.width <= 0 || graphBounds.height <= 0) return;

            var scaleX = (minimap.width - 18) / graphBounds.width;
            var scaleY = (minimap.height - 28) / graphBounds.height;
            var scale = Mathf.Max(0.02f, Mathf.Min(scaleX, scaleY));

            foreach (var node in _model.Nodes)
            {
                var x = minimap.x + 9 + (node.rect.x - graphBounds.x) * scale;
                var y = minimap.y + 20 + (node.rect.y - graphBounds.y) * scale;
                var nodeRect = new Rect(x, y, Mathf.Max(3, node.rect.width * scale), Mathf.Max(3, node.rect.height * scale));
                GUI.color = node == _selected ? new Color(0.2f, 0.8f, 1f) : NodeHasProblem(node) ? Color.red : Color.white;
                GUI.Box(nodeRect, GUIContent.none);
            }

            GUI.color = new Color(0.4f, 1f, 0.4f);
            var viewRect = new Rect(
                minimap.x + 9 + (_graphScroll.x / _zoom - graphBounds.x) * scale,
                minimap.y + 20 + (_graphScroll.y / _zoom - graphBounds.y) * scale,
                (panelRect.width / _zoom) * scale,
                (panelRect.height / _zoom) * scale);
            GUI.Box(viewRect, GUIContent.none);
            GUI.color = Color.white;
        }

        private Rect CalculateNodeBounds()
        {
            if (_model.Nodes.Count == 0)
                return new Rect(0, 0, 1, 1);

            var minX = _model.Nodes.Min(n => n.rect.x);
            var minY = _model.Nodes.Min(n => n.rect.y);
            var maxX = _model.Nodes.Max(n => n.rect.xMax);
            var maxY = _model.Nodes.Max(n => n.rect.yMax);
            return new Rect(minX, minY, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY));
        }

        private string LayoutPrefsKey
        {
            get
            {
                if (_csvFile == null) return string.Empty;
                return LayoutPrefsPrefix + AssetDatabase.GetAssetPath(_csvFile);
            }
        }

        private void SaveLayoutPrefs()
        {
            var key = LayoutPrefsKey;
            if (string.IsNullOrEmpty(key) || _model.Nodes.Count == 0) return;

            var snapshot = new LayoutSnapshot();
            foreach (var node in _model.Nodes)
                snapshot.nodes.Add(new LayoutNode { id = node.id, x = node.rect.x, y = node.rect.y });

            EditorPrefs.SetString(key, JsonUtility.ToJson(snapshot));
        }

        private void RestoreLayoutPrefs()
        {
            var key = LayoutPrefsKey;
            if (string.IsNullOrEmpty(key) || !EditorPrefs.HasKey(key)) return;

            var snapshot = JsonUtility.FromJson<LayoutSnapshot>(EditorPrefs.GetString(key));
            if (snapshot == null || snapshot.nodes == null) return;

            foreach (var saved in snapshot.nodes)
            {
                var node = _model.Find(saved.id);
                if (node != null)
                    node.rect = new Rect(saved.x, saved.y, NodeWidth, NodeHeight);
            }
        }

        private string BuildNodeTitle(DialogueGraphNode node)
        {
            var marker = node == _selected ? "* " : string.Empty;
            return marker + "#" + node.id + " " + node.speaker;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 48 ? value : value.Substring(0, 45) + "...";
        }

        [Serializable]
        private sealed class LayoutSnapshot
        {
            public List<LayoutNode> nodes = new List<LayoutNode>();
        }

        [Serializable]
        private sealed class LayoutNode
        {
            public int id;
            public float x;
            public float y;
        }
    }
}
