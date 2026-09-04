using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation.CardVisuals;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoH.Presentation
{
    /// <summary>
    /// A hero power medallion attached beside its owner's hero, and the menu of
    /// fixed choices it opens.
    ///
    /// It decides nothing. Whether the power may be used is
    /// <see cref="GameSession.CanUseHeroPower"/>, asked every refresh; what the
    /// choices are is <see cref="GameSession.GetHeroPowerOptions"/>, read out of
    /// the card. This view counts no mana, remembers no "already used" flag and
    /// knows nothing about any class - which is why a hero power with two
    /// options, or six, needs no change here, and why a second hero with its
    /// own power needs nothing more than a second instance of this component
    /// bound to a second hero's transform.
    ///
    /// Opening the menu commits nothing. No command is sent until a choice is
    /// clicked, so closing it again is a complete cancellation with nothing to
    /// undo.
    ///
    /// The medallion is a screen-space UI element that tracks a world-space
    /// hero's position every refresh, rather than a 3D object of its own. That
    /// keeps it clickable through the same ordinary uGUI Button already used
    /// for the end-turn button and the choice menu, instead of teaching the
    /// board's 3D pointer probe a new kind of thing to hit.
    ///
    /// The medallion itself is composed from three independent visual layers
    /// rather than one flattened picture:
    ///
    ///     CenterArt  - behind everything, clipped to a circle, replaceable
    ///                  per hero power through the same CardId-keyed binding
    ///                  every other card's artwork goes through
    ///                  (<see cref="CardVisualLibraryAsset"/>). Raise's own
    ///                  entry points at the authored claws-and-orb painting;
    ///                  a different hero power with no entry draws the
    ///                  library's shared placeholder instead, the same as an
    ///                  unbound minion would.
    ///     Frame      - drawn over the art, defining the medallion's outer
    ///                  silhouette: the authored bronze-and-gold ring
    ///                  (<see cref="customFrame"/>) today, or the generic
    ///                  procedural ring from <see cref="MedallionArt"/> if
    ///                  that reference is ever unset. Either way it is
    ///                  shared by every hero power - nothing here is keyed
    ///                  to a class or a card id, which is what makes it
    ///                  reusable by whichever hero power comes next.
    ///     ManaGem     - the shared mana gem every other card already uses,
    ///                  positioned here by this view's own layout rather than
    ///                  a card's, with its own cost text on top.
    ///
    /// Changing what art a hero power shows never touches the frame, and
    /// changing the frame never touches the gem: three sprites, three
    /// SerializeField references, no code path connecting one to another.
    ///
    /// The four choices, once the power is activated, are real
    /// <see cref="CardView"/> instances - the same prefab and the same
    /// <see cref="CardVisualFactory"/> pipeline a hand card is drawn by, bound
    /// with <see cref="EntityId.None"/> because none of them is a card in
    /// anyone's hand. That is also what keeps them out of the board's own
    /// pointer probe automatically: it only ever calls a raycast hit a hand
    /// card when the card behind it has a real id. Choosing one submits the
    /// same <c>UseHeroPowerCommand</c> a hand-drawn placeholder would have;
    /// nothing about the command changed, only what the option looks like.
    /// </summary>
    public sealed class HeroPowerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visual source")]
        [Tooltip("Where the shared mana gem comes from. The same catalog every card's own gem is resolved from.")]
        [SerializeField] private CardVisualCatalogAsset catalog;

        [Tooltip("Where a hero power's own centre art comes from, by CardId - the same binding every card's artwork uses.")]
        [SerializeField] private CardVisualLibraryAsset artLibrary;

        [Header("Frame")]
        [Tooltip("Our own authored medallion border. Used as-is; MedallionArt.Ring() is only a last-resort stand-in when this is unset.")]
        [SerializeField] private Sprite customFrame;

        [SerializeField] private Button button;
        [SerializeField] private Image medallionFrame;

        [Header("Centre art")]
        [Tooltip("The invisible circular stencil that keeps art from bleeding past the frame's opening.")]
        [SerializeField] private Image centerArtMask;

        [Tooltip("The replaceable artwork itself, inside the mask.")]
        [SerializeField] private Image centerArt;

        [Header("Mana gem")]
        [SerializeField] private Image manaGem;
        [SerializeField] private TextMeshProUGUI manaCostLabel;

        [Header("Tooltip")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI tooltipTitle;
        [SerializeField] private TextMeshProUGUI tooltipBody;

        [Header("Choices")]
        [Tooltip("The uGUI framing (title, Cancel) shown while choosing. Hidden until the power is activated.")]
        [SerializeField] private GameObject choicePanel;

        [Tooltip(
            "Real card, same prefab every hand card uses. Instantiated once per option and reused - " +
            "this is what makes a choice a real card rather than a hand-drawn stand-in for one.")]
        [SerializeField] private CardView choiceCardPrefab;

        [Tooltip(
            "World-space parent the choice cards are laid out under. Kept at the identity transform " +
            "always - not part of the HUD canvas, since a Screen Space - Overlay canvas always draws " +
            "in front of world-space cards, so the choices have to live outside it to be visible at " +
            "all - and never moved itself, since every card's own position is now computed directly " +
            "in world space and handed to it as a resting pose. Nothing here anchors the choices to " +
            "the board.")]
        [SerializeField] private Transform choiceAnchor;

        [Tooltip(
            "A world-space quad, dark and semi-transparent, shown behind the choice cards and in " +
            "front of the board while choosing - what gives the four cards visual priority over " +
            "everything else on the table. Instantiated by the installer; sized and placed by this " +
            "view every time the menu opens, against whatever the camera's aspect and field of view " +
            "actually are.")]
        [SerializeField] private GameObject choiceBackdrop;

        [SerializeField] private Button cancelButton;

        [Header("Choice presentation")]
        [Tooltip(
            "How far in front of the camera the choice row and its backdrop sit, along the camera's " +
            "own forward axis. Free to pick within the camera's near/far planes: a card's on-screen " +
            "size and position are computed from viewport fractions, which already correct for " +
            "distance, so changing this alone does not change how big or where the cards read on " +
            "screen - only how far away the underlying quad physically is.")]
        [SerializeField] private float presentationPlaneDistance = 8f;

        [Tooltip(
            "Viewport-space vertical position (0 = bottom, 1 = top) of every choice card's centre. " +
            "Above the board's own centre so the row reads as floating over the table rather than " +
            "sitting on it, and above the near hero's own screen position so the two are never asked " +
            "to share the same space.")]
        [SerializeField] private float choiceViewportY = 0.65f;

        [Tooltip(
            "The visible gap left between two neighbouring cards' own edges, as a viewport fraction - " +
            "not the centre-to-centre spacing itself. That spacing is computed every layout pass as " +
            "each card's own current viewport width (which changes with the Game view's aspect ratio) " +
            "plus this gap, which is what keeps the gap looking the same size relative to the cards at " +
            "any window shape - a fixed centre-to-centre fraction does not: at a wider-than-16:9 Game " +
            "view the same fixed spacing left cards visibly narrower but the same distance apart, which " +
            "read as far more spread out than the identical layout at 16:9. Deliberately not " +
            "constrained by End Turn's own screen position: the choice row is allowed to sit in front " +
            "of it (see MatchHud.SetEndTurnModalDimmed), since End Turn has nothing to do while a " +
            "choice is open regardless of what is drawn over it.")]
        [SerializeField] private float choiceCardGap = 0.02f;

        [Tooltip(
            "How tall a choice card reads on screen, as a fraction of the viewport's own height - " +
            "sized to read close to a full, prominent choice presentation (comparable to a reference " +
            "Hearthstone choice screen) now that End Turn is no longer what caps how big the group can " +
            "get: the two remaining limits are the screen's own safe margins and each card's neighbours.")]
        [SerializeField] private float choiceCardViewportHeight = 0.4f;

        [Tooltip("How much larger than the exact camera frustum the backdrop is drawn, so it still fully covers a wider-than-reference window.")]
        [SerializeField] private float backdropCoverageMargin = 1.5f;

        [Header("Anchoring")]
        [Tooltip("Camera the board is rendered through. Falls back to Camera.main.")]
        [SerializeField] private Camera matchCamera;

        [Tooltip("Offset from the hero's own position, in world units, that reads as beside it.")]
        [SerializeField] private Vector3 heroRelativeOffset = new Vector3(1.55f, 0.30f, 0f);

        [Header("Palette")]
        [Tooltip(
            "White leaves an authored frame's own colours untouched. Only set this away from white to " +
            "give presence to the plain procedural ring when no authored frame is assigned.")]
        [SerializeField] private Color frameAvailableColor = Color.white;
        [SerializeField] private Color frameUnavailableColor = new Color(0.55f, 0.55f, 0.55f, 0.72f);
        [SerializeField] private Color artAvailableColor = Color.white;
        [SerializeField] private Color artUnavailableColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        [SerializeField] private Color manaAvailableColor = Color.white;
        [SerializeField] private Color manaUnavailableColor = new Color(0.55f, 0.55f, 0.55f, 0.72f);

        private readonly List<CardView> _choiceCards = new List<CardView>();
        private int _activeChoiceCount;

        private RectTransform _rectTransform;
        private RectTransform _canvasRect;
        private Transform _heroAnchor;
        private CardDefinition _definition;
        private bool _frameIsCustom;
        private bool _manaGemIsResolved;

        /// <summary>The player asked to open the choice menu.</summary>
        public event Action ActivationRequested;

        /// <summary>The player picked an option, by its index in the authored list.</summary>
        public event Action<int> OptionChosen;

        /// <summary>Whether the choice menu is open. Purely a view state.</summary>
        public bool IsChoosing { get; private set; }

        /// <summary>Whether the power was usable at the last refresh.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>Which player this view is currently showing.</summary>
        public PlayerId PlayerId { get; private set; }

        /// <summary>Whether the hover tooltip is currently on screen.</summary>
        public bool IsShowingTooltip => tooltipPanel != null && tooltipPanel.activeSelf;

        /// <summary>The tooltip's title line, read back for tests rather than duplicated by them.</summary>
        public string TooltipTitle => tooltipTitle != null ? tooltipTitle.text : string.Empty;

        /// <summary>The tooltip's body text, read back for tests rather than duplicated by them.</summary>
        public string TooltipBody => tooltipBody != null ? tooltipBody.text : string.Empty;

        /// <summary>Whether the medallion is showing our own authored frame rather than the procedural stand-in.</summary>
        public bool IsShowingCustomFrame => _frameIsCustom;

        /// <summary>Whether the mana gem resolved to the shared catalog sprite rather than the procedural stand-in.</summary>
        public bool IsShowingCatalogManaGem => _manaGemIsResolved;

        /// <summary>The sprite currently behind the frame, or null when the bound card has none. Read back for tests.</summary>
        public Sprite CenterArtSprite => centerArt != null ? centerArt.sprite : null;

        /// <summary>The sprite currently drawn as the frame. Read back for tests.</summary>
        public Sprite FrameSprite => medallionFrame != null ? medallionFrame.sprite : null;

        /// <summary>The sprite currently drawn as the mana gem. Read back for tests.</summary>
        public Sprite ManaGemSprite => manaGem != null ? manaGem.sprite : null;

        /// <summary>
        /// The real card views currently standing in for the four choices,
        /// in option order. Read back for tests rather than duplicated by
        /// them; a card not currently offered is inactive but still present.
        /// </summary>
        public IReadOnlyList<CardView> ChoiceCards => _choiceCards;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _canvasRect = transform.parent as RectTransform;

            if (matchCamera == null)
            {
                matchCamera = Camera.main;
            }

            ResolveFrame();
            ResolveManaGem();

            if (centerArtMask != null)
            {
                centerArtMask.sprite = MedallionArt.Disc();
                centerArtMask.raycastTarget = false;
            }

            if (centerArt != null)
            {
                centerArt.raycastTarget = false;
            }

            if (button != null)
            {
                // The clickable area is the frame's whole rectangle rather
                // than its exact transparent pixels: an imported texture is
                // not readable by default, and alpha hit-testing on one
                // throws rather than degrading - the same failure this
                // project already hit and fixed once. A slightly generous
                // circular button beside the hero costs nothing here.
                button.targetGraphic = medallionFrame;
                button.onClick.AddListener(() => ActivationRequested?.Invoke());
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(CloseChoices);
            }

            CloseChoices();
            HideTooltip();
        }

        /// <summary>
        /// Puts the authored bronze-and-gold ring on the medallion, or the
        /// generic procedural stand-in on the rare path where no frame
        /// sprite is assigned at all (an unconfigured scene, a test that
        /// never wired one). Nothing here is keyed to a class or a card id
        /// either way, which is what lets a future hero power reuse whichever
        /// of the two is showing by simply existing.
        /// </summary>
        private void ResolveFrame()
        {
            _frameIsCustom = customFrame != null;

            if (medallionFrame != null)
            {
                medallionFrame.sprite = customFrame != null ? customFrame : MedallionArt.Ring();
            }
        }

        /// <summary>
        /// Puts the shared mana gem - the same one every other card's own
        /// mana cost is drawn with - behind this view's own cost text.
        ///
        /// Resolved through the catalog rather than a direct reference so
        /// that if the gem art ever changes, this follows without being
        /// touched; positioned and sized by this view's own layout rather
        /// than a card's, per the audit this task asked for.
        /// </summary>
        private void ResolveManaGem()
        {
            CardVisualResolution gem = catalog != null
                ? catalog.Resolve(
                    CardVisualSlot.ManaGem, new CardVisualDescriptor(CardType.HeroPower, CardClass.Neutral))
                : CardVisualResolution.Missing;

            _manaGemIsResolved = gem.Found;

            if (manaGem != null)
            {
                manaGem.sprite = gem.Found ? gem.Sprite : MedallionArt.Disc();
            }
        }

        /// <summary>
        /// Puts this card's own art behind the frame, through the same
        /// CardId-keyed binding every other card's artwork is resolved from.
        ///
        /// A card with nothing bound draws the library's shared placeholder,
        /// the same as an unbound minion or spell would - never a hole, never
        /// an exception, and never a filename decided by this file.
        /// </summary>
        private void ResolveCenterArt(CardId cardId)
        {
            if (centerArt == null)
            {
                return;
            }

            centerArt.sprite = artLibrary != null ? artLibrary.ArtworkFor(cardId) : null;
            centerArt.enabled = centerArt.sprite != null;
        }

        /// <summary>
        /// Shows this player's hero power beside the given hero, or hides the
        /// whole thing when they have none.
        ///
        /// Called on every rebuild, because in hotseat the near seat changes
        /// hands and a hero power belongs to a player rather than to a side of
        /// the table; <paramref name="heroAnchor"/> is whichever hero transform
        /// currently sits on that side, so the medallion always ends up beside
        /// whoever it was just bound to.
        /// </summary>
        public void Bind(GameSession session, Player player, Transform heroAnchor)
        {
            PlayerId = player.Id;
            _heroAnchor = heroAnchor;

            Hero hero = player.Hero;

            if (session == null || !session.IsReady || !hero.HasHeroPower ||
                !session.State.Catalog.TryGet(hero.HeroPowerCardId, out CardDefinition definition))
            {
                _definition = null;
                Hide();
                return;
            }

            _definition = definition;
            gameObject.SetActive(true);

            ResolveCenterArt(definition.Id);

            SetText(manaCostLabel, definition.ManaCost.ToString());
            SetText(tooltipTitle, definition.Name);
            SetText(tooltipBody, definition.ManaCost + " Mana\n" + definition.Text);

            BuildChoicesOnActiveHierarchy(session, definition);
            TrackHeroPosition();
        }

        /// <summary>
        /// Composes the four choice cards with the world-space anchor forced
        /// active for the duration, then leaves it exactly how it found it.
        ///
        /// This exists because of a real, measured bug: <see cref="BuildChoices"/>
        /// binds real <see cref="CardView"/> instances, and a bound CardView's
        /// TextMeshPro layers auto-size themselves - a computation that only
        /// converges correctly while the whole GameObject hierarchy is
        /// active. The choice anchor's normal resting state is inactive
        /// (nothing is shown until Raise is clicked), and the very first
        /// composition used to happen right there, on an inactive hierarchy,
        /// at the moment the hero power itself is bound - long before anyone
        /// had opened the menu. The result was not "deferred until shown", it
        /// was silently wrong: fonts pinned near their uncapped maximum
        /// rather than fitted to their slot, which is exactly the catastrophic
        /// oversized text a manual validation pass caught. Composing here,
        /// with the anchor forced active just long enough to let TextMeshPro
        /// actually settle, is what fixes it - not any font, recipe or style
        /// value, none of which were ever wrong.
        /// </summary>
        private void BuildChoicesOnActiveHierarchy(GameSession session, CardDefinition definition)
        {
            bool wasActive = choiceAnchor != null && choiceAnchor.gameObject.activeSelf;

            if (choiceAnchor != null && !wasActive)
            {
                choiceAnchor.gameObject.SetActive(true);
            }

            BuildChoices(session, definition);

            if (choiceAnchor != null && !wasActive)
            {
                choiceAnchor.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Lights the medallion according to the engine's answer, and keeps it
        /// glued to its hero. Nothing else.
        /// </summary>
        public void Refresh(GameSession session, bool interactionAllowed)
        {
            if (session == null || !session.IsReady || PlayerId.IsNone)
            {
                return;
            }

            IsAvailable = session.CanUseHeroPower(PlayerId) == RejectionReason.None;

            bool usable = IsAvailable && interactionAllowed;

            if (button != null)
            {
                button.interactable = usable;
            }

            ApplyAvailabilityVisuals(usable);

            // A menu left open while the power stopped being usable - the queue
            // started replaying, or the turn ended under it - is closed rather
            // than left offering choices that would now be refused.
            if (IsChoosing && !usable)
            {
                CloseChoices();
            }

            TrackHeroPosition();
            UpdateChoiceInteraction();
        }

        /// <summary>
        /// Hover and click for the four choice cards, done as this view's own
        /// dedicated raycast rather than through the board's pointer probe.
        ///
        /// The probe already refuses to treat these as hand cards - each one
        /// is bound with <see cref="EntityId.None"/>, and the probe only ever
        /// calls a card a <c>HandCard</c> when it has a real id - but it has
        /// no reason to know about a hero power's choice cards either, so
        /// this asks its own question instead of teaching the shared probe a
        /// third kind of card to recognise.
        /// </summary>
        private void UpdateChoiceInteraction()
        {
            if (!IsChoosing || matchCamera == null || _choiceCards.Count == 0)
            {
                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            Ray ray = matchCamera.ScreenPointToRay(mouse.position.ReadValue());
            ApplyChoicePointer(ray, mouse.leftButton.wasPressedThisFrame);
        }

        /// <summary>
        /// The actual hover/click logic, taking a ray rather than reading the
        /// mouse itself - the same shape <c>MatchInputController</c> already
        /// uses for its own pointer handling, so a test can drive a click
        /// with a constructed ray exactly the way it already drives one on
        /// the board, instead of simulating an Input System device.
        /// </summary>
        internal void ApplyChoicePointer(Ray ray, bool clicked)
        {
            int hovered = ClosestChoiceUnderRay(ray);

            for (int index = 0; index < _choiceCards.Count; index++)
            {
                if (_choiceCards[index] != null && _choiceCards[index].gameObject.activeSelf)
                {
                    _choiceCards[index].SetHovered(index == hovered);
                }
            }

            if (hovered >= 0 && clicked)
            {
                CloseChoices();
                OptionChosen?.Invoke(hovered);
            }
        }

        /// <summary>
        /// Which choice card, if any, the ray meets first - checked against
        /// every hit along the ray rather than only the nearest one, so a
        /// choice card sitting behind something else the ray also happens to
        /// cross is never missed.
        /// </summary>
        private int ClosestChoiceUnderRay(Ray ray)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

            int closestIndex = -1;
            float closestDistance = float.MaxValue;

            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                Collider collider = hits[hitIndex].collider;

                if (collider == null)
                {
                    continue;
                }

                CardView hitView = collider.GetComponentInParent<CardView>();

                if (hitView == null)
                {
                    continue;
                }

                int optionIndex = _choiceCards.IndexOf(hitView);

                if (optionIndex < 0 || !_choiceCards[optionIndex].gameObject.activeSelf)
                {
                    continue;
                }

                if (hits[hitIndex].distance < closestDistance)
                {
                    closestDistance = hits[hitIndex].distance;
                    closestIndex = optionIndex;
                }
            }

            return closestIndex;
        }

        /// <summary>
        /// Opens the menu. Commits nothing.
        ///
        /// The medallion itself is hidden for as long as the menu is open - a
        /// real, measured overlap, not a guess: it is a screen-space element
        /// tracking the hero's own position, and that position sits, on
        /// screen, right among the four choice cards it is supposed to be
        /// offering. Hiding it is one of this project's own established
        /// options for that ("masqué temporairement pendant que le menu de
        /// choix est ouvert"), and the simplest one that cannot leave a stray
        /// frame or gem drawn over a card. Nothing here stops
        /// <see cref="Refresh"/> from still running against it - that is an
        /// ordinary method call from <c>MatchInputController</c>, not a Unity
        /// message, so it keeps working on a hidden GameObject exactly as it
        /// would on a shown one.
        /// </summary>
        public void OpenChoices()
        {
            if (_choiceCards.Count == 0)
            {
                return;
            }

            IsChoosing = true;
            HideTooltip();

            if (choicePanel != null)
            {
                choicePanel.SetActive(true);
            }

            if (choiceAnchor != null)
            {
                choiceAnchor.gameObject.SetActive(true);
            }

            // Recomputed here, not only once at bind time: this is a real
            // window's actual aspect and field of view at the moment the
            // player opens the menu, not whatever they happened to be when
            // the match started. Cheap either way - a handful of trig calls
            // - so there is no reason to trust a cached answer over asking
            // again.
            LayoutChoiceCards(_activeChoiceCount);
            LayoutChoiceBackdrop();

            if (choiceBackdrop != null)
            {
                choiceBackdrop.SetActive(true);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Closes the menu without choosing. This is the whole of cancellation:
        /// nothing was sent, so nothing has to be taken back.
        /// </summary>
        public void CloseChoices()
        {
            IsChoosing = false;

            gameObject.SetActive(true);

            if (choicePanel != null)
            {
                choicePanel.SetActive(false);
            }

            if (choiceAnchor != null)
            {
                choiceAnchor.gameObject.SetActive(false);
            }

            if (choiceBackdrop != null)
            {
                choiceBackdrop.SetActive(false);
            }

            for (int index = 0; index < _choiceCards.Count; index++)
            {
                if (_choiceCards[index] != null)
                {
                    _choiceCards[index].SetHovered(false);
                }
            }
        }

        /// <summary>The tooltip: name, cost and rules text, read straight off the card.</summary>
        public void ShowTooltip()
        {
            if (IsChoosing || _definition == null)
            {
                return;
            }

            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);
            }
        }

        public void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData) => ShowTooltip();

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData) => HideTooltip();

        private void Hide()
        {
            CloseChoices();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Recomputes the medallion's screen position from the hero it is
        /// bound to.
        ///
        /// The camera is fixed and the hero rarely moves, but this runs every
        /// refresh anyway - called once per real frame already, for the
        /// availability check - so the medallion stays glued to its hero
        /// through its hit-recoil wobble too, at no meaningful cost.
        /// </summary>
        private void TrackHeroPosition()
        {
            if (_heroAnchor == null || matchCamera == null || _canvasRect == null)
            {
                return;
            }

            Vector3 worldPoint = _heroAnchor.position + heroRelativeOffset;
            Vector3 screenPoint = matchCamera.WorldToScreenPoint(worldPoint);

            if (screenPoint.z < 0f)
            {
                // Behind the camera. Leave the medallion where it last was
                // rather than snapping it to a meaningless projection.
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screenPoint, null, out Vector2 local))
            {
                _rectTransform.anchoredPosition = local;
            }
        }

        /// <summary>
        /// Dims the three layers together without touching what any of them
        /// currently show. Available leaves the frame's own colours alone
        /// entirely - multiplying real art by a tint is how art gets ruined -
        /// and only the unavailable state desaturates it.
        /// </summary>
        private void ApplyAvailabilityVisuals(bool usable)
        {
            if (medallionFrame != null)
            {
                medallionFrame.color = usable ? frameAvailableColor : frameUnavailableColor;
            }

            if (centerArt != null)
            {
                centerArt.color = usable ? artAvailableColor : artUnavailableColor;
            }

            if (manaGem != null)
            {
                manaGem.color = usable ? manaAvailableColor : manaUnavailableColor;
            }
        }

        /// <summary>
        /// Fills each choice with the card the option actually summons,
        /// through the same real card pipeline every hand card and board
        /// minion is drawn by.
        ///
        /// A choice card is bound with <see cref="EntityId.None"/>, because
        /// none of these four is a card in anyone's hand - it is a preview of
        /// what Raise would put on the board, not a card that can be played,
        /// dragged, or that costs its own printed mana to show. Binding it
        /// still resolves the Necromancer's own class frame automatically:
        /// nothing here names that frame, the same way nothing here names an
        /// artwork or a rules panel. Read out of the catalog rather than
        /// authored here, so a servant whose statistics change is described
        /// correctly without this file being opened.
        /// </summary>
        private void BuildChoices(GameSession session, CardDefinition definition)
        {
            IReadOnlyList<EffectDefinition> options = session.GetHeroPowerOptions(PlayerId);

            _activeChoiceCount = options.Count;

            EnsureChoiceCards(options.Count);
            LayoutChoiceCards(options.Count);

            for (int index = 0; index < _choiceCards.Count; index++)
            {
                bool used = index < options.Count;
                CardView view = _choiceCards[index];

                view.gameObject.SetActive(used);

                if (!used)
                {
                    continue;
                }

                CardId summoned = options[index].Action.SummonCardId;

                if (!summoned.IsNone && session.State.Catalog.TryGet(summoned, out CardDefinition card))
                {
                    view.Bind(new CardViewModel(
                        EntityId.None, card.Id, card.Name, card.ManaCost, card.Attack, card.Health,
                        card.Text, card.Type, card.Class, card.Tribe, card.Rarity, isPlayable: true));
                }
            }

            SynchronizeChoiceTypography(options.Count);
        }

        private void EnsureChoiceCards(int count)
        {
            if (choiceCardPrefab == null || choiceAnchor == null)
            {
                return;
            }

            while (_choiceCards.Count < count)
            {
                CardView view = Instantiate(choiceCardPrefab, choiceAnchor);
                view.gameObject.SetActive(false);

                // A hand card's hover is tuned to pull one card up and out of
                // an overlapping fan - a large lift, toward the camera, and a
                // hard snap to face it square on. A choice card is not in a
                // fan: it already rests, alone, exactly where the
                // presentation put it, camera-facing. Reusing the hand's own
                // hover geometry on it was a real, measured bug (it could
                // throw a card toward the edge of the screen and out of its
                // slot's rotation); this is the smallest override that
                // replaces it with a small highlight instead, without
                // touching what a hand card does.
                view.ConfigureHover(
                    lift: ChoiceHoverLift, forward: ChoiceHoverForward,
                    scaleMultiplier: ChoiceHoverScale, keepRestingRotationWhileHovered: true);

                _choiceCards.Add(view);
            }
        }

        /// <summary>Tiny vertical rise on hover - a highlight, not the hand's "pulled out of the fan" lift.</summary>
        private const float ChoiceHoverLift = 0.05f;

        /// <summary>Tiny motion toward the camera on hover - sorting order already brings a hovered card to the front.</summary>
        private const float ChoiceHoverForward = 0.03f;

        /// <summary>Within the 1.03-1.06 range asked for: enough to read as "picked out", never enough to touch a neighbour's slot.</summary>
        private const float ChoiceHoverScale = 1.04f;

        /// <summary>
        /// Makes the four choice cards' titles - and, where the text is not
        /// empty, their rules text - share one size within this
        /// presentation, rather than each independently settling on
        /// whatever TMP's own auto-sizing fitted for that card's own name
        /// length. Four cards shown side by side as one choice read as
        /// typographically inconsistent otherwise - "Crypt Fiend" fits
        /// larger than "Skeletal Warrior" purely because it is shorter,
        /// which has nothing to do with which card matters more.
        ///
        /// This never touches <c>CardVisualRecipe_Standard</c>, the text
        /// style tables, or any font asset - only the two pooled
        /// TextMeshPro instances these four particular CardView objects
        /// happen to own, and only after letting each one resolve its own
        /// natural fitted size first. The smallest of the four becomes the
        /// size every one of them is pinned to, so nothing is ever pinned
        /// larger than what its own slot could fit.
        /// </summary>
        private void SynchronizeChoiceTypography(int count)
        {
            SynchronizeLabelGroup(count, card => card.Shown.Name, skipEmpty: false, extraScale: 1f);

            // Rules text specifically reads oversized on these cards even
            // after group synchronization: a short keyword like "Rush" has
            // nothing to make TMP's auto-sizing shrink it, so it grows to
            // fill nearly the whole width of the rules box on its own -
            // exactly the "keyword poster" a manual validation pass flagged.
            // A normal Hearthstone rules line is comfortably smaller than
            // its box, not sized to fill it, so this scales the
            // synchronized result down afterward. Local to this modal
            // presentation only: it runs after the same real per-card fit
            // synchronization above, on the same pooled instances these
            // four CardView objects already own, and touches nothing a hand
            // or board card, the card viewer, or CardVisualRecipe_Standard
            // reads from.
            SynchronizeLabelGroup(count, card => card.Shown.RulesText, skipEmpty: true, extraScale: 0.7f);
        }

        /// <summary>
        /// One synchronized group: every choice card's own label for the
        /// text <paramref name="textOf"/> selects, pinned to the smallest of
        /// their independently-fitted sizes, then scaled by
        /// <paramref name="extraScale"/>.
        ///
        /// Auto-sizing is turned back on and forced to resolve before being
        /// read, rather than trusted from whatever it last settled on: these
        /// CardView instances are pooled and reused across repeated binds
        /// (a new match, a hotseat swap), so a label pinned by a previous
        /// synchronization would otherwise be measured as "already the
        /// right size" instead of being re-fitted to whatever card it is
        /// showing now.
        /// </summary>
        private void SynchronizeLabelGroup(int count, Func<CardView, string> textOf, bool skipEmpty, float extraScale)
        {
            List<TextMeshPro> labels = new List<TextMeshPro>();
            float smallest = float.MaxValue;

            for (int index = 0; index < count && index < _choiceCards.Count; index++)
            {
                CardView card = _choiceCards[index];
                string text = textOf(card);

                if (skipEmpty && string.IsNullOrEmpty(text))
                {
                    continue;
                }

                TextMeshPro label = FindLabelWithText(card, text);

                if (label == null)
                {
                    continue;
                }

                label.enableAutoSizing = true;
                label.ForceMeshUpdate();

                labels.Add(label);
                smallest = Mathf.Min(smallest, label.fontSize);
            }

            if (labels.Count == 0 || !(smallest > 0f) || smallest == float.MaxValue)
            {
                return;
            }

            float finalSize = smallest * extraScale;

            foreach (TextMeshPro label in labels)
            {
                label.enableAutoSizing = false;
                label.fontSize = finalSize;
            }
        }

        private static TextMeshPro FindLabelWithText(CardView card, string text)
        {
            foreach (TextMeshPro label in card.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (label.text == text)
                {
                    return label;
                }
            }

            return null;
        }

        /// <summary>
        /// Spaces the choice cards evenly across the centre of the screen -
        /// in viewport fractions, not board geometry.
        ///
        /// The previous approach positioned the row in world space, offset
        /// from the camera along its forward axis by a fixed distance, with
        /// a fixed world-unit spacing and scale. That reads as "roughly
        /// centred" only by coincidence, at exactly the distance and field
        /// of view it was tuned against; anything else - a different camera
        /// distance, a different aspect ratio, even just the accumulated
        /// rounding of several earlier retunings - moves the row somewhere
        /// else entirely; a manual validation pass caught it spilling off
        /// the left edge and crowding the hero and the board. None of that
        /// is fixable by retuning the same three numbers again, because the
        /// numbers were never the actual problem - being anchored to the
        /// board was.
        ///
        /// This instead asks the camera directly: for a target viewport
        /// point, where is the world point that projects there on a plane
        /// <see cref="presentationPlaneDistance"/> in front of the camera.
        /// That question has the same answer regardless of the board's own
        /// layout, and - because a viewport fraction means the same thing at
        /// any resolution - the same answer at 1600x900 as at 2560x1440.
        ///
        /// Recomputed against the actual option count every time the choices
        /// are rebuilt, rather than fixed at four, so a hero power with a
        /// different number of options - not something Raise has today, but
        /// nothing here assumes it never will - lays out correctly too.
        /// </summary>
        private void LayoutChoiceCards(int count)
        {
            if (count <= 0 || matchCamera == null)
            {
                return;
            }

            float scale = ChoiceCardScale();

            // Centre-to-centre spacing derived from the card's own current
            // viewport width, not a flat constant: a card's viewport width
            // shrinks as the Game view gets wider than 16:9 (the same world
            // size is a smaller fraction of a wider frustum), and a flat
            // spacing constant does not shrink with it - so at a wide Game
            // view the cards read visibly narrower while sitting exactly as
            // far apart, which is what actually made the row look spread
            // out rather than compact. Tying spacing to the card's own
            // measured width keeps the gap looking the same size relative
            // to the cards at any aspect ratio.
            float spacing = ChoiceCardViewportWidth(scale) + choiceCardGap;

            // Facing the camera exactly - the row is a screen composition,
            // not an object resting on the board, so it takes the board's
            // own reading tilt from nowhere.
            Quaternion facing = Quaternion.LookRotation(matchCamera.transform.forward, matchCamera.transform.up);

            float firstOffset = -(count - 1) * 0.5f;

            for (int index = 0; index < count && index < _choiceCards.Count; index++)
            {
                float viewportX = 0.5f + (firstOffset + index) * spacing;
                Vector3 worldPosition = PresentationPlanePoint(viewportX, choiceViewportY);

                // choiceAnchor is kept at the identity transform (see its own
                // tooltip), so a world position and a position local to it
                // are numerically the same thing.
                _choiceCards[index].SetRestingPose(worldPosition, facing, scale);
            }
        }

        /// <summary>
        /// The uniform scale that makes a card <see cref="choiceCardViewportHeight"/>
        /// of the viewport tall at <see cref="presentationPlaneDistance"/>,
        /// given the camera's actual field of view right now. Shared by the
        /// card layout and the layout regression tests, so neither can drift
        /// from the other.
        /// </summary>
        private float ChoiceCardScale()
        {
            float worldCardHeight = choiceCardViewportHeight * PresentationPlaneHeight();
            return worldCardHeight / CardCanvas.CardHeight;
        }

        /// <summary>
        /// How wide a card at <paramref name="scale"/> actually reads on
        /// screen right now, in viewport fractions - unlike its height, this
        /// depends on the camera's current aspect ratio (a wider Game view
        /// spreads the same world width over a wider frustum, so the same
        /// card reads as a smaller fraction of it). Measured against the
        /// card's own full nominal width rather than just its frame art:
        /// the attack and health gems sit at the outer bottom corners and
        /// reach almost exactly this edge, so a narrower "frame-only"
        /// measurement would let two cards' stat gems overlap even while
        /// their frames looked clear.
        /// </summary>
        private float ChoiceCardViewportWidth(float scale)
        {
            float worldCardWidth = CardCanvas.CardWidth * scale;
            float horizontalExtent = PresentationPlaneHeight() * matchCamera.aspect;
            return worldCardWidth / horizontalExtent;
        }

        /// <summary>The full height, in world units, of the presentation plane's visible slice of the frustum.</summary>
        private float PresentationPlaneHeight() =>
            2f * presentationPlaneDistance * Mathf.Tan(matchCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// The world point on the presentation plane - perpendicular to the
        /// camera's forward axis, <see cref="presentationPlaneDistance"/> in
        /// front of it - that a given viewport point projects onto. The
        /// standard screen-to-world unprojection through a fixed plane,
        /// rather than a fixed world offset, which is what makes every
        /// position this feeds resolution- and aspect-independent.
        /// </summary>
        private Vector3 PresentationPlanePoint(float viewportX, float viewportY)
        {
            Ray ray = matchCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            float alignment = Vector3.Dot(ray.direction, matchCamera.transform.forward);
            float distance = presentationPlaneDistance / alignment;
            return ray.origin + ray.direction * distance;
        }

        /// <summary>
        /// Sizes and places the dimmed backdrop to exactly cover the
        /// camera's frustum at <see cref="presentationPlaneDistance"/>, plus
        /// <see cref="backdropCoverageMargin"/> of slack - computed from the
        /// live camera every time the menu opens, rather than baked in once,
        /// so a window resized between matches, or a Game view at a
        /// completely different aspect, still gets a backdrop that actually
        /// covers it.
        /// </summary>
        private void LayoutChoiceBackdrop()
        {
            if (choiceBackdrop == null || matchCamera == null)
            {
                return;
            }

            Transform backdrop = choiceBackdrop.transform;

            backdrop.SetPositionAndRotation(
                matchCamera.transform.position + matchCamera.transform.forward * presentationPlaneDistance,
                Quaternion.LookRotation(matchCamera.transform.forward, matchCamera.transform.up));

            float verticalExtent = PresentationPlaneHeight();
            float horizontalExtent = verticalExtent * matchCamera.aspect;

            backdrop.localScale = new Vector3(
                horizontalExtent * backdropCoverageMargin, verticalExtent * backdropCoverageMargin, 1f);
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
