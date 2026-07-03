using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialogueInlineTextTests
    {
        [Test]
        public void Parse_PlainText_IsSingleTextToken()
        {
            var tokens = DialogueInlineTagParser.Parse("こんにちは");

            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual("こんにちは", Text(tokens[0]));
        }

        [Test]
        public void Parse_Ruby_ProducesRubyToken()
        {
            var tokens = DialogueInlineTagParser.Parse("これは[ruby=せかい]世界[/ruby]だ");

            Assert.AreEqual(3, tokens.Count);
            Assert.AreEqual("これは", Text(tokens[0]));
            var ruby = (DialogueRubyToken)tokens[1];
            Assert.AreEqual("世界", ruby.Base);
            Assert.AreEqual("せかい", ruby.Ruby);
            Assert.AreEqual("だ", Text(tokens[2]));
        }

        [Test]
        public void Parse_WaitAndSpeed_ProduceCommandTokens()
        {
            var tokens = DialogueInlineTagParser.Parse("a[w=0.5]b[speed=2]c");

            Assert.AreEqual(0.5f, ((DialogueWaitToken)tokens[1]).Seconds, 1e-6f);
            Assert.AreEqual(2f, ((DialogueSpeedToken)tokens[3]).Scale, 1e-6f);
        }

        [Test]
        public void Parse_UnknownTag_IsPassedThroughLiterally()
        {
            var tokens = DialogueInlineTagParser.Parse("a[foo]b");

            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual("a[foo]b", Text(tokens[0]));
        }

        [Test]
        public void Parse_EscapedBracketAndUnclosed_DoNotCrash()
        {
            Assert.AreEqual("a[b", Text(DialogueInlineTagParser.Parse("a[[b")[0]));
            Assert.AreEqual("a[b", Text(DialogueInlineTagParser.Parse("a[b")[0]));
        }

        [Test]
        public void Build_Wait_KeepsVisibleIndexFromBaseText()
        {
            var built = DialogueInlineText.Build("ab[w=0.5]cd");

            Assert.AreEqual("abcd", built.DisplayText);
            Assert.AreEqual(1, built.Commands.Count);
            Assert.AreEqual(DialogueInlineCommandKind.Wait, built.Commands[0].Kind);
            Assert.AreEqual(0.5f, built.Commands[0].Value, 1e-6f);
            Assert.AreEqual(2, built.Commands[0].VisibleCharIndex);
        }

        [Test]
        public void Build_Ruby_CountsBaseAndRubyAsVisible()
        {
            var built = DialogueInlineText.Build("[ruby=よ]世[/ruby][w=1]x");

            // 親文字1 + ルビ1 = 可視2 のあとにウェイト。
            Assert.AreEqual(2, built.Commands[0].VisibleCharIndex);
            StringAssert.Contains("世", built.DisplayText);
            StringAssert.Contains("よ", built.DisplayText);
        }

        [Test]
        public void Build_Color_EmitsRichTextTags()
        {
            var built = DialogueInlineText.Build("[color=#ff0000]赤[/color]");

            Assert.AreEqual("<color=#ff0000>赤</color>", built.DisplayText);
            Assert.IsEmpty(built.Commands);
        }

        private static string Text(DialogueInlineToken token)
        {
            return ((DialogueTextToken)token).Text;
        }
    }
}
