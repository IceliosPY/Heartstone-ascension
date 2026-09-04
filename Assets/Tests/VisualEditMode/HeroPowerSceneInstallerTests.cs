using CoH.Editor;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    public sealed class HeroPowerSceneInstallerTests
    {
        private GameObject _stage;
        private Transform _hud;
        private Transform _dragLayer;

        [SetUp]
        public void SetUp()
        {
            _stage = new GameObject("HeroPowerInstallerTestStage");
            _hud = NewChild(_stage.transform, "HUD");
            _dragLayer = NewChild(_stage.transform, "DragLayer");
        }

        [TearDown]
        public void TearDown()
        {
            if (_stage != null)
            {
                Object.DestroyImmediate(_stage);
            }

        }

        [Test]
        public void Cleanup_removes_owned_cross_hierarchy_objects_and_is_idempotent()
        {
            GameObject heroPower = NewChild(_hud, "HeroPower").gameObject;
            heroPower.AddComponent<HeroPowerView>();
            GameObject choices = NewChild(_hud, "Choices").gameObject;
            GameObject anchor = NewChild(_dragLayer, "ChoiceCardAnchor").gameObject;
            GameObject backdrop = NewChild(_dragLayer, "ChoiceBackdrop").gameObject;

            GameObject ownershipObject = NewChild(_hud, "HeroPowerPresentationRoot").gameObject;
            HeroPowerPresentationRoot ownership = ownershipObject.AddComponent<HeroPowerPresentationRoot>();
            ownership.Bind(heroPower, choices, anchor, backdrop);

            HeroPowerSceneInstaller.RemoveInstalledHierarchy(_hud, _dragLayer);
            HeroPowerSceneInstaller.RemoveInstalledHierarchy(_hud, _dragLayer);

            Assert.That(ownership == null, Is.True);
            Assert.That(heroPower == null, Is.True);
            Assert.That(CountDirectChildren(_hud, "HeroPowerPresentationRoot"), Is.Zero);
            Assert.That(CountDirectChildren(_hud, "Choices"), Is.Zero);
            Assert.That(CountDirectChildren(_dragLayer, "ChoiceCardAnchor"), Is.Zero);
            Assert.That(CountDirectChildren(_dragLayer, "ChoiceBackdrop"), Is.Zero);
        }

        [Test]
        public void Cleanup_removes_legacy_orphans_only_from_the_known_parents()
        {
            NewChild(_hud, "Choices");
            NewChild(_hud, "Choices");
            NewChild(_dragLayer, "ChoiceCardAnchor");
            NewChild(_dragLayer, "ChoiceBackdrop");

            Transform unrelated = NewChild(_stage.transform, "Unrelated");
            NewChild(unrelated, "Choices");
            NewChild(unrelated, "ChoiceCardAnchor");
            NewChild(unrelated, "ChoiceBackdrop");

            HeroPowerSceneInstaller.RemoveInstalledHierarchy(_hud, _dragLayer);

            Assert.That(CountDirectChildren(_hud, "Choices"), Is.Zero);
            Assert.That(CountDirectChildren(_dragLayer, "ChoiceCardAnchor"), Is.Zero);
            Assert.That(CountDirectChildren(_dragLayer, "ChoiceBackdrop"), Is.Zero);
            Assert.That(CountDirectChildren(unrelated, "Choices"), Is.EqualTo(1));
            Assert.That(CountDirectChildren(unrelated, "ChoiceCardAnchor"), Is.EqualTo(1));
            Assert.That(CountDirectChildren(unrelated, "ChoiceBackdrop"), Is.EqualTo(1));
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static int CountDirectChildren(Transform parent, string name)
        {
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name == name)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
