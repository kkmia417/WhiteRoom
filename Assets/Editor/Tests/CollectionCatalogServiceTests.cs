using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kkmia.TalkSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WhiteRoom.Novel.Editor.Tests
{
    public sealed class CollectionCatalogServiceTests
    {
        private const string CatalogPath = "Assets/Resources/WhiteRoom/collection_catalog.csv";
        private const string ScenarioPath = "Assets/Resources/Dialogue/r00_escape_talksystem.csv";

        [Test]
        public void CatalogContainsExactlyTheFourUniqueScenarioEndingsInStableOrder()
        {
            var result = LoadCatalog();
            var endings = result.Catalog.List(CollectionItemKind.Ending);
            var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioPath);
            var scenarioKeys = CsvLoader.Parse<DialogueData>(scenario).Values
                .Where(row => !string.IsNullOrWhiteSpace(row.EndingKey))
                .Select(row => row.EndingKey)
                .Distinct()
                .ToArray();

            Assert.That(result.Warnings, Is.Empty);
            Assert.That(endings.Count, Is.EqualTo(4));
            Assert.That(endings.Select(entry => entry.Key).Distinct().Count(), Is.EqualTo(4));
            CollectionAssert.AreEquivalent(scenarioKeys, endings.Select(entry => entry.Key));
            CollectionAssert.IsOrdered(endings.Select(entry => entry.Order).ToArray());
        }

        [Test]
        public void BadSideAndTrueEndingLockedNamesAreControlledByCatalogData()
        {
            var endings = LoadCatalog().Catalog.List(CollectionItemKind.Ending);

            Assert.That(
                endings.Single(entry => entry.Key == "ending_return_to_white_room").LockedNameRule,
                Is.EqualTo(LockedNameRule.Show));
            Assert.That(
                endings.Single(entry => entry.Key == "ending_beyond_correctness").LockedNameRule,
                Is.EqualTo(LockedNameRule.Mask));
            Assert.That(
                endings.Single(entry => entry.Key == "ending_single_answer").LockedNameRule,
                Is.EqualTo(LockedNameRule.Mask));
        }

        [Test]
        public void ServiceMatchesUnlocksMasksLockedNamesAndIgnoresUnknownIds()
        {
            var warnings = new List<string>();
            var persisted = new List<string>
            {
                "ending:ending_beyond_correctness",
                "ending:future_unknown"
            };
            var service = new CollectionService(
                LoadCatalog().Catalog,
                _ => persisted,
                warnings.Add);

            var items = service.Build(CollectionItemKind.Ending);

            Assert.That(items.Count, Is.EqualTo(4));
            Assert.That(items.Single(item => item.Entry.Key == "ending_beyond_correctness").IsUnlocked, Is.True);
            Assert.That(items.Single(item => item.Entry.Key == "ending_beyond_correctness").DisplayName, Is.EqualTo("正解の外側"));
            Assert.That(items.Single(item => item.Entry.Key == "ending_single_answer").DisplayName, Is.EqualTo("????????"));
            Assert.That(items.Single(item => item.Entry.Key == "ending_return_to_white_room").DisplayName, Is.EqualTo("白い部屋へ戻る"));
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("ending:future_unknown"));

            service.Build(CollectionItemKind.Ending);
            Assert.That(warnings.Count, Is.EqualTo(1), "Unknown persisted IDs should not spam the log on refresh.");
        }

        [Test]
        public void GalleryUsesCgPrefixAndHasValidEmptyStateData()
        {
            var catalog = LoadCatalog().Catalog;
            var service = new CollectionService(catalog, _ => new List<string>());

            Assert.That(catalog.List(CollectionItemKind.Cg), Is.Empty);
            Assert.That(service.Build(CollectionItemKind.Cg), Is.Empty);
            Assert.That(CollectionCatalog.GetUnlockId(CollectionItemKind.Cg, "opening"), Is.EqualTo("cg:opening"));
            Assert.That(CollectionCatalog.GetUnlockCategory(CollectionItemKind.Cg), Is.EqualTo(DialogueUnlockCategories.Cg));
        }

        [Test]
        public void LoaderSkipsDuplicateKeysAndReportsMalformedRows()
        {
            var result = CollectionCatalogLoader.LoadText(
                "Kind,Key,DisplayName,Category,Order,LockedName\n" +
                "ending,same,One,BAD END,10,Show\n" +
                "ending,same,Two,BAD END,20,Mask\n" +
                "unknown,key,Name,TYPE,not-number,Other\n");

            Assert.That(result.Catalog.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Warnings.Any(message => message.Contains("duplicate key")), Is.True);
            Assert.That(result.Warnings.Any(message => message.Contains("unknown Kind")), Is.True);
        }

        [Test]
        public void TitleMenuProvidesWorkingEndingListAndGalleryEntries()
        {
            var endingOpens = 0;
            var galleryOpens = 0;
            var configOpens = 0;
            var quitOpens = 0;
            var title = new TitleMenuController(
                null,
                () => { },
                () => { },
                () => endingOpens++,
                () => galleryOpens++,
                () => configOpens++,
                () => quitOpens++);
            var create = typeof(TitleMenuController).GetMethod(
                "CreateMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null);
            var root = (GameObject)create.Invoke(title, null);
            try
            {
                var buttons = root.GetComponentsInChildren<Button>(true)
                    .ToDictionary(button => button.name);
                Assert.That(buttons.ContainsKey("EndingListButton"), Is.True);
                Assert.That(buttons.ContainsKey("GalleryButton"), Is.True);
                Assert.That(buttons.ContainsKey("ConfigButton"), Is.True);
                Assert.That(buttons.ContainsKey("QuitButton"), Is.True);

                buttons["EndingListButton"].onClick.Invoke();
                buttons["GalleryButton"].onClick.Invoke();
                buttons["ConfigButton"].onClick.Invoke();
                buttons["QuitButton"].onClick.Invoke();
                Assert.That(endingOpens, Is.EqualTo(1));
                Assert.That(galleryOpens, Is.EqualTo(1));
                Assert.That(configOpens, Is.EqualTo(1));
                Assert.That(quitOpens, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CollectionCatalogLoadResult LoadCatalog()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            Assert.That(asset, Is.Not.Null);
            return CollectionCatalogLoader.Load(asset);
        }
    }
}
