using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CoH.Editor;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// That the editor shows a hand the way the game lays one out.
    ///
    /// The fan itself is shared code, so there is nothing to check there. What
    /// cannot be shared is the settings: they live on a component in the match
    /// scene, and an editor window cannot open a scene in order to draw a panel.
    /// So the editor keeps one copy, in one place, and this reads the scene and
    /// fails when the two drift.
    ///
    /// A copy with a test on it is a very different thing from a copy without
    /// one. Preview and runtime have disagreed twice in this project already,
    /// and both times it was a value someone had written down twice.
    /// </summary>
    public sealed class HandPresentationTests
    {
        private static float FromTheScene(string key)
        {
            Assert.That(File.Exists(HandPresentation.ScenePath), Is.True,
                "No match scene at " + HandPresentation.ScenePath + ".");

            string scene = File.ReadAllText(HandPresentation.ScenePath);

            int at = scene.IndexOf("handLayout:", System.StringComparison.Ordinal);

            Assert.That(at, Is.GreaterThan(0), "The scene wires no hand layout.");

            Match found = Regex.Match(
                scene.Substring(at, 400), @"^\s*" + Regex.Escape(key) + @":\s*(\S+)\s*$",
                RegexOptions.Multiline);

            Assert.That(found.Success, Is.True, "The scene's hand layout has no " + key + ".");

            return float.Parse(found.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void The_editors_hand_settings_are_the_scenes()
        {
            HandFanSettings mine = HandPresentation.Settings();

            Assert.That(mine.Scale, Is.EqualTo(FromTheScene("Scale")).Within(0.0001f),
                "The editor draws a hand at a different card scale from the game.");

            Assert.That(mine.Spacing, Is.EqualTo(FromTheScene("Spacing")).Within(0.0001f));
            Assert.That(mine.MaxWidth, Is.EqualTo(FromTheScene("MaxWidth")).Within(0.0001f));
            Assert.That(mine.PivotDistance, Is.EqualTo(FromTheScene("PivotDistance")).Within(0.0001f));
            Assert.That(mine.DepthStep, Is.EqualTo(FromTheScene("DepthStep")).Within(0.0001f));
        }

        /// <summary>
        /// And the plane it lays them on is the one the scene puts the hand on.
        /// </summary>
        [Test]
        public void The_editors_hand_sits_where_the_scenes_does()
        {
            string scene = File.ReadAllText(HandPresentation.ScenePath);

            int at = scene.IndexOf("m_Name: NearHand", System.StringComparison.Ordinal);

            Assert.That(at, Is.GreaterThan(0), "The scene has no near hand.");

            Match found = Regex.Match(
                scene.Substring(at, 1200),
                @"m_LocalPosition: \{x: (\S+?), y: (\S+?), z: (\S+?)\}");

            Assert.That(found.Success, Is.True, "The near hand has no position.");

            float y = float.Parse(found.Groups[2].Value, CultureInfo.InvariantCulture);
            float z = float.Parse(found.Groups[3].Value, CultureInfo.InvariantCulture);

            Assert.That(HandPresentation.AnchorPosition.y, Is.EqualTo(y).Within(0.0001f),
                "The editor's hand sits at a different height from the game's.");

            Assert.That(HandPresentation.AnchorPosition.z, Is.EqualTo(z).Within(0.0001f));
        }

        /// <summary>
        /// And it watches from where the match camera watches.
        ///
        /// The angle a card is seen at is most of how legible it is, so an
        /// editor looking from somewhere else would be answering a question
        /// nobody asked. The zoom is the editor's own — that is a crop, and a
        /// crop cannot change an angle.
        /// </summary>
        [Test]
        public void The_editors_eye_is_the_matchs_camera()
        {
            string scene = File.ReadAllText(HandPresentation.ScenePath);

            Match named = Regex.Match(
                scene, @"!u!1 &(\d+)(?:(?!--- ).)*?m_Name: MainCamera",
                RegexOptions.Singleline);

            Assert.That(named.Success, Is.True, "The scene has no main camera.");

            Match placed = Regex.Match(
                scene,
                @"Transform:(?:(?!--- ).)*?m_GameObject: \{fileID: " + named.Groups[1].Value +
                @"\}(?:(?!--- ).)*?m_LocalRotation: \{x: (\S+?),.*?" +
                @"m_LocalPosition: \{x: \S+?, y: (\S+?), z: (\S+?)\}",
                RegexOptions.Singleline);

            Assert.That(placed.Success, Is.True, "The main camera has no transform.");

            float y = float.Parse(placed.Groups[2].Value, CultureInfo.InvariantCulture);
            float z = float.Parse(placed.Groups[3].Value, CultureInfo.InvariantCulture);

            Assert.That(HandPresentation.EyePosition.y, Is.EqualTo(y).Within(0.01f),
                "The editor looks at the hand from a different height than the game.");

            Assert.That(HandPresentation.EyePosition.z, Is.EqualTo(z).Within(0.01f));

            // Turned about x alone, so the half angle is the arc sine of that term.
            float pitch = 2f * Mathf.Asin(
                float.Parse(placed.Groups[1].Value, CultureInfo.InvariantCulture)) * Mathf.Rad2Deg;

            Assert.That(HandPresentation.EyePitch, Is.EqualTo(pitch).Within(0.05f),
                "The editor looks down at the hand at a different angle than the game.");
        }

        /// <summary>
        /// The editor does not reimplement the fan. It asks for a pose the same
        /// way the presenter does, so a card in a preview is where a card in a
        /// hand would be.
        /// </summary>
        [Test]
        public void The_editor_asks_the_games_own_fan_where_a_card_goes()
        {
            HandFanSettings settings = HandPresentation.Settings();

            for (int count = 1; count <= 10; count++)
            {
                int middle = HandPresentation.IndexOf(HandPresentation.Place.Centre, count);
                int right = HandPresentation.IndexOf(HandPresentation.Place.Right, count);

                Assert.That(middle, Is.InRange(0, count - 1));
                Assert.That(right, Is.EqualTo(count - 1));

                CardPose pose = HandFanLayout.GetPose(right, count, settings);

                Assert.That(pose.Scale, Is.EqualTo(settings.Scale).Within(0.0001f),
                    "A card in the editor's hand is a different size from one in the game's.");
            }
        }
    }
}
