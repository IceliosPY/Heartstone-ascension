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

        /// <summary>
        /// The two interaction states a still can show: a card in the air over
        /// an open slot, and an attack being aimed.
        ///
        /// The poses here are the same ones the controller computes at run time,
        /// written out by hand because nothing in this scene is alive: an editor
        /// still never runs Awake or LateUpdate, so a preview has to place what
        /// the game would have eased into.
        /// </summary>
        [MenuItem("Conquest of Hearthstone/Capture Interaction Preview")]
        public static void CaptureInteraction()
        {
            CaptureInteractionTo(
                Path.Combine(Directory.GetCurrentDirectory(), "interaction-drag.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "interaction-aim.png"),
                1920, 1080);
        }

        public static void CaptureInteractionTo(string dragPath, string aimPath, int width, int height)
        {
            // --- dragging a card over an open slot -------------------------
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            BoardAnchors anchors = Object.FindFirstObjectByType<BoardAnchors>();
            Camera camera = Object.FindFirstObjectByType<Camera>();
            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            GameObject minion = AssetDatabase.LoadAssetAtPath<GameObject>(MinionPrefab);

            // Five in the fan, because the sixth is in the player's hand.
            PopulateHand(anchors.Hand(true), card, 5, 1f, "Test Soldier", "2", "2", "3");
            PopulateHand(anchors.Hand(false), card, 5, 0.55f, null, null, null, null);

            HoverCardAt(anchors.Hand(true), 2);

            // Three minions holding a gap open at slot 2, exactly as the drop
            // resolver arranges them.
            PopulateRowWithGap(anchors.Board(true), minion, 3, 2);
            PopulateBoard(anchors.Board(false), minion, 3);

            ShowInsertionMarker(anchors.Board(true), 3, 2);
            DraggedCard(camera, card, new Vector3(1.2f, 0.2f, -1.05f));

            PopulateHero("NearHeroView", "PLAYER 1", "30", "deck 21   hand 6", null);
            PopulateHero("FarHeroView", "PLAYER 2", "24", "deck 21   hand 5", "5");

            Render(camera, dragPath, width, height);

            // --- aiming an attack ------------------------------------------
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            anchors = Object.FindFirstObjectByType<BoardAnchors>();
            camera = Object.FindFirstObjectByType<Camera>();

            PopulateHand(anchors.Hand(true), card, 6, 1f, "Test Soldier", "2", "2", "3");
            PopulateHand(anchors.Hand(false), card, 5, 0.55f, null, null, null, null);

            PopulateBoard(anchors.Board(true), minion, 3);
            PopulateBoard(anchors.Board(false), minion, 3);

            PopulateHero("NearHeroView", "PLAYER 1", "30", "deck 21   hand 6", null);
            PopulateHero("FarHeroView", "PLAYER 2", "24", "deck 21   hand 5", "5");

            AimAnAttack(anchors);

            Render(camera, aimPath, width, height);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Lifts one card of the fan the way hovering does.</summary>
        private static void HoverCardAt(Transform hand, int index)
        {
            if (index >= hand.childCount)
            {
                return;
            }

            Transform hovered = hand.GetChild(index);

            // The offsets on the card prefab: up, toward the camera, straightened
            // out of the fan, and a little larger.
            hovered.localPosition += new Vector3(0f, 0.5f, -0.62f);
            hovered.localRotation = Quaternion.identity;
            hovered.localScale = Vector3.one * (0.9f * 1.24f);
        }

        /// <summary>The card in the air, on the plane the drag holds it at.</summary>
        private static void DraggedCard(Camera camera, GameObject prefab, Vector3 aimedAt)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "DraggedCard";

            Vector3 origin = camera.transform.position;
            Vector3 direction = (aimedAt - origin).normalized;

            instance.transform.position = origin + direction * 8.2f + camera.transform.up * 0.62f;
            instance.transform.rotation = camera.transform.rotation;
            instance.transform.localScale = Vector3.one * 0.8f;

            SetText(instance, "NameBanner/NameText", "Test Soldier");
            SetText(instance, "ManaGem/ManaText", "2");
            SetText(instance, "Statistics/AttackGem/AttackText", "2");
            SetText(instance, "Statistics/HealthGem/HealthText", "3");
            SetText(instance, "RulesBox/RulesText", string.Empty);
            Hide(instance, "TribeBanner");
            Hide(instance, "FaceDownCover");
        }

        private static void PopulateRowWithGap(Transform anchor, GameObject prefab, int count, int gap)
        {
            for (int index = 0; index < count; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                instance.transform.localPosition = BoardDropResolver.PositionWithGap(index, count, gap, 1.2f);

                SetText(instance, "NameText", "Test Soldier");
                SetText(instance, "AttackPlate/AttackText", "2");
                SetText(instance, "HealthPlate/HealthText", "3");
            }
        }

        private static void ShowInsertionMarker(Transform row, int count, int slot)
        {
            BoardInsertionMarker marker = Object.FindFirstObjectByType<BoardInsertionMarker>();
            if (marker == null)
            {
                Debug.LogWarning("The scene has no insertion marker.");
                return;
            }

            marker.Show(row, BoardDropResolver.GapPosition(count, slot, 1.2f), slot);
        }

        /// <summary>
        /// An arrow from the leftmost friendly minion to the far hero, with every
        /// enemy character marked, and the one under the pointer marked harder.
        /// </summary>
        private static void AimAnAttack(BoardAnchors anchors)
        {
            TargetingArrow arrow = Object.FindFirstObjectByType<TargetingArrow>();
            GameObject nearHero = GameObject.Find("NearHeroView");
            GameObject farHero = GameObject.Find("FarHeroView");

            Transform attacker = anchors.Board(true).GetChild(0);
            Transform row = anchors.Board(false);

            for (int index = 0; index < row.childCount; index++)
            {
                MinionView view = row.GetChild(index).GetComponent<MinionView>();
                if (view != null)
                {
                    view.SetTargetable(true);
                    view.SetTargetHighlighted(false);
                }
            }

            HeroView enemy = farHero == null ? null : farHero.GetComponent<HeroView>();
            if (enemy != null)
            {
                enemy.SetTargetable(true);
                enemy.SetTargetHighlighted(true);
            }

            // The friendly hero is not a legal target, so it carries no marker.
            HeroView friendly = nearHero == null ? null : nearHero.GetComponent<HeroView>();
            if (friendly != null)
            {
                friendly.SetTargetable(false);
            }

            MinionView attackerView = attacker.GetComponent<MinionView>();
            if (attackerView != null)
            {
                attackerView.SetSelected(true);
            }

            if (arrow != null && farHero != null)
            {
                arrow.Show(attacker.position, farHero.transform.position + new Vector3(0f, 0.45f, 0f));
            }
        }

        private static void Render(Camera camera, string outputPath, int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 2;

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

            Debug.Log("Preview written to " + outputPath);
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

            // The near side belongs to whoever is acting, so the preview shows a
            // readable hand there and card backs on the far side.
            PopulateHand(anchors.Hand(true), card, 6, 1f, "Test Soldier", "2", "2", "3");
            PopulateHand(anchors.Hand(false), card, 5, 0.55f, null, null, null, null);

            PopulateBoard(anchors.Board(true), minion, 4);
            PopulateBoard(anchors.Board(false), minion, 7);

            PopulateHero("NearHeroView", "PLAYER 1", "30", "deck 22   hand 6", null);
            PopulateHero("FarHeroView", "PLAYER 2", "24", "deck 21   hand 5", "5");

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
            Transform anchor, GameObject prefab, int count, float sideScale,
            string name, string mana, string attack, string health)
        {
            // Same numbers the scene wires into the presenter.
            HandFanSettings settings = new HandFanSettings
            {
                PivotDistance = 7f,
                AnglePerCard = 6.5f,
                MaxSpreadAngle = 38f,
                DepthStep = 0.035f,
                Scale = 0.9f
            };

            for (int index = 0; index < count; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                CardPose pose = HandFanLayout.GetPose(index, count, settings);

                instance.transform.localPosition = pose.LocalPosition;
                instance.transform.localRotation = pose.LocalRotation;
                instance.transform.localScale = Vector3.one * pose.Scale * sideScale;

                bool faceUp = name != null;
                Transform cover = instance.transform.Find("FaceDownCover");
                if (cover != null)
                {
                    cover.gameObject.SetActive(!faceUp);
                }

                if (!faceUp)
                {
                    Hide(instance, "ArtworkArea/Artwork");
                    Hide(instance, "RarityGem");
                    Hide(instance, "ManaGem");
                    Hide(instance, "Statistics");
                    Hide(instance, "TribeBanner");
                    Hide(instance, "NameBanner/NameText");
                    Hide(instance, "RulesBox/RulesText");
                    continue;
                }

                SetText(instance, "NameBanner/NameText", name);
                SetText(instance, "ManaGem/ManaText", mana);
                SetText(instance, "Statistics/AttackGem/AttackText", attack);
                SetText(instance, "Statistics/HealthGem/HealthText", health);
                SetText(instance, "RulesBox/RulesText", string.Empty);

                Hide(instance, "TribeBanner");
            }
        }

        private static void PopulateBoard(Transform anchor, GameObject prefab, int count)
        {
            for (int index = 0; index < count; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
                instance.transform.localPosition = BoardRowLayout.GetPosition(index, count, 1.2f);

                SetText(instance, "NameText", "Test Soldier");
                SetText(instance, "AttackPlate/AttackText", "2");
                SetText(instance, "HealthPlate/HealthText", "3");
            }
        }

        private static void PopulateHero(string objectName, string label, string health, string counters, string armor)
        {
            GameObject hero = GameObject.Find(objectName);
            if (hero == null)
            {
                Debug.LogWarning("No hero view named " + objectName);
                return;
            }

            SetText(hero, "NameText", label);
            SetText(hero, "HealthText", health);
            SetText(hero, "CountersText", counters);

            Transform badge = hero.transform.Find("ArmorBadge");
            if (badge != null)
            {
                badge.gameObject.SetActive(armor != null);

                if (armor != null)
                {
                    SetText(hero, "ArmorBadge/ArmorText", armor);
                }
            }
        }

        private static void Hide(GameObject root, string path)
        {
            Transform found = root.transform.Find(path);
            if (found != null)
            {
                found.gameObject.SetActive(false);
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
