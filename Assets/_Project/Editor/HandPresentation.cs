using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// How the game presents a card in a hand, for the editor tools that need to
    /// show the same thing.
    ///
    /// The fan itself is not reimplemented here: <see cref="HandFanLayout"/> is
    /// the one that decides where a card goes, and the hover comes off the real
    /// prefab, so an editor showing a resting or a hovered card is running the
    /// game's own arithmetic on the game's own numbers.
    ///
    /// What does have to be repeated is the settings, because they live in the
    /// scene and an editor window cannot open a scene to draw a panel. That copy
    /// is the one drift risk in the whole arrangement, so it is kept in exactly
    /// one place and a test reads the scene and fails if the two disagree.
    /// </summary>
    public static class HandPresentation
    {
        public const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        /// <summary>
        /// The near hand's settings, as the match scene wires them.
        ///
        /// Checked against the scene by HandPresentationTests. If that test
        /// fails, the scene is right and this is stale.
        /// </summary>
        public static HandFanSettings Settings() => new HandFanSettings
        {
            Scale = 1.56f,
            Spacing = 0.765f,
            MaxWidth = 7.56f,
            PivotDistance = 15.0f,
            DepthStep = 0.035f
        };

        /// <summary>How far the hand's plane is tilted, and how high it sits.</summary>
        public const float AnchorTilt = 36f;

        public static readonly Vector3 AnchorPosition = new Vector3(0f, 1.88f, -4.75f);

        /// <summary>
        /// Where the match camera watches from, and how far down it looks.
        ///
        /// An editor showing a card in a hand has to look at it from where the
        /// player does, or the foreshortening is somebody's guess at the
        /// foreshortening. Checked against the scene, like the settings above.
        /// </summary>
        public static readonly Vector3 EyePosition = new Vector3(0f, 9.5f, -7.75f);

        public const float EyePitch = 54f;

        /// <summary>Where in a hand a card is being looked at.</summary>
        public enum Place
        {
            Left = 0,
            Centre = 1,
            Right = 2
        }

        public static int IndexOf(Place place, int count)
        {
            switch (place)
            {
                case Place.Left:
                    return 0;

                case Place.Right:
                    return Mathf.Max(0, count - 1);

                default:
                    return Mathf.Max(0, (count - 1) / 2);
            }
        }

        /// <summary>
        /// Poses a card the way a hand would, and hands back the anchor it needs
        /// to sit under.
        ///
        /// The anchor matters: the fan works in a tilted frame, and a card posed
        /// without it would be in the right place on a plane nobody is looking
        /// at. This is also what makes a hovered card's turn toward the camera
        /// mean the same thing here as it does in a match.
        /// </summary>
        public static Transform Anchor(Transform parent)
        {
            GameObject anchor = new GameObject("Hand") { hideFlags = HideFlags.HideAndDontSave };

            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = AnchorPosition;
            anchor.transform.localRotation = Quaternion.Euler(AnchorTilt, 0f, 0f);

            return anchor.transform;
        }

        /// <summary>
        /// Puts one card where the hand would put it, resting or being read.
        ///
        /// Through the view's own methods rather than by setting a transform,
        /// so the hover is the prefab's hover: its lift, its forward, its scale
        /// and its turn toward the camera, none of them repeated here.
        /// </summary>
        public static void Pose(CardView view, int index, int count, bool hovered)
        {
            CardPose pose = HandFanLayout.GetPose(index, count, Settings());

            view.SetRestingPose(pose.LocalPosition, pose.LocalRotation, pose.Scale);
            view.SetHandOrder(index);
            view.SetHovered(hovered);
            view.SnapToPose();
        }
    }
}
