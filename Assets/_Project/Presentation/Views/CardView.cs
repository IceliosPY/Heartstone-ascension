using CoH.Core.Identifiers;
using CoH.Presentation.CardVisuals;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// One card in a hand.
    ///
    /// It owns no picture and knows no card. What a card looks like is composed
    /// from data by the <see cref="CardVisualFactory"/> and drawn by the
    /// <see cref="CardVisualPainter"/>, which is why this one component shows a
    /// neutral minion, a spell and a legendary without a second prefab existing
    /// and without a single line here mentioning any of them. Turning a card
    /// into a different kind of card is handing the painter a different plan.
    ///
    /// What is left here is everything the composer has no opinion about: where
    /// the card sits, how it rises to be read, how it follows the pointer, and
    /// whether the engine says it can be played. A cost going from 5 to 3
    /// rewrites a label; it never re-resolves an image.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [Header("Composition")]
        [SerializeField] private CardVisualFactory visuals;
        [SerializeField] private CardVisualPainter painter;

        [Header("Hover")]
        [Tooltip(
            "How far a hovered card rises out of the hand, clear above the fan. This is what " +
            "makes it read as a card being presented rather than merely poking up out of a " +
            "crowd of neighbours.")]
        [SerializeField] private float hoverLift = 1.8f;

        [Tooltip("How far it comes toward the camera, which is what puts it in front of its neighbours.")]
        [SerializeField] private float hoverForward = 1f;

        [Tooltip(
            "How much larger a hovered card is than its neighbours. Bigger than a small " +
            "swelling, so a hand card's text, art and stats are actually comfortable to read at " +
            "a glance, but short of filling the screen - the lift and the approach toward the " +
            "camera already do most of the work of picking it out.")]
        [SerializeField] private float hoverScale = 1.2f;

        [Tooltip(
            "Extra degrees a hovered card turns to face the camera. The hand lies on a plane " +
            "tilted thirty six degrees while the camera looks down at fifty four, so a card " +
            "that has merely straightened out of the fan is still eighteen degrees away from " +
            "square: its top edge is further off than its bottom, and the writing keystones. " +
            "This closes that.")]
        [SerializeField] private float hoverFaceOn = 18f;

        [Tooltip("How quickly a card reaches its target pose. Higher is snappier.")]
        [SerializeField] private float poseSmoothing = 18f;

        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private CardVisualDescriptor _shown;
        private bool _hasShown;

        private bool _isHovered;
        private bool _isPlayable;
        private bool _isFaceDown;
        private bool _isDragging;

        private Vector3 _restingPosition;
        private Quaternion _restingRotation = Quaternion.identity;
        private float _restingScale = 1f;
        private bool _hasPose;
        private Transform _poseParent;

        private Collider _collider;
        private UnityEngine.Rendering.SortingGroup _group;
        private int _handOrder;
        private bool _keepRestingRotationWhileHovered;

        /// <summary>Which card instance in the engine this view stands for.</summary>
        public EntityId EntityId { get; private set; }

        /// <summary>Whether the engine says this card can be played right now.</summary>
        public bool IsPlayable => _isPlayable;

        public bool IsFaceDown => _isFaceDown;

        /// <summary>True while the pointer is over this card and it has risen.</summary>
        public bool IsHovered => _isHovered;

        /// <summary>True while this card is following the pointer.</summary>
        public bool IsDragging => _isDragging;

        /// <summary>Where the layout wants this card, whatever it is doing right now.</summary>
        public Vector3 RestingLocalPosition => _restingPosition;

        /// <summary>
        /// What the last composition could not find. Empty on a finished card.
        ///
        /// Read by the reports rather than by the game: a card with a gap still
        /// draws, with that layer absent, so a missing file is a thing somebody
        /// can see and fix instead of an exception in the middle of a match.
        /// </summary>
        internal System.Collections.Generic.IReadOnlyList<CardVisualGap> Gaps => _plan.Gaps;

        /// <summary>The composed stack, for the tests and the preview tool.</summary>
        internal CardVisualPlan Plan => _plan;

        /// <summary>
        /// The card this view is currently showing. Diagnostics and tests.
        ///
        /// Exposed so that a test can compose the very same card a second time,
        /// through the same factory, and compare the two — which is the only way
        /// to tell a difference in how a card is composed from a difference in
        /// how it is drawn.
        /// </summary>
        internal CardVisualDescriptor Shown => _shown;

        private void Awake()
        {
            _collider = GetComponent<Collider>();

            if (painter == null)
            {
                painter = GetComponent<CardVisualPainter>();
            }

            EnsureSortingGroup();
        }

        /// <summary>
        /// Makes the card sort as one object rather than as twenty loose
        /// layers.
        ///
        /// This is the whole reason a hovered card used to come forward and
        /// still be painted over. A card is a stack of sprites with sorting
        /// orders from the backdrop up to the last label, and those orders are
        /// global: every card's frame is order twenty and every card's name is
        /// order a hundred and thirty, so the *neighbour's* name drew in front
        /// of *this* card's frame whatever the two cards' depths were. Moving a
        /// card toward the camera could not fix that, because depth was never
        /// what decided it.
        ///
        /// A sorting group makes those orders private to the card. Inside, the
        /// layers stack as the recipe says; outside, the card is one thing with
        /// one order, and cards sort against each other — and against the board
        /// and the hero behind them — by that.
        /// </summary>
        private void EnsureSortingGroup()
        {
            if (_group != null)
            {
                return;
            }

            _group = GetComponent<UnityEngine.Rendering.SortingGroup>();

            if (_group == null)
            {
                _group = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            }

            ApplySorting();
        }

        /// <summary>
        /// Where this card stands in the hand, left to right.
        ///
        /// The later a card, the further forward it draws, which is what makes
        /// an overlapping fan read as a fan.
        /// </summary>
        public void SetHandOrder(int order)
        {
            if (_handOrder == order)
            {
                return;
            }

            _handOrder = order;
            ApplySorting();
        }

        private void ApplySorting()
        {
            if (_group == null)
            {
                return;
            }

            // The hand lives in front of the board and the heroes, so a hand
            // that overlaps them covers them rather than being cut into by
            // them. A card being read, or carried, comes further forward still.
            int order = HandBase + Mathf.Clamp(_handOrder, 0, 99);

            if (_isDragging)
            {
                order = CarriedOrder;
            }
            else if (_isHovered)
            {
                order = ReadOrder;
            }

            _group.sortingOrder = order;
        }

        /// <summary>The hand draws above the board and both heroes.</summary>
        private const int HandBase = 100;

        /// <summary>A card being read stands in front of the rest of the hand.</summary>
        private const int ReadOrder = 300;

        /// <summary>And one being carried stands in front of everything.</summary>
        private const int CarriedOrder = 400;

        /// <summary>
        /// Records where the layout wants this card.
        ///
        /// Hovering and dragging are offsets from this pose, never edits to it,
        /// which is what stops a card drifting a little further out of the hand
        /// every time the pointer crosses it. However an interaction ends, the
        /// card returns to exactly what the fan computed.
        /// </summary>
        public void SetRestingPose(Vector3 localPosition, Quaternion localRotation, float scale)
        {
            _restingPosition = localPosition;
            _restingRotation = localRotation;
            _restingScale = scale;

            // A card eases to a new pose when the hand re-fans under it, and
            // arrives instantly when it is new or when the board has just
            // flipped. Easing across a turn change would mean sliding somebody
            // else's hand across the table.
            bool snap = !_hasPose || _poseParent != transform.parent;

            _hasPose = true;
            _poseParent = transform.parent;

            if (snap && !_isDragging)
            {
                ApplyPose(1f);
            }
        }

        /// <summary>
        /// Overrides this specific card's hover response away from the
        /// hand's own tuned defaults.
        ///
        /// A hand card overlaps its neighbours in a fan, so hovering it has
        /// to rise clear of them and square up to the camera to be read at
        /// all - that is what <see cref="hoverLift"/>, <see cref="hoverForward"/>
        /// and the hand's absolute "square to camera" rotation are for. A
        /// Raise choice card is nothing like that: it already rests exactly
        /// where the presentation put it, camera-facing, in its own isolated
        /// slot in a compact row - so hovering it should only need a small
        /// highlight, and reusing the hand's own geometry is exactly what
        /// threw a choice card out of its slot and, at large enough lift,
        /// toward the edge of the screen.
        ///
        /// <paramref name="keepRestingRotationWhileHovered"/> is what stops
        /// <see cref="ApplyPose"/> replacing the resting rotation with the
        /// hand's absolute facing angle: a choice card's resting rotation is
        /// already correct (computed to face the camera), so hovering it
        /// should never touch rotation at all.
        /// </summary>
        public void ConfigureHover(
            float lift, float forward, float scaleMultiplier, bool keepRestingRotationWhileHovered)
        {
            hoverLift = lift;
            hoverForward = forward;
            hoverScale = scaleMultiplier;
            _keepRestingRotationWhileHovered = keepRestingRotationWhileHovered;
        }

        /// <summary>Raises the card so it can be read, and brings it in front of its neighbours.</summary>
        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered)
            {
                return;
            }

            _isHovered = hovered;
            ApplySorting();
            Repaint();
        }

        /// <summary>
        /// Takes the card out of the hand so it can follow the pointer. The
        /// collider goes with it: a card under the cursor would otherwise be the
        /// first thing every ray meets, and the board could never be aimed at.
        /// </summary>
        public void BeginDrag(Transform dragLayer)
        {
            _isDragging = true;
            _isHovered = false;
            ApplySorting();

            if (dragLayer != null)
            {
                transform.SetParent(dragLayer, true);
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            Repaint();
        }

        /// <summary>Places the dragged card, in world space, under the pointer.</summary>
        public void UpdateDrag(Vector3 worldPosition, Quaternion worldRotation, float scale)
        {
            if (!_isDragging)
            {
                return;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// Puts the card back under the hand without moving it, so it glides
        /// home from wherever it was let go rather than blinking there.
        /// </summary>
        public void EndDrag(Transform handAnchor)
        {
            _isDragging = false;
            ApplySorting();

            if (handAnchor != null)
            {
                transform.SetParent(handAnchor, true);
                _poseParent = handAnchor;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            Repaint();
        }

        private void LateUpdate()
        {
            if (_isDragging || !_hasPose)
            {
                return;
            }

            // Frame rate independent easing: what stays constant is the fraction
            // of the remaining distance covered per second, not per frame.
            ApplyPose(1f - Mathf.Exp(-poseSmoothing * Time.deltaTime));
        }

        /// <summary>
        /// Starts this card a short distance below, and a little smaller than,
        /// the slot the layout just gave it, then lets the ordinary pose easing
        /// in <see cref="LateUpdate"/> carry it the rest of the way.
        ///
        /// For a hand that has just become the one in front of the acting
        /// player: <see cref="SetRestingPose"/> has already been called with the
        /// correct target for this turn, so nudging the current transform away
        /// from that target and doing nothing else is enough to make the very
        /// next frame ease back up into it - the same smoothing every other
        /// re-layout already uses, not a second animation system.
        /// </summary>
        public void NudgeBelowRestingPose(float drop, float shrinkFactor)
        {
            if (_isDragging || !_hasPose)
            {
                return;
            }

            transform.localPosition = _restingPosition + new Vector3(0f, -drop, 0f);
            transform.localRotation = _restingRotation;
            transform.localScale = Vector3.one * (_restingScale * shrinkFactor);
        }

        /// <summary>
        /// Puts the card at its target pose at once, rather than easing there.
        ///
        /// For anything that has to see the finished result without a running
        /// game: a still of a hovered card, or a test that would otherwise be
        /// asserting on how far along an animation happened to be.
        /// </summary>
        internal void SnapToPose() => ApplyPose(1f);

        private void ApplyPose(float t)
        {
            Vector3 targetPosition = _isHovered
                ? _restingPosition + new Vector3(0f, hoverLift, -hoverForward)
                : _restingPosition;

            // A hovered card straightens up out of the fan and turns the rest of
            // the way to face the camera, which is most of what makes it
            // readable: out of the lean, and out of the keystoning. A card
            // configured to keep its resting rotation (see ConfigureHover)
            // skips this entirely - it is already facing the camera, and
            // this absolute angle is meaningless outside the hand's own
            // fixed fan tilt.
            Quaternion targetRotation = _isHovered && !_keepRestingRotationWhileHovered
                ? Quaternion.Euler(hoverFaceOn, 0f, 0f)
                : _restingRotation;
            float targetScale = _isHovered ? _restingScale * hoverScale : _restingScale;

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, t);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, t);
        }

        /// <summary>
        /// Shows a card, face up, from a snapshot the presenter built.
        ///
        /// Two paths, and which one is taken is the difference between a smooth
        /// hand and a stuttering one. A card that has become a different card —
        /// a different type, class, rarity or painting — is composed again. A
        /// card that is the same card with different numbers on it, which is
        /// what a match produces constantly, only has its labels rewritten.
        /// </summary>
        public void Bind(CardViewModel model)
        {
            EntityId = model.EntityId;
            _isPlayable = model.IsPlayable;
            _isFaceDown = false;

            if (visuals == null)
            {
                return;
            }

            Show(visuals.Describe(model), model.IsPlayable);
        }

        /// <summary>
        /// Shows the back of a card. Used for the waiting player's hand, where
        /// the count matters and the contents do not.
        ///
        /// Composed rather than covered: the back is layers like everything
        /// else, so it can vary by style without a lid being invented for it.
        /// Nothing about the card is passed in, which is the strongest form the
        /// guarantee can take — there is nothing here to leak.
        /// </summary>
        public void BindFaceDown()
        {
            EntityId = EntityId.None;
            _isPlayable = false;
            _isHovered = false;
            _isFaceDown = true;

            if (visuals == null)
            {
                return;
            }

            Show(
                new CardVisualDescriptor(
                    Core.Cards.CardType.None,
                    Core.Cards.CardClass.Neutral,
                    showsCost: false,
                    faceDown: true),
                playable: false);
        }

        /// <summary>
        /// Shows a described card directly, without a match behind it. The
        /// preview tool, the captures and the tests use this.
        ///
        /// Lit by default, because a card composed outside a match has no
        /// engine to ask whether it can be played, and a still of six dimmed
        /// cards would misrepresent the game rather than describe it.
        /// </summary>
        public void Show(in CardVisualDescriptor card, bool playable = true)
        {
            if (visuals == null || painter == null)
            {
                return;
            }

            _isPlayable = playable;
            _isFaceDown = card.IsFaceDown;

            if (_hasShown && _shown.LooksTheSameAs(card))
            {
                // Same pictures, possibly different numbers.
                RecomposeTextOnly(card);
                return;
            }


            visuals.Compose(card, _plan);
            painter.Apply(_plan);

            _shown = card;
            _hasShown = true;

            Repaint();
        }

        private void RecomposeTextOnly(in CardVisualDescriptor card)
        {
            visuals.Compose(card, _plan);
            painter.RefreshText(_plan);

            _shown = card;
            Repaint();
        }

        /// <summary>
        /// Paints the card for its current state.
        ///
        /// An unplayable card is dimmed rather than merely refused on click, so
        /// a player can tell at a glance what they can afford. The judgement
        /// itself is never made here: it arrives already decided in the model.
        ///
        /// A card being read is never dimmed. Not affording a card stops it
        /// being played, not inspected, and a player deciding what to do next
        /// needs to read exactly the ones they cannot afford yet.
        /// </summary>
        private void Repaint()
        {
            if (painter == null)
            {
                return;
            }

            bool lit = _isFaceDown || _isPlayable || _isHovered || _isDragging;
            painter.SetDimmed(!lit);
        }

        /// <summary>
        /// Where this card lies in the fan, left to right.
        ///
        /// Unlike <see cref="DrawOrder"/> this says nothing about being read or
        /// carried, which is what makes it the right question to ask of a
        /// pointer: a card being hovered draws in front of the whole hand, and
        /// a pointer that took that as its answer would never be able to leave
        /// it.
        /// </summary>
        internal int HandOrder => _handOrder;

        /// <summary>
        /// Where this card draws relative to everything else. Diagnostics and
        /// tests: this is the number that decides whether a hovered card is
        /// actually in front, and depth alone never did.
        /// </summary>
        internal int DrawOrder => _group == null ? 0 : _group.sortingOrder;

        /// <summary>Where this card's appearance comes from. Tooling and tests.</summary>
        internal CardVisualFactory Visuals => visuals;

        /// <summary>
        /// Points a card at a factory and a painter, for a card that was not
        /// built from the prefab. Used by the tests and the preview tool, which
        /// is the point: they compose through this class rather than around it.
        /// </summary>
        internal void UseForTests(CardVisualFactory factory, CardVisualPainter thePainter)
        {
            visuals = factory;
            painter = thePainter;
            _hasShown = false;
        }
    }
}
