using System;
using System.Diagnostics;
using System.Linq;
using kkmia.TalkSystem;
using kkmia.TalkSystem.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WhiteRoom.Novel.Editor
{
    public static class WhiteRoomDialogueCompilationValidation
    {
        private const string ManifestPath = "Assets/Resources/Dialogue/r00_escape_talksystem.dialogue";
        private const string ProfilePath = "Assets/Presentation/Validation/WhiteRoomDialogueValidationProfile.asset";

        [MenuItem("Tools/WhiteRoom/Validate and Measure Compiled Dialogue")]
        public static void ValidateFromCommandLine()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CompiledDialogueAsset>(ManifestPath);
            if (asset == null) throw new InvalidOperationException("Compiled scenario is missing.");
            var csv = new TextAsset(DialogueSourceImporter.ReadSourceCsv(ManifestPath));
            try
            {
                var source = new DialogueRepository(csv).GetAll().ToArray();
                var compiled = asset.CreateRepository().GetAll().ToArray();
                if (source.Length != compiled.Length) throw new InvalidOperationException("Row counts differ.");
                for (var i = 0; i < source.Length; i++)
                {
                    if (JsonUtility.ToJson(source[i]) != JsonUtility.ToJson(compiled[i])
                        || source[i].RowNumber != compiled[i].RowNumber
                        || !source[i].ExtraColumns.OrderBy(pair => pair.Key)
                            .SequenceEqual(compiled[i].ExtraColumns.OrderBy(pair => pair.Key)))
                        throw new InvalidOperationException("Compiled row differs: " + source[i].Id);
                }
                var profile = AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(ProfilePath);
                var report = DialogueValidationRunner.ValidateProfile(profile);
                if (report.HasErrors) throw new InvalidOperationException(string.Join("\n", report.Messages));
                Debug.Log("Compiled dialogue equivalence: " + compiled.Length + " rows, all fields and extra columns preserved; profile passed.");
                Measure("CSV parse + validation", () => new DialogueRepository(csv));
                Measure("Compiled indexes", asset.CreateRepository);
            }
            finally { UnityEngine.Object.DestroyImmediate(csv); }
        }

        private static void Measure(string name, Func<IDialogueRepository> create)
        {
            const int iterations = 5;
            create(); // Warm JIT and caches. Measures repository construction, not asset I/O/deserialization.
            var timer = new Stopwatch();
            var before = GC.GetAllocatedBytesForCurrentThread();
            timer.Start();
            for (var i = 0; i < iterations; i++) create();
            timer.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            var allocationEvidence = allocated > 0
                ? $"{allocated / iterations:N0} managed bytes/construction"
                : "allocation counter unavailable on this runtime";
            Debug.Log($"Dialogue benchmark {name}: {timer.Elapsed.TotalMilliseconds / iterations:F3} ms, {allocationEvidence} (warm, n={iterations}).");
        }
    }
}
