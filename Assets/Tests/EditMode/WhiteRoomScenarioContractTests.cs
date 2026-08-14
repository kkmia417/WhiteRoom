using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WhiteRoom.Novel.EditModeTests
{
    public sealed class WhiteRoomScenarioContractTests
    {
        private const string ScenarioPath = "Assets/Resources/Dialogue/r00_escape_talksystem.csv";
        private const string RouteMatrixPath = "Assets/Tests/Fixtures/r00_ending_routes.json";
        private const int PublishedRowCount = 10648;
        private const int MaximumTurnCharacters = 40;

        [Serializable]
        private sealed class RouteMatrixDocument
        {
            public int startId;
            public EndingRoute[] routes;
        }

        [Serializable]
        private sealed class EndingRoute
        {
            public string endingKey;
            public int[] choiceTargets;
        }

        [Test]
        public void ScenarioStructureMatchesThePublishedBaseline()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            Assert.That(csv, Is.Not.Null, ScenarioPath);
            var repository = new DialogueRepository(csv);
            var rows = repository.GetAll().ToArray();
            var physicalRows = csv.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length - 1;

            Assert.That(physicalRows, Is.EqualTo(PublishedRowCount), "The full manuscript CSV row baseline changed.");
            Assert.That(rows.Length, Is.EqualTo(physicalRows), "Duplicate IDs can be hidden by dictionary parsing.");
            Assert.That(rows.Select(row => row.Id).Distinct().Count(), Is.EqualTo(rows.Length));
            Assert.That(rows.All(row => (row.Text ?? string.Empty).Length <= MaximumTurnCharacters), Is.True,
                $"Every dialogue turn must fit within {MaximumTurnCharacters} characters.");
            Assert.That(repository.ValidationReport.HasErrors, Is.False,
                string.Join("\n", repository.ValidationReport.Messages.Select(message => message.ToString())));

            var byId = rows.ToDictionary(row => row.Id);
            var choiceRows = rows.Where(row => row.GetChoices().Count > 0).ToArray();
            Assert.That(choiceRows.Length, Is.EqualTo(2));
            Assert.That(rows.Count(row => !string.IsNullOrWhiteSpace(row.EndingKey)), Is.EqualTo(4));
            Assert.That(rows.Select(row => row.EndingKey).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(rows.Count(row => !string.IsNullOrWhiteSpace(row.ChapterKey)), Is.EqualTo(14));
            Assert.That(rows.All(row => string.IsNullOrWhiteSpace(row.ConditionKey)), Is.True,
                "The chapter 1-14 adaptation intentionally has no state-gated branches.");
            foreach (var routeKey in new[] { "managed_future", "single_answer" })
            {
                var branchStart = rows.Single(row => row.RouteKey == routeKey);
                Assert.That(CountRowsThroughEnding(repository, branchStart.Id), Is.GreaterThanOrEqualTo(5),
                    routeKey + " must include an authored aftermath before the ending.");
            }

            foreach (var row in rows)
            {
                if (row.NextId >= 0)
                    Assert.That(byId.ContainsKey(row.NextId), Is.True, $"Row {row.Id} NextId {row.NextId}");
                foreach (var choice in row.GetChoices())
                    Assert.That(byId.ContainsKey(choice.NextId), Is.True, $"Row {row.Id} choice target {choice.NextId}");
            }
        }

        [Test]
        public void ChapterAndEndingBoundariesResetPortraitState()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            Assert.That(csv, Is.Not.Null, ScenarioPath);
            var rows = new DialogueRepository(csv).GetAll().ToArray();
            var chapterRows = rows.Where(row => !string.IsNullOrWhiteSpace(row.ChapterKey)).ToArray();
            var endingRows = rows.Where(row => !string.IsNullOrWhiteSpace(row.EndingKey)).ToArray();

            Assert.That(chapterRows, Has.Length.EqualTo(14));
            foreach (var row in chapterRows)
            {
                var directives = row.GetStageDirectives();
                Assert.That(directives, Is.Not.Empty, $"Chapter row {row.Id} must define the stage.");
                Assert.That(directives[0].IsClearAll, Is.True,
                    $"Chapter row {row.Id} must clear inherited portraits first.");
            }

            foreach (var row in endingRows)
                Assert.That(row.GetStageDirectives().Any(directive => directive.IsClearAll), Is.True,
                    $"Ending row {row.Id} must clear all portraits.");

            var reiOnlyBoundaries = new[]
            {
                rows.Single(row => (row.Background ?? string.Empty).StartsWith("lab_room_white#cut")),
                rows.Single(row => row.ChapterKey == "chapter_05"),
                rows.Single(row => row.ChapterKey == "chapter_06"),
                rows.Single(row => row.ChapterKey == "chapter_10")
            };
            foreach (var reiOnlyBoundary in reiOnlyBoundaries)
            {
                var state = new DialogueStageState();
                state.Apply(DialogueStageDirective.ParseList("Rei@left|Nagi@right"));
                state.Apply(reiOnlyBoundary.GetStageDirectives());
                Assert.That(state.Occupancy.Values, Does.Not.Contain("Nagi"),
                    $"Row {reiOnlyBoundary.Id} must not inherit Nagi from the previous scene.");
            }
        }

        [Test]
        public void ReviewedOpeningDialogueKeepsSpeakerAndTextTogether()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var rows = new DialogueRepository(csv).GetAll().ToDictionary(row => row.Id);
            var expected = new Dictionary<int, (string Speaker, string Text)>
            {
                { 1000024, ("少女", "即答なんだ") },
                { 1000095, ("ユイ", "分かってる") },
                { 1000097, ("職員", "N-17") },
                { 1000102, ("ユイ", "元気ですけど") },
                { 1000103, ("職員", "自覚症状と数値は一致しない場合があります。本日から補助剤を追加します") },
                { 1000123, ("システム音声", "結果を表示します") },
                { 1000130, ("ユイ", "目の前で死ぬ人がいるからです") },
                { 1000192, ("アサヒ", "それ、本当にレイが思ってる？") },
                { 1000264, ("レイ", "何をしているのですか") },
                { 1000265, ("ユイ", "待ってた") },
                { 1000373, ("レイ", "ここは評価室です") },
                { 1000374, ("少女", "見れば分かる") },
                { 1000653, ("ナギ", "じゃあ、レイ自身は？　本当は知りたい？") },
                { 1000680, ("ナギ", "正面出口は無理") },
                { 1000681, ("レイ", "別の出口は") },
            };

            foreach (var pair in expected)
            {
                Assert.That(rows.ContainsKey(pair.Key), Is.True, pair.Key.ToString());
                Assert.That(rows[pair.Key].Speaker, Is.EqualTo(pair.Value.Speaker), pair.Key.ToString());
                Assert.That(rows[pair.Key].Text, Is.EqualTo(pair.Value.Text), pair.Key.ToString());
            }
        }

        [Test]
        public void EveryPublishedEndingRouteResolvesDeterministically()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var matrixAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(RouteMatrixPath);
            Assert.That(csv, Is.Not.Null, ScenarioPath);
            Assert.That(matrixAsset, Is.Not.Null, RouteMatrixPath);
            var repository = new DialogueRepository(csv);
            var matrix = JsonUtility.FromJson<RouteMatrixDocument>(matrixAsset.text);
            Assert.That(matrix, Is.Not.Null);
            Assert.That(matrix.routes, Is.Not.Null);
            Assert.That(matrix.routes.Length, Is.EqualTo(4));
            Assert.That(matrix.routes.Select(route => route.endingKey).Distinct().Count(), Is.EqualTo(4));

            var scenarioEndingKeys = repository.GetAll()
                .Select(row => row.EndingKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct()
                .OrderBy(key => key)
                .ToArray();
            CollectionAssert.AreEquivalent(scenarioEndingKeys, matrix.routes.Select(route => route.endingKey));

            foreach (var route in matrix.routes)
                AssertRoute(repository, matrix.startId, route);
        }

        [Test]
        public void ProductResolversProgressConditionsAndFallbackSaveTitlesRemainStable()
        {
            var row = new DialogueRepository(AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath))
                .GetAll()
                .OrderBy(item => item.RowNumber)
                .First();
            var resolverType = RequireProductType("WhiteRoom.Novel.PlayerNameVariableResolver");
            var resolver = (IDialogueVariableResolver)Activator.CreateInstance(
                resolverType,
                new Func<string>(() => "Rei"));
            string value;
            Assert.That(resolver.TryResolve("PLAYERNAME", row, out value), Is.True);
            Assert.That(value, Is.EqualTo("Rei"));
            Assert.That(resolver.TryResolve("unknown", row, out value), Is.False);

            var progressType = RequireProductType("WhiteRoom.Novel.DialogueProgressService");
            var progress = Activator.CreateInstance(progressType, new object[] { false });
            try
            {
                progressType.GetMethod("RecordEvent")?.Invoke(progress, new object[] { "qa_probe" });
                var evaluator = (IDialogueConditionEvaluator)progress;
                Assert.That(evaluator.Evaluate("event:qa_probe", row), Is.True);
                Assert.That(evaluator.Evaluate("!event:qa_missing", row), Is.True);
                Assert.That(evaluator.Evaluate("event:qa_missing", row), Is.False);
            }
            finally
            {
                (progress as IDisposable)?.Dispose();
            }

            var root = new GameObject("WhiteRoomSaveTitleContract", typeof(DialogueSaveSystem));
            object saveService = null;
            try
            {
                var saveType = RequireProductType("WhiteRoom.Novel.NovelSaveService");
                saveService = Activator.CreateInstance(
                    saveType,
                    null,
                    root.GetComponent<DialogueSaveSystem>(),
                    DialogueSaveSlotConventions.FirstManualSlot,
                    false,
                    null);
                var buildTitle = saveType.GetMethod("BuildSaveTitle", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(buildTitle, Is.Not.Null);
                Assert.That(buildTitle.Invoke(saveService, new object[] { DialogueSaveSystem.QuickSaveSlot }), Is.EqualTo("Quick Save"));
                Assert.That(buildTitle.Invoke(saveService, new object[] { DialogueSaveSlotConventions.FirstManualSlot }),
                    Is.EqualTo("Save " + DialogueSaveSlotConventions.FirstManualSlot));
            }
            finally
            {
                (saveService as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertRoute(DialogueRepository repository, int startId, EndingRoute route)
        {
            Assert.That(route, Is.Not.Null);
            Assert.That(route.endingKey, Is.Not.Empty);
            var choices = new Queue<int>(route.choiceTargets ?? Array.Empty<int>());
            var visited = new HashSet<int>();
            var row = repository.Get(startId);

            var maximumSteps = repository.GetAll().Count() + 1;
            for (var step = 0; step < maximumSteps; step++)
            {
                Assert.That(row, Is.Not.Null, $"{route.endingKey}: missing row at step {step}");
                Assert.That(visited.Add(row.Id), Is.True, $"{route.endingKey}: cycle at row {row.Id}");
                if (!string.IsNullOrWhiteSpace(row.EndingKey))
                {
                    Assert.That(row.EndingKey, Is.EqualTo(route.endingKey));
                    Assert.That(choices, Is.Empty, $"{route.endingKey}: unused choice targets remain");
                    return;
                }

                var available = row.GetChoices();
                if (available.Count > 0)
                {
                    Assert.That(choices, Is.Not.Empty, $"{route.endingKey}: no choice target supplied for row {row.Id}");
                    var target = choices.Dequeue();
                    Assert.That(available.Any(choice => choice.NextId == target), Is.True,
                        $"{route.endingKey}: row {row.Id} cannot select {target}");
                    row = repository.Get(target);
                }
                else
                {
                    Assert.That(row.NextId, Is.GreaterThanOrEqualTo(0),
                        $"{route.endingKey}: route ended before its EndingKey at row {row.Id}");
                    row = repository.Get(row.NextId);
                }
            }

            Assert.Fail(route.endingKey + ": route exceeded the scenario-sized guard.");
        }

        private static int CountRowsThroughEnding(DialogueRepository repository, int startId)
        {
            var count = 0;
            var visited = new HashSet<int>();
            var row = repository.Get(startId);
            while (row != null && visited.Add(row.Id))
            {
                count++;
                if (!string.IsNullOrWhiteSpace(row.EndingKey))
                    return count;
                row = repository.Get(row.NextId);
            }

            Assert.Fail($"Route {startId} did not reach an ending.");
            return count;
        }

        private static Type RequireProductType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
