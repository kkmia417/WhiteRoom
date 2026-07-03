using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace kkmia.TalkSystem.Tests
{
    public sealed class DialoguePresenterTests
    {
        [Test]
        public void Presenter_RebindAndDispose_UnsubscribesOldView()
        {
            var repo = CreateRepository();
            var first = new FakeDialogueView();
            var second = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, first);

            Assert.AreEqual(1, first.NextSubscriberCount);

            presenter.BindView(second);
            Assert.AreEqual(0, first.NextSubscriberCount);
            Assert.AreEqual(1, second.NextSubscriberCount);

            presenter.Dispose();
            Assert.AreEqual(0, second.NextSubscriberCount);
        }

        [Test]
        public void Presenter_NextRequested_AdvancesOnlyOnce()
        {
            var repo = CreateRepository();
            var view = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, view);

            presenter.Start(1);
            view.RaiseNext();

            Assert.AreEqual(2, presenter.CurrentData.Id);
        }

        [Test]
        public void Presenter_NaturalEnd_ClearsViewAndRaisesEndedOnce()
        {
            var repo = CreateRepository();
            var view = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, view);

            var endedCount = 0;
            presenter.DialogueEnded += _ => endedCount++;

            presenter.Start(1);
            view.RaiseNext(); // 1 -> 2 (末尾, NextId < 0)
            view.RaiseNext(); // 末尾で送り -> 自然終了

            Assert.AreEqual(DialogueSessionState.Ended, presenter.State);
            Assert.AreEqual(1, view.ClearCount);
            Assert.AreEqual(1, view.ForceStopCount);
            Assert.AreEqual(1, endedCount);
        }

        [Test]
        public void Presenter_RestoreState_DoesNotReplaySideEffects()
        {
            var repo = CreateEventRepository();
            var view = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, view);

            var dispatcher = new CountingEventDispatcher();
            presenter.SetEventDispatcher(dispatcher);

            var lineStartedCount = 0;
            presenter.LineStarted += _ => lineStartedCount++;

            presenter.Start(1); // EventKey 付きの行を開始
            Assert.AreEqual(1, lineStartedCount);
            Assert.AreEqual(1, dispatcher.DispatchCount);

            var save = presenter.CaptureState();
            var historyBefore = presenter.Session.History.Count;

            Assert.IsTrue(presenter.RestoreState(save));

            Assert.AreEqual(1, lineStartedCount, "復元では LineStarted を再発火しない");
            Assert.AreEqual(1, dispatcher.DispatchCount, "復元ではイベントを再ディスパッチしない");
            Assert.AreEqual(historyBefore, presenter.Session.History.Count, "復元で履歴が重複しない");
        }

        [Test]
        public void Presenter_Session_ExposesReadOnlyInterface()
        {
            var property = typeof(DialoguePresenter).GetProperty("Session");

            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(IReadOnlyDialogueSession), property.PropertyType);
        }

        [Test]
        public void Presenter_RestoreState_ResetsRollbackHistoryToRestoredLine()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,One,2\n2,A,Two,3\n3,A,Three,-1\n").Values);
            var view = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, view);

            presenter.Start(1);
            view.RaiseNext(); // -> line 2
            var saveAtLine2 = presenter.CaptureState();
            view.RaiseNext(); // -> line 3

            Assert.IsTrue(presenter.RestoreState(saveAtLine2));

            Assert.AreEqual(2, presenter.CurrentData.Id);
            Assert.IsFalse(presenter.CanRollback, "復元直後はロード前の履歴へ戻れない");
            Assert.IsFalse(presenter.Rollback(), "ロード前セッションの履歴を復元しない");

            view.RaiseNext(); // line 2 -> line 3
            Assert.IsTrue(presenter.CanRollback);
            Assert.IsTrue(presenter.Rollback());
            Assert.AreEqual(2, presenter.CurrentData.Id, "復元後に進めた分だけ戻れる");
        }

        [Test]
        public void Presenter_ProgressMarkersRaiseAndPersistInSaveData()
        {
            var repo = CreateProgressRepository();
            var view = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, view);
            var events = new List<DialogueProgressEventContext>();
            presenter.ProgressMarkerReached += events.Add;

            presenter.Start(1);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(DialogueProgressMarkerType.Chapter, events[0].Marker.Type);
            Assert.AreEqual("chapter_1", events[0].Marker.Key);
            Assert.IsTrue(events[0].Marker.IsFirstReach);
            Assert.AreEqual(DialogueProgressMarkerType.Route, events[1].Marker.Type);
            Assert.AreEqual("route_a", events[1].Marker.Key);
            Assert.IsTrue(events[1].Marker.IsFirstReach);

            view.RaiseNext();

            Assert.AreEqual(5, events.Count);
            Assert.AreEqual(DialogueProgressMarkerType.Chapter, events[2].Marker.Type);
            Assert.IsFalse(events[2].Marker.IsFirstReach);
            Assert.AreEqual(DialogueProgressMarkerType.Route, events[3].Marker.Type);
            Assert.IsFalse(events[3].Marker.IsFirstReach);
            Assert.AreEqual(DialogueProgressMarkerType.Ending, events[4].Marker.Type);
            Assert.AreEqual("ending_good", events[4].Marker.Key);
            Assert.IsTrue(events[4].Marker.IsFirstReach);

            var save = presenter.CaptureState();
            CollectionAssert.AreEqual(new[] { "chapter_1" }, save.Progress.ReachedChapterKeys);
            CollectionAssert.AreEqual(new[] { "route_a" }, save.Progress.ReachedRouteKeys);
            CollectionAssert.AreEqual(new[] { "ending_good" }, save.Progress.ReachedEndingKeys);
            Assert.AreEqual("ending_good", save.Progress.CurrentEndingKey);

            var restored = new DialoguePresenter(repo, new FakeDialogueView());
            var restoredEventCount = 0;
            restored.ProgressMarkerReached += _ => restoredEventCount++;

            Assert.IsTrue(restored.RestoreState(save));
            Assert.AreEqual(0, restoredEventCount);
            Assert.AreEqual("chapter_1", restored.Session.Progress.CurrentChapterKey);
            Assert.AreEqual("route_a", restored.Session.Progress.CurrentRouteKey);
            Assert.AreEqual("ending_good", restored.Session.Progress.CurrentEndingKey);
        }

        [Test]
        public void Presenter_RebindMidSession_KeepsSessionAndRedrawsCurrentLine()
        {
            var repo = CreateRepository();
            var first = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, first);

            presenter.Start(1);
            var current = presenter.CurrentData;
            Assert.AreEqual(1, current.Id);

            // View を差し替え、セッションを維持したまま現在行を再描画する。
            var second = new FakeDialogueView();
            presenter.BindView(second);
            presenter.RedrawCurrent();

            Assert.AreSame(current, presenter.CurrentData, "再バインドで現在行が失われない");
            Assert.AreEqual(1, second.LastShown.Id, "新 View に現在行が再描画される");

            // 古い View の操作は進行に影響しない。
            first.RaiseNext();
            Assert.AreEqual(1, presenter.CurrentData.Id);

            // 新 View の操作で会話が継続する。
            second.RaiseNext();
            Assert.AreEqual(2, presenter.CurrentData.Id);
        }

        [Test]
        public void Presenter_Rollback_ReturnsToPreviousLineWithoutSideEffects()
        {
            var repo = new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,One,2\n2,A,Two,3\n3,A,Three,-1\n").Values);
            var view = new FakeDialogueView();
            var presenter = new DialoguePresenter(repo, view);

            var lineStarted = 0;
            presenter.LineStarted += _ => lineStarted++;

            presenter.Start(1);   // line 1
            Assert.IsFalse(presenter.CanRollback, "先頭行では戻れない");

            view.RaiseNext();      // -> line 2
            view.RaiseNext();      // -> line 3
            Assert.AreEqual(3, presenter.CurrentData.Id);
            Assert.AreEqual(3, lineStarted);
            Assert.IsTrue(presenter.CanRollback);

            var historyBefore = presenter.Session.History.Count;

            Assert.IsTrue(presenter.Rollback()); // line 3 -> line 2
            Assert.AreEqual(2, presenter.CurrentData.Id, "直前の行へ戻る");
            Assert.AreEqual(2, view.LastShown.Id, "戻った行が再描画される");
            Assert.AreEqual(3, lineStarted, "ロールバックで LineStarted は再発火しない");
            Assert.AreEqual(historyBefore - 1, presenter.Session.History.Count, "戻った分だけ履歴が縮む");

            // 戻った後も継続して進める。
            view.RaiseNext(); // line 2 -> line 3
            Assert.AreEqual(3, presenter.CurrentData.Id);
        }

        private static DialogueRepository CreateRepository()
        {
            return new DialogueRepository(CsvLoader.ParseText<DialogueData>(
                "Id,Speaker,Text,NextId\n1,A,Hello,2\n2,A,End,-1\n").Values);
        }

        private static DialogueRepository CreateEventRepository()
        {
            var csv = "Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices\n" +
                      "1,A,Hello,2,,,,reward,\n" +
                      "2,A,End,-1,,,,,\n";
            return new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
        }

        private static DialogueRepository CreateProgressRepository()
        {
            var csv = DialogueCsvCodec.Write(DialogueSchema.FullHeaders, new[]
            {
                new[]
                {
                    "1", "A", "Start", "2", string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, "chapter_1", "route_a", string.Empty
                },
                new[]
                {
                    "2", "A", "End", "-1", string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, "chapter_1", "route_a", "ending_good"
                }
            });
            return new DialogueRepository(CsvLoader.ParseText<DialogueData>(csv).Values);
        }

        private sealed class CountingEventDispatcher : IDialogueEventDispatcher
        {
            public int DispatchCount { get; private set; }

            public void Dispatch(DialogueEventContext context)
            {
                DispatchCount++;
            }
        }

        private sealed class FakeDialogueView : IDialogueView
        {
            private Action _nextRequested;
            private Action<int> _choiceSelected;

            public int NextSubscriberCount { get; private set; }
            public int ChoiceSubscriberCount { get; private set; }
            public bool IsTyping { get; private set; }
            public int ClearCount { get; private set; }
            public int ForceStopCount { get; private set; }
            public int ShowCount { get; private set; }
            public DialogueData LastShown { get; private set; }

            public event Action NextRequested
            {
                add
                {
                    _nextRequested += value;
                    NextSubscriberCount++;
                }
                remove
                {
                    _nextRequested -= value;
                    NextSubscriberCount--;
                }
            }

            public event Action<int> ChoiceSelected
            {
                add
                {
                    _choiceSelected += value;
                    ChoiceSubscriberCount++;
                }
                remove
                {
                    _choiceSelected -= value;
                    ChoiceSubscriberCount--;
                }
            }

            public void Show(DialogueData data, IReadOnlyList<DialogueChoice> choices, Action onComplete)
            {
                IsTyping = false;
                ShowCount++;
                LastShown = data;
                if (onComplete != null)
                    onComplete();
            }

            public void CompleteTyping()
            {
                IsTyping = false;
            }

            public void Clear()
            {
                ClearCount++;
            }

            public void ForceStop()
            {
                ForceStopCount++;
            }

            public void SetTypewriterSpeed(float newInterval)
            {
            }

            public void RaiseNext()
            {
                if (_nextRequested != null)
                    _nextRequested();
            }

            public void RaiseChoice(int index)
            {
                if (_choiceSelected != null)
                    _choiceSelected(index);
            }
        }
    }
}
