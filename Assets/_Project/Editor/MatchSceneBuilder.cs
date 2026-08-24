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
            TextMeshPro nameText = Text(nameBanner, "NameText", 92f, 572f, 624f, 159f, -0.004f,
                2.6f, TextAlignmentOptions.Center, Color.white);

            // textParchment: x 113, y 718, 580 x 341
            GameObject rulesBox = Group(root, "RulesBox");
            Quad(rulesBox, "Parchment", 113f, 718f, 580f, 341f, -0.003f, "M_CardParchment");
            TextMeshPro rulesText = Text(rulesBox, "RulesText", 150f, 750f, 500f, 210f, -0.004f,
                1.8f, TextAlignmentOptions.Center, new Color(0.12f, 0.09f, 0.06f));

            // manaGem: x 33, y 114, 179 x 181
            GameObject manaGem = Group(root, "ManaGem");
            Quad(manaGem, "Gem", 33f, 114f, 179f, 181f, -0.004f, "M_ManaGem");
            TextMeshPro manaText = Text(manaGem, "ManaText", 33f, 114f, 179f, 181f, -0.005f,
                4.2f, TextAlignmentOptions.Center, Color.white);

            // rarityGem: x 347, y 663, 122 x 92
            Renderer rarityGem = Quad(root, "RarityGem", 347f, 663f, 122f, 92f, -0.004f, "M_RarityGem");

            // attackIcon: x 0, y 893, 222 x 245  /  healthIcon: x 590, y 906, 170 x 231
            GameObject statistics = Group(root, "Statistics");
            GameObject attackGem = Group(statistics, "AttackGem");
            Quad(attackGem, "Gem", 10f, 893f, 200f, 245f, -0.004f, "M_AttackGem");
            TextMeshPro attackText = Text(attackGem, "AttackText", 10f, 893f, 200f, 245f, -0.005f,
                4.2f, TextAlignmentOptions.Center, Color.white);

            GameObject healthGem = Group(statistics, "HealthGem");
            Quad(healthGem, "Gem", 590f, 906f, 170f, 231f, -0.004f, "M_HealthGem");
            TextMeshPro healthText = Text(healthGem, "HealthText", 590f, 906f, 170f, 231f, -0.005f,
                4.2f, TextAlignmentOptions.Center, Color.white);

            // tribePlaque: x 145, y 975, 511 x 97
            GameObject tribeBanner = Group(root, "TribeBanner");
            Quad(tribeBanner, "Plaque", 145f, 975f, 511f, 97f, -0.005f, "M_TribePlaque");
            TextMeshPro tribeText = Text(tribeBanner, "TribeText", 145f, 975f, 511f, 97f, -0.006f,
                1.8f, TextAlignmentOptions.Center, Color.white);

            GameObject faceDown = Group(root, "FaceDownCover");
            Quad(faceDown, "Back", 0f, 0f, CanvasWidth, CanvasHeight, -0.02f, "M_CardBack");

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(CardWidth, CardHeight, 0.05f);
            collider.center = Vector3.zero;
            collider.gameObject.name = root.name;

            Wire(view, nameof(CardView), ("frame", frame), ("artwork", art), ("rarityGem", rarityGem));
            WireObjects(view, ("tribeBanner", tribeBanner), ("statistics", statistics), ("faceDownCover", faceDown));
            WireTexts(view,
                ("nameText", nameText), ("manaText", manaText), ("attackText", attackText),
                ("healthText", healthText), ("rulesText", rulesText), ("tribeText", tribeText));

            return SavePrefab(root, PrefabFolder + "/P_CardPlaceholder.prefab");
        }

        private static GameObject BuildMinionPrefab()
        {
            GameObject root = new GameObject("P_MinionPlaceholder");
            MinionView view = root.AddComponent<MinionView>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.78f, 0.13f, 0.78f);
            body.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = Mat("M_Minion");

            TextMeshPro nameText = WorldText(root, "NameText", new Vector3(0f, 0.30f, -0.50f), 1.5f, Color.white);
            TextMeshPro attackText = WorldText(root, "AttackText", new Vector3(-0.44f, 0.30f, 0.26f), 3f, new Color(1f, 0.85f, 0.3f));
            TextMeshPro healthText = WorldText(root, "HealthText", new Vector3(0.44f, 0.30f, 0.26f), 3f, Color.white);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.9f, 0.5f, 0.9f);
            collider.center = new Vector3(0f, 0.25f, 0f);

            Wire(view, nameof(MinionView), ("body", bodyRenderer));
            WireTexts(view, ("nameText", nameText), ("attackText", attackText), ("healthText", healthText));

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
            // angle is the point: a flat top-down view would read as a
            // spreadsheet, and a low one would hide the far board.
            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cameraObject.AddComponent<AudioListener>();

            cameraObject.transform.position = new Vector3(0f, 7.5f, -6f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            GameObject light = new GameObject("DirectionalLight");
            Light directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.25f;
            directional.color = new Color(1f, 0.97f, 0.9f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // An empty scene carries no lighting settings, and the default
            // ambient washes everything toward the same tint. Setting it flat
            // and neutral is what lets the placeholder palette read as the
            // colours it actually is.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.38f, 0.42f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            // -- World ------------------------------------------------------
            GameObject world = new GameObject("World");

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Board";
            table.transform.SetParent(world.transform, false);
            table.transform.localScale = new Vector3(11f, 0.4f, 7.6f);
            table.transform.localPosition = new Vector3(0f, -0.2f, 0.2f);
            table.GetComponent<Renderer>().sharedMaterial = Mat("M_Board");

            // The two halves are tinted differently so a glance tells you whose
            // side of the table you are looking at.
            Zone(world, "PlayerOneZone", new Vector3(0f, 0.005f, -0.9f), new Vector3(9.6f, 0.02f, 1.9f), "M_ZoneNear");
            Zone(world, "PlayerTwoZone", new Vector3(0f, 0.005f, 1.6f), new Vector3(9.6f, 0.02f, 1.9f), "M_ZoneFar");
            Zone(world, "CentreLine", new Vector3(0f, 0.008f, 0.35f), new Vector3(10.4f, 0.02f, 0.06f), "M_CentreLine");

            GameObject anchorsObject = new GameObject("Anchors");
            anchorsObject.transform.SetParent(world.transform, false);
            BoardAnchors anchors = anchorsObject.AddComponent<BoardAnchors>();

            Transform p1Board = Anchor(anchorsObject, "PlayerOneBoard", new Vector3(0f, 0.02f, -0.9f), Vector3.zero);
            Transform p2Board = Anchor(anchorsObject, "PlayerTwoBoard", new Vector3(0f, 0.02f, 1.6f), Vector3.zero);
            Transform p1Hand = Anchor(anchorsObject, "PlayerOneHand", new Vector3(0f, 0.95f, -3.35f), new Vector3(35f, 0f, 0f));
            // The far hand sits low and just inside the top of the frame: the
            // opponent's cards are shown face down, so they only need to be
            // countable, not readable.
            // Facing the camera like the near hand rather than turned around:
            // a quad has one side, and turning it away simply culls it. The
            // opponent's cards are shown from behind by covering them, not by
            // rotating them.
            Transform p2Hand = Anchor(anchorsObject, "PlayerTwoHand", new Vector3(0f, 0.55f, 3.75f), new Vector3(35f, 0f, 0f));

            // Scaled down at the anchor: the far hand is read as a count, so it
            // gives its room back to the board.
            p2Hand.localScale = Vector3.one * 0.7f;
            Transform p1Hero = Anchor(anchorsObject, "PlayerOneHero", new Vector3(0f, 0.02f, -2.35f), Vector3.zero);
            Transform p2Hero = Anchor(anchorsObject, "PlayerTwoHero", new Vector3(0f, 0.02f, 2.95f), Vector3.zero);

            Wire(anchors, nameof(BoardAnchors),
                ("playerOneHand", p1Hand), ("playerOneBoard", p1Board), ("playerOneHero", p1Hero),
                ("playerTwoHand", p2Hand), ("playerTwoBoard", p2Board), ("playerTwoHero", p2Hero));

            HeroView heroOne = BuildHero(world, "PlayerOneHeroView", p1Hero.position, "M_HeroOne");
            HeroView heroTwo = BuildHero(world, "PlayerTwoHeroView", p2Hero.position, "M_HeroTwo");

            BuildDropZone(world, "PlayerOneDropZone", new Vector3(0f, 0.15f, -0.9f), true);
            BuildDropZone(world, "PlayerTwoDropZone", new Vector3(0f, 0.15f, 1.6f), false);

            // -- HUD --------------------------------------------------------
            MatchHud hud = BuildHud(out GameObject hudObject);

            // -- Systems ----------------------------------------------------
            GameObject systems = new GameObject("Systems");

            GameObject sessionObject = new GameObject("GameSession");
            sessionObject.transform.SetParent(systems.transform, false);
            PresentationQueue queue = sessionObject.AddComponent<PresentationQueue>();
            GameSession session = sessionObject.AddComponent<GameSession>();
            Wire(session, nameof(GameSession), ("queue", queue));

            GameObject presenterObject = new GameObject("MatchPresenter");
            presenterObject.transform.SetParent(systems.transform, false);
            MatchPresenter presenter = presenterObject.AddComponent<MatchPresenter>();
            Wire(presenter, nameof(MatchPresenter),
                ("session", session), ("anchors", anchors), ("hud", hud),
                ("cardPrefab", cardPrefab.GetComponent<CardView>()),
                ("minionPrefab", minionPrefab.GetComponent<MinionView>()),
                ("playerOneHero", heroOne), ("playerTwoHero", heroTwo));

            // Cards are authored one unit wide, then scaled down in hand so a
            // full hand of ten still fits inside the table.
            WireNumbers(presenter,
                ("handLayout.Scale", 0.62f),
                ("handLayout.MaxWidth", 5.4f),
                ("handLayout.PreferredSpacing", 0.72f),
                ("handLayout.SpreadAngle", 14f),
                ("handLayout.ArcDrop", 0.16f),
                ("handLayout.DepthStep", 0.03f),
                ("boardSpacing", 1.05f));

            GameObject inputObject = new GameObject("MatchInput");
            inputObject.transform.SetParent(systems.transform, false);
            MatchInputController input = inputObject.AddComponent<MatchInputController>();
            Wire(input, nameof(MatchInputController),
                ("session", session), ("presenter", presenter), ("hud", hud), ("matchCamera", camera));

            GameObject bootstrapObject = new GameObject("MatchBootstrap");
            bootstrapObject.transform.SetParent(systems.transform, false);
            MatchBootstrap bootstrap = bootstrapObject.AddComponent<MatchBootstrap>();

            CardCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<CardCatalogAsset>(CatalogPath);
            DeckListAsset deck = AssetDatabase.LoadAssetAtPath<DeckListAsset>(DeckPath);

            Wire(bootstrap, nameof(MatchBootstrap),
                ("catalog", catalog), ("playerOneDeck", deck), ("playerTwoDeck", deck),
                ("session", session), ("presenter", presenter));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
        }

        private static HeroView BuildHero(GameObject parent, string name, Vector3 position, string materialName)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent.transform, false);
            root.transform.position = position;

            HeroView view = root.AddComponent<HeroView>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.7f, 0.45f, 1.2f);
            body.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            Renderer renderer = body.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(materialName);

            TextMeshPro nameText = WorldText(root, "NameText", new Vector3(0f, 0.48f, -0.34f), 1.7f, Color.white);
            TextMeshPro healthText = WorldText(root, "HealthText", new Vector3(0.66f, 0.48f, 0.12f), 3.6f, Color.white);

            GameObject armorBadge = new GameObject("ArmorBadge");
            armorBadge.transform.SetParent(root.transform, false);
            TextMeshPro armorText = WorldText(armorBadge, "ArmorText", new Vector3(-0.66f, 0.48f, 0.12f), 3.2f, new Color(0.7f, 0.85f, 1f));
            armorBadge.SetActive(false);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, 0.7f, 1.3f);
            collider.center = new Vector3(0f, 0.35f, 0f);

            Wire(view, nameof(HeroView), ("body", renderer));
            WireObjects(view, ("armorBadge", armorBadge));
            WireTexts(view, ("nameText", nameText), ("healthText", healthText), ("armorText", armorText));

            return view;
        }

        private static void BuildDropZone(GameObject parent, string name, Vector3 position, bool seatOne)
        {
            GameObject zone = new GameObject(name);
            zone.transform.SetParent(parent.transform, false);
            zone.transform.position = position;

            BoxCollider collider = zone.AddComponent<BoxCollider>();
            collider.size = new Vector3(11f, 0.3f, 2.2f);

            BoardDropZone drop = zone.AddComponent<BoardDropZone>();
            drop.SetOwner(seatOne ? Core.Identifiers.PlayerId.One : Core.Identifiers.PlayerId.Two);
        }

        private static MatchHud BuildHud(out GameObject hudObject)
        {
            hudObject = new GameObject("HUD");
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

            TextMeshProUGUI turn = UiText(hudObject, "TurnText", new Vector2(24f, -24f), new Vector2(320f, 44f), 30f, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI active = UiText(hudObject, "ActivePlayerText", new Vector2(24f, -68f), new Vector2(460f, 44f), 30f, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI mana = UiText(hudObject, "ManaText", new Vector2(24f, -112f), new Vector2(360f, 44f), 30f, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI one = UiText(hudObject, "PlayerOneText", new Vector2(24f, -180f), new Vector2(760f, 40f), 26f, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI two = UiText(hudObject, "PlayerTwoText", new Vector2(24f, -218f), new Vector2(760f, 40f), 26f, TextAlignmentOptions.TopLeft);
            TextMeshProUGUI hint = UiText(hudObject, "HintText", new Vector2(24f, -270f), new Vector2(900f, 40f), 26f, TextAlignmentOptions.TopLeft);
            hint.color = new Color(1f, 0.88f, 0.5f);
            TextMeshProUGUI debug = UiText(hudObject, "DebugText", new Vector2(24f, 24f), new Vector2(900f, 34f), 20f, TextAlignmentOptions.BottomLeft);
            debug.color = new Color(0.6f, 0.6f, 0.65f);
            Anchor(debug.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            // End turn button, on the right where a thumb expects it.
            GameObject buttonObject = new GameObject("EndTurnButton", typeof(RectTransform));
            buttonObject.transform.SetParent(hudObject.transform, false);
            RectTransform buttonRect = (RectTransform)buttonObject.transform;
            Anchor(buttonRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            buttonRect.anchoredPosition = new Vector2(-140f, 0f);
            buttonRect.sizeDelta = new Vector2(220f, 88f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.55f, 0.42f, 0.16f);
            Button button = buttonObject.AddComponent<Button>();

            TextMeshProUGUI label = UiText(buttonObject, "Label", Vector2.zero, new Vector2(220f, 88f), 30f, TextAlignmentOptions.Center);
            label.text = "END TURN";
            Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            label.rectTransform.anchoredPosition = Vector2.zero;

            GameObject resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
            resultPanel.transform.SetParent(hudObject.transform, false);
            RectTransform resultRect = (RectTransform)resultPanel.transform;
            Anchor(resultRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            resultRect.sizeDelta = new Vector2(900f, 200f);
            Image resultBackground = resultPanel.AddComponent<Image>();
            resultBackground.color = new Color(0f, 0f, 0f, 0.75f);

            TextMeshProUGUI result = UiText(resultPanel, "ResultText", Vector2.zero, new Vector2(880f, 180f), 72f, TextAlignmentOptions.Center);
            Anchor(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            result.rectTransform.anchoredPosition = Vector2.zero;
            resultPanel.SetActive(false);

            Wire(hud, nameof(MatchHud), ("endTurnButton", button), ("resultPanel", resultPanel));
            WireTexts(hud,
                ("turnText", turn), ("activePlayerText", active), ("manaText", mana),
                ("playerOneText", one), ("playerTwoText", two), ("hintText", hint),
                ("debugText", debug), ("resultText", result));

            return hud;
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Places a quad using HearthCards canvas pixels, with the origin at the
        /// top left of the card, and converts it to local units centred on the
        /// card.
        /// </summary>
        private static Renderer Quad(
            GameObject parent, string name,
            float x, float y, float width, float height, float z,
            string materialName)
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

        private static void Zone(GameObject parent, string name, Vector3 position, Vector3 scale, string materialName)
        {
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = name;
            zone.transform.SetParent(parent.transform, false);
            zone.transform.localPosition = position;
            zone.transform.localScale = scale;
            Object.DestroyImmediate(zone.GetComponent<Collider>());
            zone.GetComponent<Renderer>().sharedMaterial = Mat(materialName);
        }

        private static TextMeshPro Text(
            GameObject parent, string name,
            float x, float y, float width, float height, float z,
            float fontSize, TextAlignmentOptions alignment, Color colour)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent.transform, false);
            textObject.transform.localPosition = ToLocal(x, y, width, height, z);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(
                width / CanvasWidth * CardWidth,
                height / CanvasHeight * CardHeight);

            // Auto-sizing rather than a fixed size: a card is barely half a unit
            // across, and a name has to fit whatever its length.
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = fontSize;
            text.alignment = alignment;
            text.color = colour;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = Vector4.zero;
            text.text = string.Empty;
            return text;
        }

        private static TextMeshPro WorldText(GameObject parent, string name, Vector3 position, float size, Color colour)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent.transform, false);
            textObject.transform.localPosition = position;

            // Tilted to face the fixed camera rather than lying flat on the
            // table, which is what keeps a number on the board readable.
            textObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(1.4f, 0.5f);
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.text = string.Empty;
            return text;
        }

        private static TextMeshProUGUI UiText(
            GameObject parent, string name, Vector2 anchoredPosition, Vector2 size,
            float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent.transform, false);

            RectTransform rect = (RectTransform)textObject.transform;
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
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

        /// <summary>
        /// The placeholder palette.
        ///
        /// Real material assets rather than property blocks set at build time,
        /// because a property block is a runtime-only thing: it is not
        /// serialised into a prefab or a scene, so colours set that way come
        /// back white. Views still tint at runtime through property blocks,
        /// which is exactly what those are for.
        /// </summary>
        private static Material Mat(string name)
        {
            string path = MaterialFolder + "/" + name + ".mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            // Everything is unlit. A placeholder's job is to show exactly the
            // colour it was given so shapes stay legible; making the palette
            // depend on lighting only adds a variable to debug, and Phase 12
            // replaces all of it with real materials anyway.
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
                case "M_Board": return new Color(0.13f, 0.10f, 0.08f);
                case "M_ZoneNear": return new Color(0.21f, 0.18f, 0.13f);
                case "M_ZoneFar": return new Color(0.19f, 0.14f, 0.14f);
                case "M_CentreLine": return new Color(0.55f, 0.44f, 0.25f);

                case "M_CardBody": return new Color(0.07f, 0.06f, 0.05f);
                case "M_CardFrame": return new Color(0.52f, 0.36f, 0.20f);
                case "M_CardArt": return new Color(0.26f, 0.33f, 0.42f);
                case "M_CardBanner": return new Color(0.30f, 0.21f, 0.12f);
                case "M_CardParchment": return new Color(0.87f, 0.81f, 0.67f);
                case "M_CardBack": return new Color(0.17f, 0.14f, 0.30f);

                case "M_ManaGem": return new Color(0.15f, 0.38f, 0.78f);
                case "M_AttackGem": return new Color(0.82f, 0.64f, 0.16f);
                case "M_HealthGem": return new Color(0.74f, 0.16f, 0.16f);
                case "M_RarityGem": return new Color(0.78f, 0.78f, 0.82f);
                case "M_TribePlaque": return new Color(0.34f, 0.25f, 0.15f);

                case "M_Minion": return new Color(0.36f, 0.46f, 0.33f);
                case "M_HeroOne": return new Color(0.24f, 0.31f, 0.50f);
                case "M_HeroTwo": return new Color(0.47f, 0.26f, 0.26f);

                default: return new Color(0.7f, 0.7f, 0.7f);
            }
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

        /// <summary>
        /// Assigns private serialized fields. Confined to this builder: the
        /// fields are private because only the inspector should write them, and
        /// a generated scene is exactly that inspector work done in code.
        /// </summary>
        private static void Wire(Object target, string typeName, params (string Field, Object Value)[] assignments)
        {
            SerializedObject serialized = new SerializedObject(target);

            foreach ((string field, Object value) in assignments)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError(typeName + " has no serialized field named " + field);
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

        private static void WireTexts(Object target, params (string Field, Object Value)[] assignments) =>
            Wire(target, target.GetType().Name, assignments);

        private static void WireObjects(Object target, params (string Field, Object Value)[] assignments) =>
            Wire(target, target.GetType().Name, assignments);
    }
}
