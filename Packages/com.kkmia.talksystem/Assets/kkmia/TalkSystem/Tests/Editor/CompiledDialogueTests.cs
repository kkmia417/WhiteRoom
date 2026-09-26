using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using kkmia.TalkSystem.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace kkmia.TalkSystem.Tests
{
    public sealed class CompiledDialogueTests
    {
        private const string Header = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,Choices,ScreenEffect\n";

        [Test]
        public void SerializedRoundTripPreservesOrderChoicesMultilineAndExtraColumns()
        {
            var source = Header + "9,A,\"First, \"\"quoted\"\"\nline\",3,,start,,Go->3,rain\n3,B,Last,-1,,start,,,\n";
            var asset = ScriptableObject.CreateInstance<CompiledDialogueAsset>();
            var copy = ScriptableObject.CreateInstance<CompiledDialogueAsset>();
            try
            {
                asset.Compile(source);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(asset), copy);
                var repository = copy.CreateRepository();
                Assert.That(repository.GetAll().Select(row => row.Id), Is.EqualTo(new[] { 9, 3 }));
                Assert.That(repository.GetByTriggerKey("start").Id, Is.EqualTo(9));
                Assert.That(repository.GetByTriggerKey("START"), Is.Null);
                Assert.That(repository.Get(-999), Is.Null);
                Assert.That(repository.Get(9).Text, Is.EqualTo("First, \"quoted\"\nline"));
                Assert.That(repository.Get(9).GetChoices().Single().NextId, Is.EqualTo(3));
                Assert.That(repository.Get(9).TryGetExtra("screeneffect", out var extra), Is.True);
                Assert.That(extra, Is.EqualTo("rain"));
                Assert.That(repository.Get(3).RowNumber, Is.EqualTo(4));
                Assert.That(repository.Get(3).ExtraColumns, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [TestCase("1,A,A,2,,,,,\n")]
        [TestCase("1,A,A,-1,,,,,\n1,B,B,-1,,,,,\n")]
        [TestCase("1,A,A,-1,,, ,Go->999,\n")]
        public void InvalidCompilationCannotExposeOldRepository(string rows)
        {
            var asset = ScriptableObject.CreateInstance<CompiledDialogueAsset>();
            try
            {
                asset.Compile(Header + "1,A,Valid,-1,,,,,\n");
                Assert.Throws<InvalidOperationException>(() => asset.Compile(Header + rows));
                Assert.Throws<InvalidOperationException>(() => asset.CreateRepository());
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void LoaderReportsMissingAssetWithoutCompleting()
        {
            bool completed = false;
            string error = null;
            var load = new CompiledDialogueRepositoryLoader(null).Load(_ => completed = true, value => error = value);
            while (load.MoveNext()) { }
            Assert.That(completed, Is.False);
            Assert.That(error, Does.Contain("missing"));
        }

        [Test]
        public void ImportCombinesCrossFileLinksAndReimportsAfterSourceEdits()
        {
            var folder = "Assets/__CompiledDialogueTests_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            var first = folder + "/first.csv";
            var second = folder + "/second.csv";
            var manifest = folder + "/test.dialogue";
            try
            {
                File.WriteAllText(first, Header + "9,A,First,3,,start,,,rain\n");
                File.WriteAllText(second, Header + "3,B,Last,-1,,,,,\n");
                File.WriteAllText(manifest, "{\"sources\":[\"" + first + "\",\"" + second + "\"]}");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<CompiledDialogueAsset>(manifest);
                Assert.That(asset, Is.Not.Null);
                Assert.That(asset.CreateRepository().Get(9).NextId, Is.EqualTo(3));
                Assert.That(asset.CreateRepository().GetAll().Select(row => row.Id), Is.EqualTo(new[] { 9, 3 }));
                File.WriteAllText(second, Header + "3,B,Edited source text,-1,,,,,\n");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<CompiledDialogueAsset>(manifest);
                Assert.That(asset.CreateRepository().Get(3).Text, Is.EqualTo("Edited source text"));

                // A broken dependency must replace the valid output with an unusable import.
                File.WriteAllText(second, Header + "3,B,Broken,999,,,,,\n");
                LogAssert.Expect(LogType.Error, new Regex(".*NextId 999 does not exist.*", RegexOptions.Singleline));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<CompiledDialogueAsset>(manifest);
                Assert.Throws<InvalidOperationException>(() => asset.CreateRepository());
                Assert.Throws<BuildFailedException>(() => new DialogueSourceBuildGate().OnPreprocessBuild(null));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void ManifestRejectsMissingSourcesDuplicateSourcesAndMismatchedHeaders()
        {
            var folder = "Assets/__CompiledDialogueTests_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            var source = folder + "/first.csv";
            var second = folder + "/second.csv";
            // Keep this fixture unimported; malformed manifest errors are tested at the read boundary.
            var manifest = folder + "/manifest.json";
            try
            {
                File.WriteAllText(manifest, "{\"sources\":[\"" + source + "\"]}");
                Assert.Throws<FileNotFoundException>(() => DialogueSourceImporter.ReadSourceCsv(manifest));
                File.WriteAllText(source, Header + "1,A,Only,-1,,,,,\n");
                File.WriteAllText(manifest, "{\"sources\":[\"" + source + "\",\"" + source + "\"]}");
                Assert.Throws<InvalidDataException>(() => DialogueSourceImporter.ReadSourceCsv(manifest));
                File.WriteAllText(second, "Id,Speaker,Text,NextId\n2,A,Second,-1\n");
                File.WriteAllText(manifest, "{\"sources\":[\"" + source + "\",\"" + second + "\"]}");
                Assert.Throws<InvalidDataException>(() => DialogueSourceImporter.ReadSourceCsv(manifest));
            }
            finally
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
