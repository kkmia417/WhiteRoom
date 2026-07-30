using System.Linq;
using kkmia.TalkSystem;
using NUnit.Framework;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class InGameSystemServiceTests
    {
        [Test]
        public void OverlayCoordinatorSuspendsAndRestoresExactPlaybackModeOnce()
        {
            var mode = DialoguePlaybackMode.Auto;
            var inputEnabled = true;
            var stopCount = 0;
            var coordinator = new GameplayOverlayCoordinator(
                () => mode,
                next => mode = next,
                () => stopCount++,
                enabled => inputEnabled = enabled);

            coordinator.Suspend();
            coordinator.Suspend();

            Assert.That(coordinator.IsSuspended, Is.True);
            Assert.That(coordinator.SuspendedMode, Is.EqualTo(DialoguePlaybackMode.Auto));
            Assert.That(mode, Is.EqualTo(DialoguePlaybackMode.Normal));
            Assert.That(inputEnabled, Is.False);
            Assert.That(stopCount, Is.EqualTo(2));

            coordinator.Resume();

            Assert.That(coordinator.IsSuspended, Is.False);
            Assert.That(mode, Is.EqualTo(DialoguePlaybackMode.Auto));
            Assert.That(inputEnabled, Is.True);
        }

        [Test]
        public void OverlayTransitionResetDiscardsSuspendedModeAndKeepsInputBlocked()
        {
            var mode = DialoguePlaybackMode.Skip;
            var inputEnabled = true;
            var coordinator = new GameplayOverlayCoordinator(
                () => mode,
                next => mode = next,
                null,
                enabled => inputEnabled = enabled);

            coordinator.Suspend();
            coordinator.ResetForTransition();
            coordinator.Resume();

            Assert.That(coordinator.IsSuspended, Is.False);
            Assert.That(mode, Is.EqualTo(DialoguePlaybackMode.Normal));
            Assert.That(inputEnabled, Is.False);
        }

        [Test]
        public void TitleReturnRequiresConfirmationOnlyForProgressAfterLastSave()
        {
            var resets = 0;
            var returns = 0;
            var service = new TitleReturnService(() => resets++, () => returns++);

            service.MarkProgressChanged();
            Assert.That(service.RequestReturnToTitle(), Is.EqualTo(TitleReturnRequestResult.ConfirmationRequired));
            Assert.That(resets, Is.Zero);
            Assert.That(returns, Is.Zero);

            Assert.That(service.ConfirmReturnToTitle(), Is.EqualTo(TitleReturnRequestResult.Started));
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(returns, Is.EqualTo(1));
            Assert.That(service.RequestReturnToTitle(), Is.EqualTo(TitleReturnRequestResult.Rejected),
                "Repeated input during scene transition must be ignored.");

            service.NotifySceneLoaded();
            service.MarkProgressChanged();
            service.MarkProgressSaved();
            Assert.That(service.RequestReturnToTitle(), Is.EqualTo(TitleReturnRequestResult.Started));
            Assert.That(resets, Is.EqualTo(2));
            Assert.That(returns, Is.EqualTo(2));
        }

        [Test]
        public void CommandCatalogConnectsAllIssue42Operations()
        {
            var config = 0;
            var hide = 0;
            var title = 0;
            var bindings = new NovelCommandBarBindings
            {
                OpenSystemConfig = () => config++,
                HideMessage = () => hide++,
                ReturnTitle = () => title++,
                CanOpenSystemConfig = () => true,
                CanHideMessage = () => true,
                CanReturnTitle = () => true
            };
            var definitions = NovelCommandCatalog.Create(bindings);

            foreach (var id in new[]
                     {
                         NovelCommandId.SystemConfig,
                         NovelCommandId.HideMessage,
                         NovelCommandId.ReturnTitle
                     })
            {
                var command = definitions.Single(item => item.Id == id);
                Assert.That(command.CanExecute(), Is.True, id.ToString());
                command.Execute();
            }

            Assert.That(config, Is.EqualTo(1));
            Assert.That(hide, Is.EqualTo(1));
            Assert.That(title, Is.EqualTo(1));
        }
    }
}
