using System.Linq;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class CsvCodecTests
    {
        [Test]
        public void Parse_QuotedComma_LoadsAsSingleField()
        {
            var csv = "Id,Speaker,Text,NextId\n1,A,\"Hello, world\",-1\n";
            var result = CsvLoader.ParseText<DialogueData>(csv);

            Assert.AreEqual("Hello, world", result[1].Text);
        }

        [Test]
        public void Write_ThenParse_RoundTripsEscapedQuotesAndMultilineText()
        {
            var headers = new[] { "Id", "Speaker", "Text", "NextId" };
            var rows = new[]
            {
                new[] { "1", "A", "He said \"Hi\"\nNext line", "-1" }
            };

            var csv = DialogueCsvCodec.Write(headers, rows);
            var result = CsvLoader.ParseText<DialogueData>(csv);

            Assert.AreEqual("He said \"Hi\"\nNext line", result[1].Text);
        }

        [Test]
        public void Parse_OptionalExtendedColumns_LoadsChoicesAndEventKey()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices,AutoNextSeconds\n" +
                      "1,A,Choose,-1,,,,open_shop,\"Buy->2|Leave->3\",0.5\n" +
                      "2,A,Buy,-1,,,,,,\n" +
                      "3,A,Leave,-1,,,,,,\n";

            var data = CsvLoader.ParseText<DialogueData>(csv)[1];

            Assert.AreEqual("open_shop", data.EventKey);
            Assert.AreEqual(2, data.GetChoices().Count);
            Assert.AreEqual(0.5f, data.AutoNextSeconds);
        }

        [Test]
        public void Parse_ProgressColumns_LoadsMarkers()
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

            var data = CsvLoader.ParseText<DialogueData>(csv)[1];

            Assert.AreEqual("chapter_1", data.ChapterKey);
            Assert.AreEqual("route_a", data.RouteKey);
            Assert.AreEqual("ending_good", data.EndingKey);
        }
    }
}
