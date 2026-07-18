using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace kkmia.TalkSystem.Tests
{
    /// <summary>
    /// DialogueView + TypewriterEffect の実挙動（文字送り・スキップ・選択肢ボタンの使い回し）を
    /// 実際の再生ループで検証する。
    /// </summary>
    public sealed class DialogueViewPlayModeTests
    {
        private sealed class ViewHarness
        {
            public GameObject Root;
            public DialogueManager Manager;
            public DialogueView View;
            public TMP_Text BodyText;
        }

        [UnityTest]
        public IEnumerator Typewriter_TypesThenSkipsToFullText()
        {
            yield return DestroyExistingManager();

            var harness = CreateHarness(
                "Id,Speaker,Text,NextId\n" +
                "1,A,HelloWorldTypewriter,2\n" +
                "2,A,Done,-1\n",
                withTypewriter: true);

            try
            {
                harness.Manager.SetTypewriterSpeed(0.5f);
                harness.Manager.StartDialogue(1);

                Assert.IsTrue(harness.View.IsTyping, "開始直後は文字送り中");
                Assert.AreEqual(DialogueSessionState.Typing, harness.Manager.State);

                // 文字送り中の Next はスキップ（全文表示）として扱われる。
                harness.Manager.RequestNext();
                yield return null;

                Assert.IsFalse(harness.View.IsTyping);
                Assert.AreEqual(DialogueSessionState.WaitingForInput, harness.Manager.State);
                Assert.AreEqual("HelloWorldTypewriter", harness.BodyText.text);
                Assert.GreaterOrEqual(harness.BodyText.maxVisibleCharacters, "HelloWorldTypewriter".Length);

                // 行が確定した後の Next は次の行へ進む。
                harness.Manager.RequestNext();
                yield return null;
                Assert.AreEqual(2, harness.Manager.CurrentData.Id);
            }
            finally
            {
                Object.Destroy(harness.Root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ChoiceButtons_AreReusedAcrossChoiceLines()
        {
            yield return DestroyExistingManager();

            var harness = CreateHarness(
                "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                "1,A,PickOne,-1,,,,,A1->2|B1->2\n" +
                "2,A,PickTwo,-1,,,,,A2->3|B2->3\n" +
                "3,A,End,-1,,,,,\n",
                withTypewriter: false);

            try
            {
                harness.Manager.StartDialogue(1);
                yield return null;

                var firstButtons = new List<Button>(GetActiveChoiceButtons(harness.View));
                Assert.AreEqual(2, firstButtons.Count, "1 行目の選択肢が描画される");

                firstButtons[0].onClick.Invoke();
                yield return null;

                Assert.AreEqual(2, harness.Manager.CurrentData.Id);
                var secondButtons = GetActiveChoiceButtons(harness.View);
                Assert.AreEqual(2, secondButtons.Count, "2 行目の選択肢が描画される");
                CollectionAssert.AreEquivalent(firstButtons, secondButtons,
                    "選択肢ボタンは Destroy/Instantiate されずプールから再利用される");

                secondButtons[0].onClick.Invoke();
                yield return null;

                Assert.AreEqual(3, harness.Manager.CurrentData.Id);
                Assert.AreEqual(0, GetActiveChoiceButtons(harness.View).Count, "選択肢の無い行ではボタンが非表示に戻る");
            }
            finally
            {
                Object.Destroy(harness.Root);
            }

            yield return null;
        }

        private static ViewHarness CreateHarness(string csv, bool withTypewriter)
        {
            var root = new GameObject("DialogueViewHarness");
            root.SetActive(false);

            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);

            var viewObject = new GameObject("DialogueView", typeof(RectTransform));
            viewObject.transform.SetParent(canvasObject.transform, false);
            var view = viewObject.AddComponent<DialogueView>();

            var textObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(viewObject.transform, false);
            var bodyText = textObject.GetComponent<TMP_Text>();
            SetField(view, "bodyText", bodyText);

            if (withTypewriter)
            {
                var typewriter = textObject.AddComponent<TypewriterEffect>();
                SetField(view, "typewriter", typewriter);
            }

            var manager = root.AddComponent<DialogueManager>();
            SetField(manager, "csvFile", new TextAsset(csv));
            SetField(manager, "view", view);

            root.SetActive(true);

            return new ViewHarness
            {
                Root = root,
                Manager = manager,
                View = view,
                BodyText = bodyText
            };
        }

        private static List<Button> GetActiveChoiceButtons(DialogueView view)
        {
            var field = typeof(DialogueView).GetField("_choiceButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_choiceButtons");
            return new List<Button>((List<Button>)field.GetValue(view));
        }

        private static IEnumerator DestroyExistingManager()
        {
            if (DialogueManager.Instance != null)
            {
                Object.Destroy(DialogueManager.Instance.gameObject);
                yield return null;
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
