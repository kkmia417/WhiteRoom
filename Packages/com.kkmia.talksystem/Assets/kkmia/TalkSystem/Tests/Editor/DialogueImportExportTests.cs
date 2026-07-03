using NUnit.Framework;
using kkmia.TalkSystem.Editor;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueImportExportTests
    {
        [Test]
        public void CsvToJsonToCsv_PreservesDialogueText()
        {
            var csv = "Id,Speaker,Text,NextId\n1,A,\"Hello, JSON\",-1\n";

            var json = DialogueImportExportUtility.CsvToJson(csv);
            var roundTrip = DialogueImportExportUtility.JsonToCsv(json);
            var parsed = CsvLoader.ParseText<DialogueData>(roundTrip);

            Assert.AreEqual("Hello, JSON", parsed[1].Text);
        }

        [Test]
        public void CsvToJsonToCsv_PreservesProgressColumns()
        {
            var csv = DialogueCsvCodec.Write(DialogueSchema.FullHeaders, new[]
            {
                new[]
                {
                    "1", "A", "Progress", "-1", string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, "chapter_1", "route_a", "ending_good"
                }
            });

            var json = DialogueImportExportUtility.CsvToJson(csv);
            var roundTrip = DialogueImportExportUtility.JsonToCsv(json);
            var parsed = CsvLoader.ParseText<DialogueData>(roundTrip);

            Assert.AreEqual("chapter_1", parsed[1].ChapterKey);
            Assert.AreEqual("route_a", parsed[1].RouteKey);
            Assert.AreEqual("ending_good", parsed[1].EndingKey);
        }

        [Test]
        public void CsvToJsonToCsv_PreservesPresentationColumns()
        {
            var csv = DialogueCsvCodec.Write(DialogueSchema.FullHeaders, new[]
            {
                new[]
                {
                    "1", "A", "Stage", "-1", string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, "bg_day#fade:1", "theme#fade:2",
                    "door|step", "voice_001", "Alice@left:smile#fadein", string.Empty, string.Empty, string.Empty
                }
            });

            var json = DialogueImportExportUtility.CsvToJson(csv);
            var roundTrip = DialogueImportExportUtility.JsonToCsv(json);
            var parsed = CsvLoader.ParseText<DialogueData>(roundTrip);

            Assert.AreEqual("bg_day#fade:1", parsed[1].Background);
            Assert.AreEqual("theme#fade:2", parsed[1].Bgm);
            Assert.AreEqual("door|step", parsed[1].Se);
            Assert.AreEqual("voice_001", parsed[1].Voice);
            Assert.AreEqual("Alice@left:smile#fadein", parsed[1].CharactersRaw);
        }

        [Test]
        public void ExportTranslationCsv_PreservesExistingTranslations()
        {
            var scenario = DialogueCsvCodec.Write(DialogueSchema.FullHeaders, new[]
            {
                new[]
                {
                    "1", "A", "Hello {name}", "-1", string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, "bg_day", string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
                }
            });
            var existing = "Id,Source,en\n1,Old source,Hello {name}\n";

            var exported = DialogueImportExportUtility.ExportTranslationCsv(scenario, new[] { "en", "ja" }, existing);
            var table = DialogueTranslationTable.FromCsv(exported);

            string text;
            Assert.IsTrue(table.TryGet(1, "en", out text));
            Assert.AreEqual("Hello {name}", text);
            Assert.IsTrue(exported.Contains("Source"));
            Assert.IsTrue(exported.Contains("Hello {name}"));
        }

        [Test]
        public void YarnLikeToCsv_ImportsSpeakerLine()
        {
            var csv = DialogueImportExportUtility.YarnLikeToCsv("Guide: Hello from text");
            var parsed = CsvLoader.ParseText<DialogueData>(csv);

            Assert.AreEqual("Guide", parsed[1].Speaker);
            Assert.AreEqual("Hello from text", parsed[1].Text);
        }
    }
}
