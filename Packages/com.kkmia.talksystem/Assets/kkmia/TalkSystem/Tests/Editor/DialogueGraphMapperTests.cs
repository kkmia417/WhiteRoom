using NUnit.Framework;
using kkmia.TalkSystem.Editor;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueGraphMapperTests
    {
        [Test]
        public void FromCsv_BuildsNextAndChoiceEdges()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices,AutoNextSeconds\n" +
                      "1,A,Start,2,,,,,Choice->3,\n" +
                      "2,A,Next,-1,,,,,,\n" +
                      "3,A,Choice,-1,,,,,,\n";

            var model = DialogueGraphMapper.FromCsv(csv);

            Assert.AreEqual(3, model.Nodes.Count);
            Assert.AreEqual(2, model.Edges.Count);
            Assert.IsFalse(model.Diagnostics.HasErrors);
        }

        [Test]
        public void ToCsv_RoundTripsEditableNodeFields()
        {
            var model = DialogueGraphMapper.FromCsv("Id,Speaker,Text,NextId\n1,A,Hello,-1\n");
            var node = model.Find(1);
            node.text = "Hello, graph";

            var csv = DialogueGraphMapper.ToCsv(model);
            var parsed = CsvLoader.ParseText<DialogueData>(csv);

            Assert.AreEqual("Hello, graph", parsed[1].Text);
        }

        [Test]
        public void ToCsv_RoundTripsFullProductionSchemaFields()
        {
            var csv = DialogueCsvCodec.Write(DialogueSchema.FullHeaders, new[]
            {
                new[]
                {
                    "1", "Guide", "Hello {playerName}", "2", "smile", "intro", "has_ticket",
                    "unlock:cg:intro", "Go->2?has_ticket", "1.5", "bg_day#fade:1",
                    "theme#crossfade:2", "click|chime", "guide_001",
                    "Guide@left:smile#fadein", "chapter_1", "route_a", "ending_good"
                },
                new[]
                {
                    "2", "Guide", "End", "-1", string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
                }
            });

            var model = DialogueGraphMapper.FromCsv(csv);
            var node = model.Find(1);

            Assert.AreEqual("Hello {playerName}", node.text);
            Assert.AreEqual("bg_day#fade:1", node.background);
            Assert.AreEqual("theme#crossfade:2", node.bgm);
            Assert.AreEqual("click|chime", node.se);
            Assert.AreEqual("guide_001", node.voice);
            Assert.AreEqual("Guide@left:smile#fadein", node.charactersRaw);
            Assert.AreEqual("chapter_1", node.chapterKey);
            Assert.AreEqual("route_a", node.routeKey);
            Assert.AreEqual("ending_good", node.endingKey);

            var roundTripCsv = DialogueGraphMapper.ToCsv(model);
            var document = DialogueCsvCodec.Parse(roundTripCsv);
            var parsed = CsvLoader.ParseText<DialogueData>(roundTripCsv);

            CollectionAssert.AreEqual(DialogueSchema.FullHeaders, document.Headers);
            Assert.AreEqual("Hello {playerName}", parsed[1].Text);
            Assert.AreEqual("unlock:cg:intro", parsed[1].EventKey);
            Assert.AreEqual("bg_day#fade:1", parsed[1].Background);
            Assert.AreEqual("theme#crossfade:2", parsed[1].Bgm);
            Assert.AreEqual("click|chime", parsed[1].Se);
            Assert.AreEqual("guide_001", parsed[1].Voice);
            Assert.AreEqual("Guide@left:smile#fadein", parsed[1].CharactersRaw);
            Assert.AreEqual("chapter_1", parsed[1].ChapterKey);
            Assert.AreEqual("route_a", parsed[1].RouteKey);
            Assert.AreEqual("ending_good", parsed[1].EndingKey);
        }
    }
}
