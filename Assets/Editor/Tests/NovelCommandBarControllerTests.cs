using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class NovelCommandBarControllerTests
    {
        [SetUp]
        public void SetUp()
        {
            NovelUiFactory.EnsureFont(
                null,
                "Fonts/LogoTypeGothicCondense/LogoTypeGothicCondense");
        }

        [TearDown]
        public void TearDown()
        {
            DestroySceneObjects<Canvas>();
            DestroySceneObjects<EventSystem>();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EnsureCreatedBuildsAllCommandSurfacesWithoutLegacySpritesOrErrors()
        {
            var definitions = NovelCommandCatalog.Create(new NovelCommandBarBindings());
            var controller = new NovelCommandBarController(definitions);

            try
            {
                controller.EnsureCreated();
                controller.SetSceneVisible(true);

                AssertSolidSurface(controller.Root.GetComponent<Image>());

                var buttons = controller.Root.GetComponentsInChildren<Button>(true);
                Assert.That(buttons, Has.Length.EqualTo(NovelCommandBarController.ExpectedCommandCount));
                foreach (var button in buttons)
                    AssertSolidSurface(button.GetComponent<Image>());

                var tooltip = FindSceneObjects<Image>()
                    .Single(image => image.gameObject.name == "NovelCommandTooltip");
                AssertSolidSurface(tooltip);
                Assert.That(tooltip.raycastTarget, Is.False);
            }
            finally
            {
                controller.Dispose();
            }
        }

        private static void AssertSolidSurface(Image image)
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.Null);
            Assert.That(image.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(image.color.a, Is.GreaterThan(0f));
        }

        private static T[] FindSceneObjects<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static void DestroySceneObjects<T>() where T : Component
        {
            foreach (var component in FindSceneObjects<T>())
            {
                if (component != null)
                    Object.DestroyImmediate(component.gameObject);
            }
        }
    }
}
