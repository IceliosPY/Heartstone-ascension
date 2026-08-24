using System.IO;
using CoH.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoH.Editor
{
    /// <summary>
    /// Renders a still of the match scene with placeholder cards and minions in
    /// place, so the framing and the card layout can be checked without opening
    /// the editor.
    ///
    /// It populates the scene, takes the picture and throws the populated scene
    /// away without saving. Nothing it does survives the call.
    /// </summary>
    public static class MatchScenePreview
    {
        private const string ScenePath = "Assets/_Project/Scenes/Match.unity";
        private const string CardPrefab = "Assets/_Project/Prefabs/P_CardPlaceholder.prefab";
        private const string MinionPrefab = "Assets/_Project/Prefabs/P_MinionPlaceholder.prefab";

        [MenuItem("Conquest of Hearthstone/Capture Match Preview")]
        public static void Capture()
        {
            CaptureTo(Path.Combine(Directory.GetCurrentDirectory(), "match-preview.png"), 1920, 1080);
        }

        public static void CaptureTo(string outputPath, int width, int height)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            BoardAnchors anchors = Object.FindFirstObjectByType<BoardAnchors>();
            Camera camera = Object.FindFirstObjectByType<Camera>();

            if (anchors == null || camera == null)
            {
                Debug.LogError("The match scene is missing its anchors or its camera.");
                return;
            }

            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            GameObject minion = AssetDatabase.LoadAssetAtPath<GameObject>(MinionPrefab);

            PopulateHand(anchors.HandOf(Core.Identifiers.PlayerId.One), card, 5, "Test Soldier", "2", "2", "3");
            PopulateHand(anchors.HandOf(Core.Identifiers.PlayerId.Two), card, 4, null, null, null, null);

            PopulateBoard(anchors.BoardOf(Core.Identifiers.PlayerId.One), minion, 3);
            PopulateBoard(anchors.BoardOf(Core.Identifiers.PlayerId.Two), minion, 2);

            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 2;

            // Going through a render request rather than calling Camera.Render:
            // under a scriptable pipeline the direct call bypasses URP and
            // shades lit materials with a fallback, which makes a preview lie
            // about colours that are perfectly correct in the scene.
            UnityEngine.Rendering.RenderPipeline.StandardRequest request =
                new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };

            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                RenderTexture previous = camera.targetTexture;
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = previous;
            }

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();

            RenderTexture.active = active;

            File.WriteAllBytes(outputPath, picture.EncodeToPNG());

            Object.DestroyImmediate(picture);
            target.Release();
            Object.DestroyImmediate(target);

            Debug.Log("Match preview written to " + outputPath);

            // Reopen clean so the populated version is never saved.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void PopulateHand(
            Transform anchor, GameObject prefab, int count,
            string name, string mana, string attack, string health)
        {
            // Same numbers the scene wires into the presenter.
            HandFanSettings settings = new HandFanSettings
            {
                Scale = 0.62f,
                MaxWidth = 5.4f,
                PreferredSpacing = 0.72f,
                SpreadAngle = 14f,
                ArcDrop = 0.16f,
                DepthStep = 0.03f
            };

            for (int index = 0; index < count; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                CardPose pose = HandFanLayout.GetPose(index, count, settings);

                instance.transform.localPosition = pose.LocalPosition;
                instance.transform.localRotation = pose.LocalRotation;
                instance.transform.localScale = Vector3.one * pose.Scale;

                bool faceUp = name != null;
                Transform cover = instance.transform.Find("FaceDownCover");
                if (cover != null)
                {
                    cover.gameObject.SetActive(!faceUp);
                }

                if (!faceUp)
                {
                    continue;
                }

                SetText(instance, "NameBanner/NameText", name);
                SetText(instance, "ManaGem/ManaText", mana);
                SetText(instance, "Statistics/AttackGem/AttackText", attack);
                SetText(instance, "Statistics/HealthGem/HealthText", health);
                SetText(instance, "RulesBox/RulesText", string.Empty);

                Transform tribe = instance.transform.Find("TribeBanner");
                if (tribe != null)
                {
                    tribe.gameObject.SetActive(false);
                }
            }
        }

        private static void PopulateBoard(Transform anchor, GameObject prefab, int count)
        {
            for (int index = 0; index < count; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                instance.transform.localPosition = BoardRowLayout.GetPosition(index, count, 1.15f);

                SetText(instance, "NameText", "Test Soldier");
                SetText(instance, "AttackText", "2");
                SetText(instance, "HealthText", "3");
            }
        }

        private static void SetText(GameObject root, string path, string value)
        {
            Transform found = root.transform.Find(path);
            if (found == null)
            {
                return;
            }

            TMPro.TextMeshPro text = found.GetComponent<TMPro.TextMeshPro>();
            if (text != null)
            {
                text.text = value;
                text.ForceMeshUpdate();
            }
        }
    }
}
