using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WhiteRoom.Novel.PlayModeTests
{
    public sealed class WhiteRoomCollectionScreenPlayModeTests
    {
        [UnityTest]
        public IEnumerator EndingListAndEmptyGalleryOpenSwitchAndClose()
        {
            var loaderType = RequireType("WhiteRoom.Novel.CollectionCatalogLoader, Assembly-CSharp");
            var serviceType = RequireType("WhiteRoom.Novel.CollectionService, Assembly-CSharp");
            var screenType = RequireType("WhiteRoom.Novel.CollectionScreenController, Assembly-CSharp");
            var asset = Resources.Load<TextAsset>("WhiteRoom/collection_catalog");
            Assert.That(asset, Is.Not.Null);
            var load = loaderType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { asset });
            var catalog = Get(load, "Catalog");
            var service = Activator.CreateInstance(
                serviceType,
                catalog,
                new Func<string, List<string>>(category => category == "ending"
                    ? new List<string> { "ending:ending_beyond_correctness" }
                    : new List<string>()),
                new Action<string>(_ => { }));
            var screen = Activator.CreateInstance(screenType, service);

            try
            {
                Invoke(screen, "OpenEndingList");
                yield return null;
                Assert.That(Get(screen, "IsOpen"), Is.EqualTo(true));
                Assert.That(Get(screen, "VisibleItemCount"), Is.EqualTo(4));
                Assert.That(GameObject.Find("EndingListButton"), Is.Not.Null);
                Assert.That(GameObject.Find("GalleryButton"), Is.Not.Null);
                Assert.That(GameObject.Find("BackButton"), Is.Not.Null);

                Invoke(screen, "OpenGallery");
                yield return null;
                Assert.That(Get(screen, "VisibleItemCount"), Is.EqualTo(0));
                Assert.That(Get(screen, "IsEmptyStateVisible"), Is.EqualTo(true));

                Invoke(screen, "HandleCancel");
                yield return null;
                Assert.That(Get(screen, "IsOpen"), Is.EqualTo(false));
            }
            finally
            {
                var root = GameObject.Find("WhiteRoomCollectionScreen");
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                Resources.UnloadAsset(asset);
            }
        }

        private static Type RequireType(string name)
        {
            var type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static object Get(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }
    }
}
