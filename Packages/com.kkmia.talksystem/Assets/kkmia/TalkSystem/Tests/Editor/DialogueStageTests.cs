using System.Linq;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueStageTests
    {
        [Test]
        public void StageDirective_ParsesSlotExpressionAndAnimation()
        {
            var directives = DialogueStageDirective.ParseList("Alice@left:smile#fadein");

            Assert.AreEqual(1, directives.Count);
            var directive = directives[0];
            Assert.AreEqual("Alice", directive.CharacterKey);
            Assert.AreEqual("left", directive.Slot);
            Assert.AreEqual("smile", directive.Expression);
            Assert.AreEqual("fadein", directive.Animation);
            Assert.IsFalse(directive.IsExit);
            Assert.IsFalse(directive.IsClearAll);
        }

        [Test]
        public void StageDirective_ParsesExitAndClearAll()
        {
            var directives = DialogueStageDirective.ParseList("-Bob | *");

            Assert.AreEqual(2, directives.Count);
            Assert.IsTrue(directives[0].IsExit);
            Assert.AreEqual("Bob", directives[0].CharacterKey);
            Assert.IsTrue(directives[1].IsClearAll);
        }

        [Test]
        public void StageDirective_CharacterOnly_UsesDefaults()
        {
            var directives = DialogueStageDirective.ParseList("Carol");

            Assert.AreEqual(1, directives.Count);
            Assert.AreEqual("Carol", directives[0].CharacterKey);
            Assert.IsFalse(directives[0].HasSlot);
            Assert.IsFalse(directives[0].HasExpression);
            Assert.IsFalse(directives[0].HasAnimation);
        }

        [Test]
        public void StageDirective_MalformedEntries_AreSkipped()
        {
            // 空エントリと退場マーカーのみのエントリは無効。
            var directives = DialogueStageDirective.ParseList("Alice | | -");

            Assert.AreEqual(1, directives.Count);
            Assert.AreEqual("Alice", directives[0].CharacterKey);
        }

        [Test]
        public void MediaCue_ParsesKeyTransitionAndDuration()
        {
            var cue = DialogueMediaCue.Parse("forest#crossfade:1.5");

            Assert.IsTrue(cue.HasValue);
            Assert.IsFalse(cue.IsClear);
            Assert.AreEqual("forest", cue.Key);
            Assert.AreEqual("crossfade", cue.Transition);
            Assert.AreEqual(1.5f, cue.Duration);
        }

        [Test]
        public void MediaCue_ClearKeyword_SetsIsClear()
        {
            var cue = DialogueMediaCue.Parse("stop#fade:0.5");

            Assert.IsTrue(cue.HasValue);
            Assert.IsTrue(cue.IsClear);
            Assert.AreEqual(string.Empty, cue.Key);
            Assert.AreEqual("fade", cue.Transition);
        }

        [Test]
        public void MediaCue_MalformedDuration_IsFlagged()
        {
            bool malformed;
            var cue = DialogueMediaCue.Parse("forest#fade:soon", out malformed);

            Assert.IsTrue(malformed);
            Assert.IsFalse(cue.HasDuration);
            Assert.AreEqual("forest", cue.Key);
        }

        [Test]
        public void CsvLoader_MapsPresentationColumns()
        {
            var csv = "Id,Speaker,Text,NextId,Background,Bgm,Se,Voice,Characters\n" +
                      "1,A,Hi,-1,forest#fade,theme,door|step,line_001,Alice@left:smile\n";

            var data = CsvLoader.ParseText<DialogueData>(csv)[1];

            Assert.AreEqual("forest#fade", data.Background);
            Assert.AreEqual("theme", data.Bgm);
            Assert.AreEqual("line_001", data.Voice);
            Assert.AreEqual(2, data.GetSeKeys().Count);
            Assert.AreEqual(1, data.GetStageDirectives().Count);
            Assert.AreEqual("forest", data.GetBackgroundCue().Key);
        }

        [Test]
        public void Validator_WarnsOnMalformedDirectiveAndDuration()
        {
            var csv = "Id,Speaker,Text,NextId,Background,Characters\n" +
                      "1,A,Hi,-1,forest#fade:soon,Alice@left | -\n";

            var report = DialogueValidator.ValidateCsv(csv, new[] { 1 });

            Assert.IsTrue(report.Messages.Any(m =>
                m.FieldName == DialogueSchema.Characters &&
                m.Severity == DialogueValidationSeverity.Warning &&
                m.Message.Contains("could not be parsed")));

            Assert.IsTrue(report.Messages.Any(m =>
                m.FieldName == DialogueSchema.Background &&
                m.Severity == DialogueValidationSeverity.Warning &&
                m.Message.Contains("duration")));
        }
    }
}
