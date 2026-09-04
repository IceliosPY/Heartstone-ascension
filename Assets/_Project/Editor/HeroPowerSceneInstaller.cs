using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CoH.Editor
{
    /// <summary>
    /// Adds the hero power medallion to the match scene, beside the near
    /// hero, and wires it up.
    ///
    /// Additive to the match scene as a whole, but not to any earlier hero
    /// power hierarchy: an older presentation under the same name is torn
    /// down and rebuilt fresh, so re-running this after a visual redesign
    /// actually replaces what is there instead of leaving stale geometry
    /// behind. What it never touches is anything outside that one subtree -
    /// the hand's position in Match.unity was tuned by hand over several
    /// phases and is not what a builder would reproduce, so this finds what
    /// already exists elsewhere and leaves it alone.
    ///
    /// It also means the scene keeps working without it: a match whose
    /// presenter has no hero power view simply shows no hero power, which is
    /// exactly what a hero with no power should look like.
    /// </summary>
    public static class HeroPowerSceneInstaller
    {
        private const string ScenePath = "Assets/_Project/Scenes/Match.unity";
        private const string ViewName = "HeroPower";
        private const string OwnershipRootName = "HeroPowerPresentationRoot";
        private const string CatalogAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualCatalog.asset";
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";

        /// <summary>
        /// Raise's own centre art: the authored claws-and-orb painting. Not a
        /// generic Hero Power frame - it never was one - it is this one hero
        /// power's replaceable artwork, bound below through the same library
        /// every other card's artwork goes through.
        /// </summary>
        private const string RaiseCenterArtPath = "Assets/_Project/Art/HeroPowers/CenterArt/Raise_CenterArt.png";

        /// <summary>
        /// The generic Hero Power frame: a bronze-and-gold ring with a
        /// transparent hole, shared by every hero power. Class-agnostic and
        /// card-agnostic by construction - nothing about it names Raise or
        /// the Necromancer - which is what lets a future hero power reuse it
        /// by simply existing.
        /// </summary>
        private const string FrameAssetPath = "Assets/_Project/Art/HeroPowers/Frames/HeroPower_Frame.png";

        private const string RaiseCardId = "necromancer_choose_your_weapons";

        /// <summary>The real card prefab every hand card and board minion is drawn from.</summary>
        private const string CardPrefabPath = "Assets/_Project/Prefabs/P_Card.prefab";

        [MenuItem("Conquest of Hearthstone/Install Hero Power Into Match Scene")]
        public static void Install()
        {
            EnsureCenterArtImportSettings(RaiseCenterArtPath);
            EnsureSpriteImportSettings(FrameAssetPath);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            MatchHud hud = Object.FindFirstObjectByType<MatchHud>();
            MatchPresenter presenter = Object.FindFirstObjectByType<MatchPresenter>();
            MatchInputController input = Object.FindFirstObjectByType<MatchInputController>();
            Camera camera = Object.FindFirstObjectByType<Camera>();
            CardVisualCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogAsset>(CatalogAssetPath);
            CardVisualLibraryAsset library = AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);
            Sprite raiseCenterArt = AssetDatabase.LoadAssetAtPath<Sprite>(RaiseCenterArtPath);
            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameAssetPath);
            GameObject cardPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            if (hud == null || presenter == null || input == null || camera == null)
            {
                Debug.LogError(
                    "The match scene is missing its HUD, presenter, input controller or camera. " +
                    "Nothing was changed.");

                return;
            }

            if (presenter.DragLayer == null)
            {
                Debug.LogError("The match presenter has no drag layer to anchor the choice cards under. " +
                                "Nothing was changed.");

                return;
            }

            CardView cardPrefab = cardPrefabAsset != null ? cardPrefabAsset.GetComponent<CardView>() : null;

            if (cardPrefab == null)
            {
                Debug.LogError(
                    "No CardView found on " + CardPrefabPath + " - the choice cards need the real card " +
                    "prefab. Nothing was changed.");

                return;
            }

            if (catalog == null)
            {
                Debug.LogWarning(
                    "No card visual catalog at " + CatalogAssetPath + " - the mana gem will fall back to " +
                    "its procedural placeholder. Run Conquest of Hearthstone -> Create Missing Card Visual " +
                    "Assets, then Import HearthCards Components, and install again.");
            }

            if (library == null)
            {
                Debug.LogWarning(
                    "No card visual library at " + LibraryAssetPath + " - the medallion will show no centre " +
                    "art at all.");
            }
            else if (raiseCenterArt != null)
            {
                // Same seam every other card's artwork goes through
                // (CardVisualLibraryAsset.ArtworkFor), so replacing this later
                // is reassigning one binding, not touching this installer, the
                // frame, the gem or HeroPowerView.
                library.Set(RaiseCardId, raiseCenterArt);
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning(
                    "No Raise centre art at " + RaiseCenterArtPath + " - Raise will draw the library's " +
                    "shared placeholder instead.");
            }

            if (frameSprite == null)
            {
                Debug.LogWarning(
                    "No authored frame at " + FrameAssetPath + " - the medallion will fall back to its " +
                    "procedural ring.");
            }

            RemoveInstalledHierarchy(hud.transform, presenter.DragLayer);

            BuiltHierarchy built = Build(
                hud.gameObject, camera, catalog, library, frameSprite, cardPrefab, presenter.DragLayer);

            GameObject ownershipObject = new GameObject(OwnershipRootName, typeof(RectTransform));
            ownershipObject.transform.SetParent(hud.transform, false);
            HeroPowerPresentationRoot ownership = ownershipObject.AddComponent<HeroPowerPresentationRoot>();
            ownership.Bind(
                built.View.gameObject,
                built.Choices,
                built.ChoiceCardAnchor,
                built.ChoiceBackdrop);

            Wire(presenter, "nearHeroPower", built.View);
            Wire(input, "heroPowerView", built.View);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("Hero power medallion installed into " + ScenePath + " and wired to the presenter and input.");
        }

        /// <summary>
        /// Removes the complete hierarchy owned by this installer.
        ///
        /// New installations are tracked by <see cref="HeroPowerPresentationRoot"/>.
        /// The direct-child name sweeps are intentionally limited to the two
        /// known parents and remove orphaned objects produced by older versions
        /// of the installer, before an ownership root existed.
        /// </summary>
        public static void RemoveInstalledHierarchy(Transform hud, Transform dragLayer)
        {
            foreach (HeroPowerPresentationRoot ownership in Object.FindObjectsByType<HeroPowerPresentationRoot>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hud == null || ownership.transform.parent != hud)
                {
                    continue;
                }

                DestroyIfPresent(ownership.HeroPower);
                DestroyIfPresent(ownership.Choices);
                DestroyIfPresent(ownership.ChoiceCardAnchor);
                DestroyIfPresent(ownership.ChoiceBackdrop);
                DestroyIfPresent(ownership.gameObject);
            }

            // Compatibility cleanup for the scene produced before explicit
            // ownership existed. The view itself was removed on every install,
            // but these three referenced siblings were previously left behind.
            foreach (HeroPowerView stale in Object.FindObjectsByType<HeroPowerView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hud != null && stale.transform.parent == hud)
                {
                    DestroyIfPresent(stale.gameObject);
                }
            }

            DestroyDirectChildrenNamed(hud, ViewName);
            DestroyDirectChildrenNamed(hud, "Choices");
            DestroyDirectChildrenNamed(dragLayer, "ChoiceCardAnchor");
            DestroyDirectChildrenNamed(dragLayer, "ChoiceBackdrop");
        }

        private static void DestroyDirectChildrenNamed(Transform parent, string childName)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void DestroyIfPresent(GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Makes sure an authored Hero Power image is set up as a UI sprite,
        /// the same way <c>CardVisualImport</c> configures every imported
        /// HearthCards component - so a re-exported file at this same path is
        /// corrected automatically the next time this runs, rather than
        /// needing the settings redone by hand in the inspector.
        /// </summary>
        private static void EnsureSpriteImportSettings(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Raise's own centre art gets import settings distinct from every
        /// other Hero Power sprite - <see cref="EnsureSpriteImportSettings"/>
        /// stays untouched, so the frame and every imported HearthCards
        /// component keep their existing, shared settings.
        ///
        /// The reason is a specific, measured problem: this painting is
        /// 941x941 and the medallion draws it at roughly a tenth of that, and
        /// bilinear sampling with no mip chain under that much minification
        /// aliases and blurs rather than resolving down cleanly - the exact
        /// artifact a mip chain exists to prevent. None of the project's
        /// other Hero Power or card sprites are minified anywhere near this
        /// hard, which is why this stays local to this one asset rather than
        /// becoming everyone's default.
        /// </summary>
        private static void EnsureCenterArtImportSettings(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (settings.filterMode != FilterMode.Trilinear)
            {
                settings.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            importer.SetTextureSettings(settings);

            TextureImporterPlatformSettings defaultPlatform = importer.GetDefaultPlatformTextureSettings();

            if (defaultPlatform.format != TextureImporterFormat.RGBA32 ||
                defaultPlatform.textureCompression != TextureImporterCompression.Uncompressed)
            {
                defaultPlatform.format = TextureImporterFormat.RGBA32;
                defaultPlatform.textureCompression = TextureImporterCompression.Uncompressed;
                defaultPlatform.overridden = true;
                importer.SetPlatformTextureSettings(defaultPlatform);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// The frame's own hole, measured from the authored 1254x1254 PNG
        /// rather than guessed: scanning its alpha along the horizontal and
        /// vertical centre lines finds the transparent gap between the two
        /// opaque ring segments at roughly 60% and 59% of the canvas
        /// respectively. <see cref="CenterArtMaskSize"/> sits just inside
        /// that (art tucks a little way under the ring's inner lip rather
        /// than stopping short of it, which is what avoids a visible gap),
        /// and is what replaced the earlier value guessed before a real
        /// frame existed to measure.
        /// </summary>
        private const float CenterArtMaskSize = 0.62f;

        /// <summary>
        /// Builds the medallion - centre art, frame, mana gem and cost, in
        /// that back-to-front order - plus the tooltip and the choice menu,
        /// all under the existing HUD canvas.
        ///
        /// A direct child of the canvas, not nested inside a positioning
        /// wrapper: the medallion's own anchor is the point every frame's
        /// world-to-screen tracking writes into, and that math only lines up
        /// when the rect it is writing to is the canvas's immediate child.
        ///
        /// Square: the frame - authored or, failing that,
        /// <see cref="MedallionArt.Ring"/> - is circular either way, and a
        /// circle stretched into a non-square rect draws as an ellipse.
        /// </summary>
        /// <summary>
        /// Between the board's own default sorting order (0 - nothing in the
        /// match scene raises it) and a card's, which is never less than 100
        /// the moment it has any <c>SortingGroup</c> at all (see
        /// <c>CardView.HandBase</c>). Sitting at 50 guarantees the backdrop
        /// draws in front of the hero, the board rows and the tiles, and
        /// behind the choice cards and the player's own hand, without either
        /// number ever having to be read from here to stay correct - only
        /// ever needing to stay between them.
        /// </summary>
        private const int BackdropSortingOrder = 50;

        private static BuiltHierarchy Build(
            GameObject hud, Camera camera, CardVisualCatalogAsset catalog,
            CardVisualLibraryAsset library, Sprite frameSprite, CardView cardPrefab, Transform dragLayer)
        {
            // The previous pass sized the root backwards from a 112px centre
            // art sharpness target, which fixed the blur but landed the
            // whole medallion at ~181px - too large beside the hero. This
            // pass dials the root back down directly to a size that reads as
            // "compact but still comfortably legible" (~140px), and the art
            // still derives from it through the same measured frame
            // proportion (CenterArtMaskSize) so the two never drift apart:
            // 140 * 0.62 = ~87px of centre art, close to the ~86px asked
            // for, and still far enough above the original ~76px that the
            // mip-mapped, uncompressed import settings keep doing their job
            // rather than being undermined by shrinking the target back
            // toward where the blur first showed up.
            const float width = 140f;
            const float height = width;

            // Everything below that isn't the root or the art itself - the
            // gem, its cost text, the tooltip's offset - was tuned relative
            // to a 72px base with a 1.7x multiplier; kept as a ratio against
            // the new root so it grows with the medallion instead of being
            // left behind, too small, beside a suddenly much bigger frame.
            const float layoutScale = width / 72f;

            GameObject root = new GameObject(ViewName, typeof(RectTransform));
            root.transform.SetParent(hud.transform, false);

            RectTransform rootRect = (RectTransform)root.transform;
            Anchor(rootRect, new Vector2(0.5f, 0.5f));
            rootRect.sizeDelta = new Vector2(width, height);

            HeroPowerView view = root.AddComponent<HeroPowerView>();
            Button button = root.AddComponent<Button>();

            // --- centre art, clipped to a circle, behind everything else ------
            GameObject maskObject = new GameObject("CenterArtMask", typeof(RectTransform));
            maskObject.transform.SetParent(root.transform, false);
            RectTransform maskRect = (RectTransform)maskObject.transform;
            Anchor(maskRect, new Vector2(0.5f, 0.5f));
            maskRect.sizeDelta = new Vector2(width, height) * CenterArtMaskSize;

            Image maskGraphic = maskObject.AddComponent<Image>();
            maskGraphic.raycastTarget = false;

            Mask mask = maskObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject artObject = new GameObject("Art", typeof(RectTransform));
            artObject.transform.SetParent(maskObject.transform, false);
            RectTransform artRect = (RectTransform)artObject.transform;
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            Image art = artObject.AddComponent<Image>();

            // --- the frame: our own authored border, over the art -------------
            GameObject frameObject = new GameObject("Frame", typeof(RectTransform));
            frameObject.transform.SetParent(root.transform, false);
            RectTransform frameRect = (RectTransform)frameObject.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            Image frame = frameObject.AddComponent<Image>();

            // --- the mana gem, overlapping the frame's top edge ----------------
            GameObject gemObject = new GameObject("ManaGem", typeof(RectTransform));
            gemObject.transform.SetParent(root.transform, false);
            RectTransform gemRect = (RectTransform)gemObject.transform;
            Anchor(gemRect, new Vector2(0.5f, 1f));
            gemRect.anchoredPosition = new Vector2(0f, 4f * layoutScale);
            gemRect.sizeDelta = new Vector2(28f, 28f) * layoutScale;
            Image gem = gemObject.AddComponent<Image>();
            gem.raycastTarget = false;

            TextMeshProUGUI cost = Label(gemObject, "CostText", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(26f, 26f) * layoutScale, 17f * layoutScale, FontStyles.Bold);

            // --- the tooltip, above the medallion --------------------------------
            GameObject tooltip = new GameObject("Tooltip", typeof(RectTransform));
            tooltip.transform.SetParent(root.transform, false);
            RectTransform tooltipRect = (RectTransform)tooltip.transform;
            Anchor(tooltipRect, new Vector2(0.5f, 1f));
            tooltipRect.pivot = new Vector2(0.5f, 0f);
            tooltipRect.anchoredPosition = new Vector2(0f, 20f * layoutScale);
            tooltipRect.sizeDelta = new Vector2(240f, 92f);

            Image tooltipBackground = tooltip.AddComponent<Image>();
            tooltipBackground.color = new Color(0f, 0f, 0f, 0.88f);
            tooltipBackground.raycastTarget = false;

            TextMeshProUGUI tooltipTitle = Label(tooltip, "Title", new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(220f, 30f), 22f, FontStyles.Bold);

            TextMeshProUGUI tooltipBody = Label(tooltip, "Body", new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(220f, 48f), 18f, FontStyles.Normal);
            tooltipBody.color = new Color(0.85f, 0.85f, 0.9f);

            tooltip.SetActive(false);

            // --- the choice framing: a title and Cancel, screen-fixed -----------
            //
            // Deliberately no full-cover backdrop here any more: a Screen
            // Space - Overlay canvas always draws in front of every
            // world-space object, so a dark panel filling this area would
            // hide the real card views rather than frame them. What is left
            // is placed clear of where the cards actually sit (title above,
            // Cancel below) so neither ever overlaps them.
            GameObject panel = new GameObject("Choices", typeof(RectTransform));
            panel.transform.SetParent(hud.transform, false);

            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Invisible, but still a raycast target: while a choice is open,
            // this keeps a stray click from reaching End Turn or anything
            // else behind it. It has no opinion about the real card views -
            // Physics.Raycast, which is what picks a choice, does not go
            // through uGUI at all.
            Image panelRaycastBlock = panel.AddComponent<Image>();
            panelRaycastBlock.color = Color.clear;

            // -110px of top margin, not the ~-48px of the first pass: that
            // put the title inside the same band the far player's hand
            // occupies, reading as cramped against the top edge. The choice
            // row itself now sits lower on screen too (see HeroPowerView's
            // own choiceViewportY), so there is real clear air between the
            // far hand and the cards for the title to sit in.
            TextMeshProUGUI title = Label(panel, "Title", new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(600f, 60f), 34f, FontStyles.Bold);
            title.text = "Choose a minion";

            GameObject cancelObject = new GameObject("Cancel", typeof(RectTransform));
            cancelObject.transform.SetParent(panel.transform, false);

            RectTransform cancelRect = (RectTransform)cancelObject.transform;
            Anchor(cancelRect, new Vector2(0.5f, 0f));
            cancelRect.anchoredPosition = new Vector2(0f, 60f);
            cancelRect.sizeDelta = new Vector2(220f, 52f);

            Image cancelBackground = cancelObject.AddComponent<Image>();
            cancelBackground.color = new Color(0.28f, 0.24f, 0.30f);

            Button cancel = cancelObject.AddComponent<Button>();

            TextMeshProUGUI cancelLabel = Label(cancelObject, "Label", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(210f, 46f), 22f, FontStyles.Normal);
            cancelLabel.text = "Cancel";

            panel.SetActive(false);

            // --- the choice cards themselves: real CardViews, world-space ------
            //
            // Parented under the presenter's own drag layer rather than the
            // HUD - the same neutral world-space bucket a card drawn out of
            // the deck is briefly placed under - because these are real
            // CardView instances and a Screen Space - Overlay canvas cannot
            // render them at all.
            //
            // Kept at the identity transform, deliberately: this installer no
            // longer bakes in any world position, distance or size for the
            // choice row or its backdrop. That was the actual defect a
            // manual validation pass caught - three retuned numbers later,
            // the row still read as "placed on the board" because it was
            // still, structurally, a fixed world offset from the camera.
            // HeroPowerView now computes every choice card's position and
            // the backdrop's size directly from the live camera each time
            // the menu opens (see LayoutChoiceCards/LayoutChoiceBackdrop),
            // which is what makes the composition centred and
            // resolution-independent rather than merely re-guessed.
            GameObject anchorObject = new GameObject("ChoiceCardAnchor");
            anchorObject.transform.SetParent(dragLayer, false);
            anchorObject.transform.localPosition = Vector3.zero;
            anchorObject.transform.localRotation = Quaternion.identity;
            anchorObject.SetActive(false);

            GameObject backdropObject = BuildChoiceBackdrop(dragLayer);

            Wire(view,
                ("matchCamera", camera), ("catalog", catalog), ("artLibrary", library),
                ("customFrame", frameSprite),
                ("button", button), ("medallionFrame", frame),
                ("centerArtMask", maskGraphic), ("centerArt", art),
                ("manaGem", gem), ("manaCostLabel", cost),
                ("tooltipPanel", tooltip), ("tooltipTitle", tooltipTitle), ("tooltipBody", tooltipBody),
                ("choicePanel", panel), ("choiceCardPrefab", cardPrefab), ("choiceAnchor", anchorObject.transform),
                ("choiceBackdrop", backdropObject), ("cancelButton", cancel));

            return new BuiltHierarchy(view, panel, anchorObject, backdropObject);
        }

        private readonly struct BuiltHierarchy
        {
            public BuiltHierarchy(
                HeroPowerView view,
                GameObject choices,
                GameObject choiceCardAnchor,
                GameObject choiceBackdrop)
            {
                View = view;
                Choices = choices;
                ChoiceCardAnchor = choiceCardAnchor;
                ChoiceBackdrop = choiceBackdrop;
            }

            public HeroPowerView View { get; }
            public GameObject Choices { get; }
            public GameObject ChoiceCardAnchor { get; }
            public GameObject ChoiceBackdrop { get; }
        }

        /// <summary>
        /// A dark, camera-facing quad sized to fully cover the screen at
        /// <see cref="ChoiceBackdropDistance"/>, sitting behind the choice
        /// cards and in front of the board for as long as the menu is open.
        ///
        /// World-space, like the choice cards themselves and for the same
        /// reason: a Screen Space - Overlay canvas always draws in front of
        /// every world-space object, so a uGUI panel here would hide the real
        /// CardViews rather than frame them - which is exactly why the
        /// choice panel's own backdrop was left fully transparent when it
        /// was built. This one lives outside that canvas entirely, ordered by
        /// <see cref="BackdropSortingOrder"/> rather than by the overlay
        /// stack, so it can sit between the board and the cards instead of
        /// in front of both.
        ///
        /// Only the sprite, colour and sorting order are set up here. Its
        /// position, rotation and size are left at the identity - they are
        /// computed fresh from the live camera every time the menu opens
        /// (<see cref="HeroPowerView"/>'s own <c>LayoutChoiceBackdrop</c>),
        /// which is what lets it cover the actual viewport at whatever
        /// resolution the game is running at rather than whatever the editor
        /// happened to be sized to when this installer last ran.
        /// </summary>
        private static GameObject BuildChoiceBackdrop(Transform dragLayer)
        {
            GameObject backdrop = new GameObject("ChoiceBackdrop");
            backdrop.transform.SetParent(dragLayer, false);
            backdrop.transform.localPosition = Vector3.zero;
            backdrop.transform.localRotation = Quaternion.identity;

            SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sprite = MedallionArt.Solid();
            renderer.color = new Color(0f, 0f, 0f, 0.6f);
            renderer.drawMode = SpriteDrawMode.Simple;

            UnityEngine.Rendering.SortingGroup group =
                backdrop.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = BackdropSortingOrder;

            backdrop.SetActive(false);
            return backdrop;
        }

        private static TextMeshProUGUI Label(
            GameObject parent, string name, Vector2 anchor, Vector2 anchoredPosition,
            Vector2 size, float fontSize, FontStyles style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent.transform, false);

            RectTransform rect = (RectTransform)textObject.transform;
            Anchor(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;

            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 pivotAndAnchor)
        {
            rect.anchorMin = pivotAndAnchor;
            rect.anchorMax = pivotAndAnchor;
            rect.pivot = pivotAndAnchor;
        }

        private static void Wire(Object target, string field, Object value) =>
            Wire(target, (field, value));

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
    }
}
