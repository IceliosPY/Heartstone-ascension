using System.IO;
using CoH.App;
using CoH.Data;
using CoH.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoH.Editor
{
    /// <summary>
    /// Builds the match scene and its placeholder prefabs from code.
    ///
    /// The scene is a generated artefact, not something hand-placed, and that is
    /// deliberate: every position below is written down and reproducible, so the
    /// board can be rebuilt after any change instead of being nudged by hand
    /// until it looks right again.
    ///
    /// The screen is laid out as a near side and a far side, never as player one
    /// and player two. In hotseat the person holding the mouse is whoever has
    /// the turn, so the comfortable half of the screen has to follow the turn.
    ///
    /// Card geometry is not invented. The proportions come from measuring how
    /// HearthCards lays a card out on an 800 by 1100 canvas: mana gem top left,
    /// name banner across the middle, rules parchment beneath it, attack and
    /// health in the bottom corners. Only the geometry is reused; every pixel
    /// here is a flat colour we drew ourselves.
    /// </summary>
    public static class MatchSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Match.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string MaterialFolder = "Assets/_Project/Art/Placeholder";

        private const string CatalogPath = "Assets/_Project/Data/Catalog/CardCatalog_Starter.asset";
        private const string DeckPath = "Assets/_Project/Data/Decks/Deck_TestSoldier.asset";

        // The HearthCards canvas, used only as a coordinate system.
        private const float CanvasWidth = 800f;
        private const float CanvasHeight = 1100f;

        // One card is one unit wide, so its height follows the same ratio.
        private const float CardWidth = 1f;
        private const float CardHeight = CardWidth * (CanvasHeight / CanvasWidth);

        // Everything on the table faces the fixed camera at this pitch.
        private const float CameraPitch = 54f;

        // --- The board, front to back ------------------------------------
        private const float NearHandZ = -4.2f;
        private const float NearHandY = 1.15f;
        private const float NearHeroZ = -2.6f;
        private const float NearRowZ = -1.05f;
        private const float CentreZ = 0.25f;
        private const float FarRowZ = 1.55f;
        private const float FarHeroZ = 3.0f;
        private const float FarHandZ = 4.3f;
        private const float FarHandY = 0.55f;

        [MenuItem("Conquest of Hearthstone/Rebuild Match Scene")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            GameObject cardPrefab = BuildCardPrefab();
            GameObject minionPrefab = BuildMinionPrefab();

            BuildScene(cardPrefab, minionPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Match scene rebuilt at " + ScenePath);
        }

        // ------------------------------------------------------------------
        //  Card prefab, laid out on the HearthCards canvas
        // ------------------------------------------------------------------

        private static GameObject BuildCardPrefab()
        {
            GameObject root = new GameObject("P_CardPlaceholder");
            CardView view = root.AddComponent<CardView>();

            // A body slightly larger than the frame, standing in for the drop
            // shadow layer HearthCards draws underneath everything.
            Quad(root, "CardBody", 0f, 0f, CanvasWidth, CanvasHeight, 0f, "M_CardBody");

            // classFrame: x 66, y 92, 669 x 1007
            Renderer frame = Quad(root, "Frame", 66f, 92f, 669f, 1007f, -0.001f, "M_CardFrame");

            // Rectangular art mask: x 186, y 185, 434 x 420
            GameObject artArea = Group(root, "ArtworkArea");
            Renderer art = Quad(artArea, "Artwork", 186f, 185f, 434f, 420f, -0.002f, "M_CardArt");

            // textBanner: x 92, y 572, 624 x 159
            GameObject nameBanner = Group(root, "NameBanner");
            Quad(nameBanner, "BannerPlate", 92f, 572f, 624f, 159f, -0.003f, "M_CardBanner");
            TextMeshPro nameText = Text(nameBanner, "NameText", 110f, 590f, 588f, 122f, -0.004f,
                3.4f, TextAlignmentOptions.Center, Color.white, bold: true);

            // textParchment: x 113, y 718, 580 x 341
            GameObject rulesBox = Group(root, "RulesBox");
            Quad(rulesBox, "Parchment", 113f, 718f, 580f, 341f, -0.003f, "M_CardParchment");
            TextMeshPro rulesText = Text(rulesBox, "RulesText", 150f, 760f, 500f, 200f, -0.004f,
                2.1f, TextAlignmentOptions.Center, new Color(0.12f, 0.09f, 0.06f));

            // manaGem: x 33, y 114, 179 x 181
            GameObject manaGemGroup = Group(root, "ManaGem");
            Renderer manaGem = Quad(manaGemGroup, "Gem", 25f, 106f, 195f, 197f, -0.004f, "M_ManaGem");
            TextMeshPro manaText = Text(manaGemGroup, "ManaText", 25f, 116f, 195f, 177f, -0.005f,
                7.5f, TextAlignmentOptions.Center, Color.white, bold: true);

            // rarityGem: x 347, y 663, 122 x 92
            Renderer rarityGem = Quad(root, "RarityGem", 347f, 663f, 122f, 92f, -0.004f, "M_RarityGem");

            // attackIcon: x 0, y 893, 222 x 245  /  healthIcon: x 590, y 906, 170 x 231
            GameObject statistics = Group(root, "Statistics");

            GameObject attackGem = Group(statistics, "AttackGem");
            Quad(attackGem, "Gem", 8f, 885f, 210f, 215f, -0.004f, "M_AttackGem");
            TextMeshPro attackText = Text(attackGem, "AttackText", 8f, 895f, 210f, 195f, -0.005f,
                7.5f, TextAlignmentOptions.Center, Color.white, bold: true);

            GameObject healthGem = Group(statistics, "HealthGem");
            Quad(healthGem, "Gem", 582f, 885f, 210f, 215f, -0.004f, "M_HealthGem");
            TextMeshPro healthText = Text(healthGem, "HealthText", 582f, 895f, 210f, 195f, -0.005f,
                7.5f, TextAlignmentOptions.Center, Color.white, bold: true);

            // tribePlaque: x 145, y 975, 511 x 97
            GameObject tribeBanner = Group(root, "TribeBanner");
            Quad(tribeBanner, "Plaque", 145f, 975f, 511f, 97f, -0.005f, "M_TribePlaque");
            TextMeshPro tribeText = Text(tribeBanner, "TribeText", 145f, 985f, 511f, 77f, -0.006f,
                2.1f, TextAlignmentOptions.Center, Color.white);

            GameObject faceDown = Group(root, "FaceDownCover");
            Quad(faceDown, "Back", 0f, 0f, CanvasWidth, CanvasHeight, -0.02f, "M_CardBack");
            Quad(faceDown, "BackInlay", 90f, 120f, 620f, 860f, -0.021f, "M_CardBackInlay");

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(CardWidth, CardHeight, 0.06f);

            Wire(view,
                ("frame", frame), ("artwork", art), ("manaGem", manaGem), ("rarityGem", rarityGem),
                ("tribeBanner", tribeBanner), ("statistics", statistics), ("faceDownCover", faceDown),
                ("nameText", nameText), ("manaText", manaText), ("attackText", attackText),
                ("healthText", healthText), ("rulesText", rulesText), ("tribeText", tribeText));

            return SavePrefab(root, PrefabFolder + "/P_CardPlaceholder.prefab");
        }

        // ------------------------------------------------------------------
        //  Minion prefab
        // ------------------------------------------------------------------

        private static GameObject BuildMinionPrefab()
        {
            GameObject root = new GameObject("P_MinionPlaceholder");
            MinionView view = root.AddComponent<MinionView>();

            // Kept inside the 1.2 the row spaces minions by, or seven lit
            // targets read as one long band instead of seven choices.
            GameObject targetRing = Ring(root, "TargetRing", 1.02f, "M_TargetRing", 0.006f);
            GameObject selectionRing = Ring(root, "SelectionRing", 1.1f, "M_SelectionRing", 0.008f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.84f, 0.09f, 0.84f);
            body.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = Mat("M_Minion");

            TextMeshPro nameText = FacingText(root, "NameText",
                new Vector3(0f, 0.22f, -0.52f), new Vector2(1.05f, 0.26f), 1.55f, Color.white);

            // Stat plates sit inside the minion's own footprint, so two
            // neighbours never blend their numbers together.
            GameObject attackGroup = Group(root, "AttackPlate");
            Renderer attackPlate = FacingQuad(attackGroup, "Plate",
                new Vector3(-0.30f, 0.20f, 0.30f), new Vector2(0.34f, 0.34f), "M_AttackGem");
            TextMeshPro attackText = FacingText(attackGroup, "AttackText",
                new Vector3(-0.30f, 0.20f, 0.295f), new Vector2(0.34f, 0.34f), 2.6f, Color.white, bold: true);

            GameObject healthGroup = Group(root, "HealthPlate");
            Renderer healthPlate = FacingQuad(healthGroup, "Plate",
                new Vector3(0.30f, 0.20f, 0.30f), new Vector2(0.34f, 0.34f), "M_HealthGem");
            TextMeshPro healthText = FacingText(healthGroup, "HealthText",
                new Vector3(0.30f, 0.20f, 0.295f), new Vector2(0.34f, 0.34f), 2.6f, Color.white, bold: true);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.95f, 0.5f, 0.95f);
            collider.center = new Vector3(0f, 0.25f, 0f);

            selectionRing.SetActive(false);
            targetRing.SetActive(false);

            Wire(view,
                ("body", bodyRenderer), ("attackPlate", attackPlate), ("healthPlate", healthPlate),
                ("selectionRing", selectionRing), ("targetRing", targetRing),
                ("nameText", nameText), ("attackText", attackText), ("healthText", healthText));

            return SavePrefab(root, PrefabFolder + "/P_MinionPlaceholder.prefab");
        }

        // ------------------------------------------------------------------
        //  Scene
        // ------------------------------------------------------------------

        private static void BuildScene(GameObject cardPrefab, GameObject minionPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // -- Camera -----------------------------------------------------
            // Fixed, looking down the table from behind the near player. The
            // angle is the point: flat on top would read as a spreadsheet, and
            // low would hide the far board.
            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.04f, 0.055f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 9.5f, -7.75f);
            cameraObject.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);

            GameObject light = new GameObject("DirectionalLight");
            Light directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.46f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            // -- World ------------------------------------------------------
            GameObject world = new GameObject("World");

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Board";
            table.transform.SetParent(world.transform, false);
            table.transform.localScale = new Vector3(13f, 0.4f, 8.4f);
            table.transform.localPosition = new Vector3(0f, -0.2f, 0.2f);
            table.GetComponent<Renderer>().sharedMaterial = Mat("M_Board");

            // The two halves are tinted apart so a glance tells you which side
            // of the table you are looking at.
            Slab(world, "NearZone", new Vector3(0f, 0.005f, NearRowZ), new Vector3(10.4f, 0.02f, 2.1f), "M_ZoneNear");
            Slab(world, "FarZone", new Vector3(0f, 0.005f, FarRowZ), new Vector3(10.4f, 0.02f, 2.1f), "M_ZoneFar");
            Slab(world, "CentreLine", new Vector3(0f, 0.01f, CentreZ), new Vector3(11.6f, 0.02f, 0.07f), "M_CentreLine");

            GameObject anchorsObject = new GameObject("Anchors");
            anchorsObject.transform.SetParent(world.transform, false);
            BoardAnchors anchors = anchorsObject.AddComponent<BoardAnchors>();

            Transform nearBoard = Anchor(anchorsObject, "NearBoard", new Vector3(0f, 0.02f, NearRowZ), Vector3.zero);
            Transform farBoard = Anchor(anchorsObject, "FarBoard", new Vector3(0f, 0.02f, FarRowZ), Vector3.zero);
            Transform nearHeroAnchor = Anchor(anchorsObject, "NearHero", new Vector3(0f, 0.02f, NearHeroZ), Vector3.zero);
            Transform farHeroAnchor = Anchor(anchorsObject, "FarHero", new Vector3(0f, 0.02f, FarHeroZ), Vector3.zero);

            // Hands are tilted to square up with the fixed camera.
            Transform nearHand = Anchor(anchorsObject, "NearHand",
                new Vector3(0f, NearHandY, NearHandZ), new Vector3(90f - CameraPitch, 0f, 0f));
            Transform farHand = Anchor(anchorsObject, "FarHand",
                new Vector3(0f, FarHandY, FarHandZ), new Vector3(90f - CameraPitch, 0f, 0f));

            Wire(anchors,
                ("nearHand", nearHand), ("nearBoard", nearBoard), ("nearHero", nearHeroAnchor),
                ("farHand", farHand), ("farBoard", farBoard), ("farHero", farHeroAnchor));

            HeroView nearHero = BuildHero(world, "NearHeroView", nearHeroAnchor.position);
            HeroView farHero = BuildHero(world, "FarHeroView", farHeroAnchor.position);

            BuildDropZone(world, "NearDropZone", new Vector3(0f, 0.2f, NearRowZ), true);
            BuildDropZone(world, "FarDropZone", new Vector3(0f, 0.2f, FarRowZ), false);

            // -- Interaction ------------------------------------------------
            // A card being dragged is parented here rather than left under a
            // hand, so it is not dragged around by the fan re-laying itself out
            // underneath it.
            GameObject dragLayer = new GameObject("DragLayer");
            dragLayer.transform.SetParent(world.transform, false);

            BoardInsertionMarker marker = BuildInsertionMarker(world);
            TargetingArrow arrow = BuildTargetingArrow(world, camera);

            // -- HUD --------------------------------------------------------
            MatchHud hud = BuildHud();

            // -- Systems ----------------------------------------------------
            GameObject systems = new GameObject("Systems");

            GameObject sessionObject = new GameObject("GameSession");
            sessionObject.transform.SetParent(systems.transform, false);
            PresentationQueue queue = sessionObject.AddComponent<PresentationQueue>();
            GameSession session = sessionObject.AddComponent<GameSession>();
            Wire(session, ("queue", queue));

            GameObject presenterObject = new GameObject("MatchPresenter");
            presenterObject.transform.SetParent(systems.transform, false);
            MatchPresenter presenter = presenterObject.AddComponent<MatchPresenter>();
            Wire(presenter,
                ("session", session), ("anchors", anchors), ("hud", hud),
                ("cardPrefab", cardPrefab.GetComponent<CardView>()),
                ("minionPrefab", minionPrefab.GetComponent<MinionView>()),
                ("nearHero", nearHero), ("farHero", farHero),
                ("dragLayer", dragLayer.transform), ("insertionMarker", marker));

            WireNumbers(presenter,
                ("handLayout.PivotDistance", 7f),
                ("handLayout.AnglePerCard", 6.5f),
                ("handLayout.MaxSpreadAngle", 38f),
                ("handLayout.DepthStep", 0.035f),
                ("handLayout.Scale", 0.9f),
                ("boardSpacing", 1.2f),
                ("farHandScale", 0.55f));

            GameObject inputObject = new GameObject("MatchInput");
            inputObject.transform.SetParent(systems.transform, false);
            MatchInputController input = inputObject.AddComponent<MatchInputController>();
            Wire(input,
                ("session", session), ("presenter", presenter), ("hud", hud),
                ("matchCamera", camera), ("targetingArrow", arrow));

            GameObject bootstrapObject = new GameObject("MatchBootstrap");
            bootstrapObject.transform.SetParent(systems.transform, false);
            MatchBootstrap bootstrap = bootstrapObject.AddComponent<MatchBootstrap>();

            Wire(bootstrap,
                ("catalog", AssetDatabase.LoadAssetAtPath<CardCatalogAsset>(CatalogPath)),
                ("playerOneDeck", AssetDatabase.LoadAssetAtPath<DeckListAsset>(DeckPath)),
                ("playerTwoDeck", AssetDatabase.LoadAssetAtPath<DeckListAsset>(DeckPath)),
                ("session", session), ("presenter", presenter));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
        }

        private static HeroView BuildHero(GameObject parent, string name, Vector3 position)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent.transform, false);
            root.transform.position = position;

            HeroView view = root.AddComponent<HeroView>();

            GameObject targetRing = Ring(root, "TargetRing", 3.2f, "M_TargetRing", 0.006f, 1.7f);
            targetRing.SetActive(false);

            GameObject plateObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateObject.name = "Plate";
            plateObject.transform.SetParent(root.transform, false);
            plateObject.transform.localScale = new Vector3(2.9f, 0.22f, 1.35f);
            plateObject.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            Object.DestroyImmediate(plateObject.GetComponent<Collider>());
            Renderer plate = plateObject.GetComponent<Renderer>();
            plate.sharedMaterial = Mat("M_HeroPlate");

            // Everything is arranged across the plate rather than up and down
            // it. Depth is where a hero gets into trouble: the near one has the
            // hand overlapping its front edge and the far one runs off the top
            // of the table, and both problems disappear once the layout only
            // uses width.
            Renderer portrait = FacingQuad(root, "Portrait",
                new Vector3(-0.62f, 0.26f, 0f), new Vector2(0.74f, 0.66f), "M_HeroPortrait");

            TextMeshPro nameText = FacingText(root, "NameText",
                new Vector3(0.24f, 0.26f, -0.18f), new Vector2(0.95f, 0.26f), 1.7f, Color.white, bold: true);

            TextMeshPro countersText = FacingText(root, "CountersText",
                new Vector3(0.24f, 0.26f, 0.20f), new Vector2(1.0f, 0.20f), 1.15f, new Color(0.76f, 0.76f, 0.82f));

            Renderer healthPlate = FacingQuad(root, "HealthPlate",
                new Vector3(1.13f, 0.26f, 0f), new Vector2(0.48f, 0.48f), "M_HealthGem");
            TextMeshPro healthText = FacingText(root, "HealthText",
                new Vector3(1.13f, 0.26f, -0.006f), new Vector2(0.48f, 0.48f), 3.2f, Color.white, bold: true);

            GameObject armorBadge = Group(root, "ArmorBadge");
            Renderer armorPlate = FacingQuad(armorBadge, "ArmorPlate",
                new Vector3(-1.26f, 0.26f, 0f), new Vector2(0.44f, 0.44f), "M_ArmorGem");
            TextMeshPro armorText = FacingText(armorBadge, "ArmorText",
                new Vector3(-1.26f, 0.26f, -0.006f), new Vector2(0.44f, 0.44f), 3f, Color.white, bold: true);
            armorBadge.SetActive(false);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(3.0f, 0.6f, 1.45f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Wire(view,
                ("plate", plate), ("portrait", portrait),
                ("healthPlate", healthPlate), ("armorPlate", armorPlate),
                ("armorBadge", armorBadge), ("targetRing", targetRing),
                ("nameText", nameText), ("healthText", healthText),
                ("armorText", armorText), ("countersText", countersText));

            return view;
        }

        private static void BuildDropZone(GameObject parent, string name, Vector3 position, bool near)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent.transform, false);
            zone.transform.position = position;

            BoxCollider collider = zone.AddComponent<BoxCollider>();
            collider.size = new Vector3(11f, 0.35f, 2.3f);

            zone.AddComponent<BoardDropZone>().SetNearSide(near);
        }

        /// <summary>
        /// The empty slot held open under a card being dragged over the board.
        /// A footprint the size of a minion, lying where that minion will stand.
        /// </summary>
        private static BoardInsertionMarker BuildInsertionMarker(GameObject parent)
        {
            GameObject root = new GameObject("BoardInsertionMarker");
            root.transform.SetParent(parent.transform, false);

            BoardInsertionMarker marker = root.AddComponent<BoardInsertionMarker>();

            GameObject visual = Group(root, "Slot");
            Ring(visual, "Footprint", 1.05f, "M_InsertionSlot", 0.012f);
            FacingQuad(visual, "Riser", new Vector3(0f, 0.34f, 0f), new Vector2(0.86f, 0.62f), "M_InsertionSlot");

            Wire(marker, ("visual", visual));

            visual.SetActive(false);
            return marker;
        }

        /// <summary>
        /// The attack arrow. A world space line so it belongs to the board it is
        /// drawn across, plus a generated triangle for the head.
        /// </summary>
        private static TargetingArrow BuildTargetingArrow(GameObject parent, Camera camera)
        {
            GameObject root = new GameObject("TargetingArrow");
            root.transform.SetParent(parent.transform, false);

            TargetingArrow arrow = root.AddComponent<TargetingArrow>();

            GameObject lineObject = Group(root, "Line");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = Mat("M_TargetArrow");
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            line.enabled = false;

            GameObject headObject = Group(root, "Head");
            MeshFilter headFilter = headObject.AddComponent<MeshFilter>();
            MeshRenderer headRenderer = headObject.AddComponent<MeshRenderer>();
            headRenderer.sharedMaterial = Mat("M_TargetArrow");
            headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            headRenderer.receiveShadows = false;
            headRenderer.enabled = false;

            Wire(arrow,
                ("line", line), ("headFilter", headFilter),
                ("headRenderer", headRenderer), ("matchCamera", camera));

            return arrow;
        }

        private static MatchHud BuildHud()
        {
            GameObject hudObject = new GameObject("HUD");
            Canvas canvas = hudObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = hudObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            hudObject.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem");
                events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            MatchHud hud = hudObject.AddComponent<MatchHud>();

            // --- Player panel: the three things somebody plays from ---------
            GameObject panel = new GameObject("PlayerPanel", typeof(RectTransform));
            panel.transform.SetParent(hudObject.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            Anchor(panelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            panelRect.anchoredPosition = new Vector2(36f, -30f);
            panelRect.sizeDelta = new Vector2(430f, 190f);
            Image panelBackground = panel.AddComponent<Image>();
            panelBackground.color = new Color(0f, 0f, 0f, 0.42f);

            // Everything on the HUD except the button itself is transparent to
            // the pointer. A readout that eats clicks would silently kill a drag
            // that happened to pass under it.
            panelBackground.raycastTarget = false;

            TextMeshProUGUI turn = UiText(panel, "TurnText", new Vector2(22f, -18f), new Vector2(380f, 40f), 30f);
            turn.color = new Color(0.75f, 0.75f, 0.82f);
            TextMeshProUGUI active = UiText(panel, "ActivePlayerText", new Vector2(22f, -58f), new Vector2(390f, 52f), 42f);
            active.fontStyle = FontStyles.Bold;
            TextMeshProUGUI mana = UiText(panel, "ManaText", new Vector2(22f, -116f), new Vector2(390f, 52f), 38f);
            mana.color = new Color(0.55f, 0.78f, 1f);

            // --- Hint, above the near hand ---------------------------------
            GameObject hintObject = new GameObject("HintText", typeof(RectTransform));
            hintObject.transform.SetParent(hudObject.transform, false);
            TextMeshProUGUI hint = hintObject.AddComponent<TextMeshProUGUI>();
            RectTransform hintRect = hint.rectTransform;
            Anchor(hintRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            hintRect.anchoredPosition = new Vector2(0f, 268f);
            hintRect.sizeDelta = new Vector2(900f, 44f);
            hint.fontSize = 28f;
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(1f, 0.86f, 0.48f);
            hint.raycastTarget = false;
            hint.text = string.Empty;

            // --- Developer overlay, small and out of the way ---------------
            GameObject debugObject = new GameObject("DebugText", typeof(RectTransform));
            debugObject.transform.SetParent(hudObject.transform, false);
            TextMeshProUGUI debug = debugObject.AddComponent<TextMeshProUGUI>();
            RectTransform debugRect = debug.rectTransform;
            Anchor(debugRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            debugRect.anchoredPosition = new Vector2(20f, 16f);
            debugRect.sizeDelta = new Vector2(760f, 28f);
            debug.fontSize = 18f;
            debug.alignment = TextAlignmentOptions.BottomLeft;
            debug.color = new Color(0.45f, 0.45f, 0.5f);
            debug.raycastTarget = false;
            debug.text = string.Empty;

            // --- End turn --------------------------------------------------
            GameObject buttonObject = new GameObject("EndTurnButton", typeof(RectTransform));
            buttonObject.transform.SetParent(hudObject.transform, false);
            RectTransform buttonRect = (RectTransform)buttonObject.transform;
            Anchor(buttonRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            buttonRect.anchoredPosition = new Vector2(-160f, -40f);
            buttonRect.sizeDelta = new Vector2(268f, 104f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.60f, 0.45f, 0.17f);
            Button button = buttonObject.AddComponent<Button>();

            TextMeshProUGUI label = UiText(buttonObject, "Label", Vector2.zero, new Vector2(250f, 96f), 26f);
            label.fontStyle = FontStyles.Bold;
            Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "END TURN";

            // --- Result ----------------------------------------------------
            GameObject resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
            resultPanel.transform.SetParent(hudObject.transform, false);
            RectTransform resultRect = (RectTransform)resultPanel.transform;
            Anchor(resultRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            resultRect.sizeDelta = new Vector2(980f, 220f);
            Image resultBackground = resultPanel.AddComponent<Image>();
            resultBackground.color = new Color(0f, 0f, 0f, 0.82f);
            resultBackground.raycastTarget = false;

            TextMeshProUGUI result = UiText(resultPanel, "ResultText", Vector2.zero, new Vector2(950f, 200f), 76f);
            result.fontStyle = FontStyles.Bold;
            Anchor(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            result.rectTransform.anchoredPosition = Vector2.zero;
            result.alignment = TextAlignmentOptions.Center;
            resultPanel.SetActive(false);

            Wire(hud,
                ("turnText", turn), ("activePlayerText", active), ("manaText", mana),
                ("hintText", hint), ("debugText", debug),
                ("endTurnButton", button), ("endTurnLabel", label),
                ("resultPanel", resultPanel), ("resultText", result));

            return hud;
        }

        // ------------------------------------------------------------------
        //  Primitives
        // ------------------------------------------------------------------

        /// <summary>
        /// Places a quad using HearthCards canvas pixels, origin at the top left
        /// of the card, converted to local units centred on the card.
        /// </summary>
        private static Renderer Quad(
            GameObject parent, string name,
            float x, float y, float width, float height, float z, string materialName)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent.transform, false);
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            quad.transform.localPosition = ToLocal(x, y, width, height, z);
            quad.transform.localScale = new Vector3(
                width / CanvasWidth * CardWidth,
                height / CanvasHeight * CardHeight,
                1f);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(materialName);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static TextMeshPro Text(
            GameObject parent, string name,
            float x, float y, float width, float height, float z,
            float fontSize, TextAlignmentOptions alignment, Color colour, bool bold = false)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent.transform, false);
            textObject.transform.localPosition = ToLocal(x, y, width, height, z);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(
                width / CanvasWidth * CardWidth,
                height / CanvasHeight * CardHeight);

            // Auto-sizing rather than a fixed size: a card is under a unit
            // across and a name has to fit whatever its length.
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = fontSize;
            text.alignment = alignment;
            text.color = colour;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = Vector4.zero;
            text.text = string.Empty;
            return text;
        }

        /// <summary>A quad on the table, tilted to square up with the camera.</summary>
        private static Renderer FacingQuad(GameObject parent, string name, Vector3 position, Vector2 size, string materialName)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent.transform, false);
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            quad.transform.localPosition = position;
            quad.transform.localRotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(materialName);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        private static TextMeshPro FacingText(
            GameObject parent, string name, Vector3 position, Vector2 size,
            float fontSize, Color colour, bool bold = false)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent.transform, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.Euler(CameraPitch, 0f, 0f);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.margin = Vector4.zero;
            text.text = string.Empty;
            return text;
        }

        /// <summary>A flat marker lying on the table under a character.</summary>
        private static GameObject Ring(
            GameObject parent, string name, float size, string materialName, float height, float depth = -1f)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = name;
            ring.transform.SetParent(parent.transform, false);
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            ring.transform.localPosition = new Vector3(0f, height, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(size, depth > 0f ? depth : size, 1f);

            Renderer renderer = ring.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(materialName);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ring;
        }

        private static void Slab(GameObject parent, string name, Vector3 position, Vector3 scale, string materialName)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent.transform, false);
            slab.transform.localPosition = position;
            slab.transform.localScale = scale;
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.GetComponent<Renderer>().sharedMaterial = Mat(materialName);
        }

        private static TextMeshProUGUI UiText(
            GameObject parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent.transform, false);

            RectTransform rect = (RectTransform)textObject.transform;
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        private static Vector3 ToLocal(float x, float y, float width, float height, float z)
        {
            float centreX = (x + width * 0.5f) / CanvasWidth;
            float centreY = (y + height * 0.5f) / CanvasHeight;

            return new Vector3(
                (centreX - 0.5f) * CardWidth,
                (0.5f - centreY) * CardHeight,
                z);
        }

        private static GameObject Group(GameObject parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent.transform, false);
            return group;
        }

        private static Transform Anchor(GameObject parent, string name, Vector3 position, Vector3 euler)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent.transform, false);
            anchor.transform.localPosition = position;
            anchor.transform.localRotation = Quaternion.Euler(euler);
            return anchor.transform;
        }

        // ------------------------------------------------------------------
        //  Placeholder palette
        // ------------------------------------------------------------------

        /// <summary>
        /// Real material assets rather than property blocks set at build time,
        /// because a property block is a runtime-only thing: it is not
        /// serialised into a prefab or a scene, so colours set that way come
        /// back white. Views still tint through property blocks at runtime,
        /// which is exactly what those are for.
        ///
        /// Everything is unlit. A placeholder's job is to show exactly the
        /// colour it was given so shapes stay legible; making the palette
        /// depend on lighting only adds a variable to debug.
        /// </summary>
        private static Material Mat(string name)
        {
            string path = MaterialFolder + "/" + name + ".mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");

            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", PaletteOf(name));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Color PaletteOf(string name)
        {
            switch (name)
            {
                case "M_Board": return new Color(0.115f, 0.09f, 0.075f);
                case "M_ZoneNear": return new Color(0.20f, 0.175f, 0.13f);
                case "M_ZoneFar": return new Color(0.19f, 0.135f, 0.135f);
                case "M_CentreLine": return new Color(0.60f, 0.48f, 0.27f);

                case "M_CardBody": return new Color(0.055f, 0.05f, 0.045f);
                case "M_CardFrame": return new Color(0.55f, 0.38f, 0.21f);
                case "M_CardArt": return new Color(0.26f, 0.33f, 0.42f);
                case "M_CardBanner": return new Color(0.28f, 0.19f, 0.11f);
                case "M_CardParchment": return new Color(0.88f, 0.82f, 0.68f);
                case "M_CardBack": return new Color(0.14f, 0.12f, 0.26f);
                case "M_CardBackInlay": return new Color(0.21f, 0.18f, 0.37f);

                case "M_ManaGem": return new Color(0.16f, 0.42f, 0.85f);
                case "M_AttackGem": return new Color(0.82f, 0.64f, 0.16f);
                case "M_HealthGem": return new Color(0.74f, 0.18f, 0.18f);
                case "M_ArmorGem": return new Color(0.36f, 0.55f, 0.80f);
                case "M_RarityGem": return new Color(0.80f, 0.80f, 0.84f);
                case "M_TribePlaque": return new Color(0.32f, 0.24f, 0.14f);

                case "M_Minion": return new Color(0.34f, 0.42f, 0.32f);
                case "M_HeroPlate": return new Color(0.26f, 0.32f, 0.48f);
                case "M_HeroPortrait": return new Color(0.30f, 0.28f, 0.34f);

                case "M_SelectionRing": return new Color(1f, 0.84f, 0.34f);
                case "M_TargetRing": return new Color(0.92f, 0.30f, 0.26f);
                case "M_TargetArrow": return new Color(1f, 0.79f, 0.30f);
                case "M_InsertionSlot": return new Color(0.98f, 0.86f, 0.45f);

                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // ------------------------------------------------------------------
        //  Serialized wiring
        // ------------------------------------------------------------------

        /// <summary>
        /// Assigns private serialized fields. Confined to this builder: the
        /// fields are private because only the inspector should write them, and
        /// a generated scene is exactly that inspector work done in code.
        /// </summary>
        private static void Wire(Object target, params (string Field, Object Value)[] assignments)
        {
            SerializedObject serialized = new SerializedObject(target);

            foreach ((string field, Object value) in assignments)
            {
                SerializedProperty property = serialized.FindProperty(field);

                if (property == null)
                {
                    Debug.LogError(target.GetType().Name + " has no serialized field named " + field);
                    continue;
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns numeric serialized fields, nested paths included.</summary>
        private static void WireNumbers(Object target, params (string Path, float Value)[] assignments)
        {
            SerializedObject serialized = new SerializedObject(target);

            foreach ((string path, float value) in assignments)
            {
                SerializedProperty property = serialized.FindProperty(path);

                if (property == null)
                {
                    Debug.LogError(target.GetType().Name + " has no serialized field at " + path);
                    continue;
                }

                property.floatValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void RegisterInBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            foreach (EditorBuildSettingsScene entry in existing)
            {
                if (entry.path == ScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
