using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CoH.Presentation
{
    /// <summary>
    /// Turns the mouse into commands.
    ///
    /// The whole interaction is one <see cref="InteractionState"/>. Picking a
    /// card up and aiming an attack are two states rather than two flags, so
    /// they cannot both be true, and neither can start while the queue is
    /// replaying: that is a state too.
    ///
    /// Both gestures work the way they do in Hearthstone, which is two gestures
    /// in one. Press on a card and drag: it follows the pointer and dropping it
    /// on your board plays it. Press and release without moving: it stays stuck
    /// to the pointer, and the next press puts it down. The same is true of an
    /// attacker and its arrow. One threshold in world units separates the two,
    /// and everything after it is identical.
    ///
    /// It decides nothing about the rules. Which cards may be played, which
    /// minions may swing and what they may hit are all asked of the engine, every
    /// time, and the answers are only ever painted. Nothing here recomputes mana,
    /// summoning sickness or a legal target, and nothing here removes a card
    /// from a hand or puts a minion on a board: it sends a command and waits to
    /// be told what happened.
    /// </summary>
    public sealed class MatchInputController : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private MatchPresenter presenter;
        [SerializeField] private MatchHud hud;
        [SerializeField] private Camera matchCamera;
        [SerializeField] private TargetingArrow targetingArrow;
        [SerializeField] private LayerMask clickMask = ~0;

        [Header("Drag")]
        [Tooltip("How far in front of the camera a dragged card floats. Nearer than the hand, so nothing covers it.")]
        [SerializeField] private float dragDistance = 8.2f;

        [Tooltip("A card in the air reads a little smaller than one being inspected in the hand.")]
        [SerializeField] private float dragScale = 0.8f;

        [Tooltip("How far above the pointer the card rides, so the slot underneath stays visible.")]
        [SerializeField] private float dragLift = 0.62f;

        [Tooltip("Movement past which a release drops the card, rather than leaving it stuck to the pointer.")]
        [SerializeField] private float stickThreshold = 0.45f;

        [Header("Aiming")]
        [Tooltip("Height above the table the targeting arrow is aimed along.")]
        [SerializeField] private float aimHeight = 0.45f;

        private readonly PointerProbe _probe = new PointerProbe();
        private readonly List<EntityId> _highlighted = new List<EntityId>();

        private InteractionState _state = InteractionState.Idle;
        private EntityId _held = EntityId.None;
        private EntityId _hoveredCard = EntityId.None;
        private EntityId _hoveredTarget = EntityId.None;
        private Vector3 _pressPoint;
        private bool _movedEnoughToDrop;

        /// <summary>What the player is doing right now.</summary>
        public InteractionState State => _state;

        /// <summary>The card or minion currently held, or None.</summary>
        internal EntityId HeldEntity => _held;

        /// <summary>Kept for the older click tests: true while something is held.</summary>
        internal bool HasSelection =>
            _state == InteractionState.DraggingHandCard || _state == InteractionState.TargetingAttack;

        internal EntityId SelectedEntity => _held;

        /// <summary>What the last pointer event landed on. Diagnostics only.</summary>
        internal string LastHit { get; private set; } = "none";

        /// <summary>Everything the engine currently says the held attacker may hit.</summary>
        internal IReadOnlyList<EntityId> HighlightedTargets => _highlighted;

        private void Awake()
        {
            if (matchCamera == null)
            {
                matchCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (hud != null)
            {
                hud.EndTurnRequested += OnEndTurnRequested;
            }

            if (session != null)
            {
                session.CommandRejected += OnCommandRejected;
            }
        }

        private void OnDisable()
        {
            if (hud != null)
            {
                hud.EndTurnRequested -= OnEndTurnRequested;
            }

            if (session != null)
            {
                session.CommandRejected -= OnCommandRejected;
            }
        }

        private void Update()
        {
            if (session == null || !session.IsReady || matchCamera == null)
            {
                return;
            }

            if (!UpdateAvailability())
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();

            // A click on the HUD is a click on the HUD. Without this the End
            // Turn button would also throw a ray at whatever sits behind it.
            if (IsPointerOverHud())
            {
                if (_state == InteractionState.HoveringHandCard)
                {
                    ClearCardHover();
                }

                return;
            }

            Ray ray = matchCamera.ScreenPointToRay(screen);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                PointerDown(ray);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                PointerUp(ray);
            }
            else
            {
                PointerMove(ray);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelInteraction();
            }
        }

        /// <summary>
        /// Keeps the state in step with the match itself, and reports whether
        /// the player may act at all.
        ///
        /// An interaction under way when the queue starts, or when the match
        /// ends, is dropped rather than left hanging: the board it was aimed at
        /// is about to stop existing.
        /// </summary>
        private bool UpdateAvailability()
        {
            bool ended = session.State.HasEnded;
            bool busy = session.IsBusy;

            if (hud != null)
            {
                hud.SetInteractable(!ended && !busy);
            }

            if (ended)
            {
                if (_state != InteractionState.GameEnded)
                {
                    CancelInteraction();
                    _state = InteractionState.GameEnded;
                }

                return false;
            }

            if (busy)
            {
                if (_state != InteractionState.Resolving)
                {
                    CancelInteraction();
                    _state = InteractionState.Resolving;
                }

                return false;
            }

            if (_state == InteractionState.Resolving || _state == InteractionState.GameEnded)
            {
                _state = InteractionState.Idle;
            }

            return true;
        }

        // ------------------------------------------------------------------
        //  Pointer events. Tests drive these directly, because a pointer device
        //  does not exist in batch mode. Each one checks availability first, so
        //  refusing to act during a resolution is a property of the controller
        //  and not of the single method that happens to read the mouse.
        // ------------------------------------------------------------------

        internal void PointerDown(Ray ray)
        {
            if (!UpdateAvailability())
            {
                return;
            }

            Probe(ray);

            switch (_state)
            {
                case InteractionState.Idle:
                case InteractionState.HoveringHandCard:
                    BeginInteraction(ray);
                    break;

                // Already holding something, from a press and release that did
                // not move. This press is the player putting it down.
                case InteractionState.DraggingHandCard:
                    ResolveCardDrop(ray);
                    break;

                case InteractionState.TargetingAttack:
                    ResolveAttack();
                    break;
            }
        }

        internal void PointerMove(Ray ray)
        {
            if (!UpdateAvailability())
            {
                return;
            }

            Probe(ray);

            switch (_state)
            {
                case InteractionState.Idle:
                case InteractionState.HoveringHandCard:
                    UpdateCardHover();
                    break;

                case InteractionState.DraggingHandCard:
                    UpdateCardDrag(ray);
                    break;

                case InteractionState.TargetingAttack:
                    UpdateAiming(ray);
                    break;
            }
        }

        internal void PointerUp(Ray ray)
        {
            if (!UpdateAvailability())
            {
                return;
            }

            Probe(ray);

            if (_state != InteractionState.DraggingHandCard && _state != InteractionState.TargetingAttack)
            {
                return;
            }

            // A press and release in the same place is a pickup, not a drop.
            // Hearthstone works this way, and it is what lets the same gesture
            // be either a drag or two clicks.
            if (!_movedEnoughToDrop)
            {
                return;
            }

            if (_state == InteractionState.DraggingHandCard)
            {
                ResolveCardDrop(ray);
            }
            else
            {
                ResolveAttack();
            }
        }

        /// <summary>A whole click in one call: press then release without moving.</summary>
        internal void HandleClick(Ray ray)
        {
            PointerDown(ray);
            PointerUp(ray);
        }

        // ------------------------------------------------------------------
        //  Starting an interaction
        // ------------------------------------------------------------------

        private void BeginInteraction(Ray ray)
        {
            if (_probe.TryFind(PointerTargetKind.HandCard, out PointerHit card))
            {
                BeginCardDrag(card, ray);
                return;
            }

            if (_probe.TryFind(PointerTargetKind.FriendlyMinion, out PointerHit minion))
            {
                BeginAttackAiming(minion, ray);
            }
        }

        private void BeginCardDrag(PointerHit hit, Ray ray)
        {
            PlayerId acting = session.State.CurrentPlayer;

            // Asked of the engine, not worked out here. A card that cannot be
            // played can still be picked up and read, but it never leaves the
            // hand, and no command is built for it.
            RejectionReason why = session.Validate(new PlayCardCommand(acting, hit.EntityId));

            if (why != RejectionReason.None)
            {
                SetHint(Explain(why));
                return;
            }

            ClearCardHover();

            _state = InteractionState.DraggingHandCard;
            _held = hit.EntityId;
            _pressPoint = DragPoint(ray);
            _movedEnoughToDrop = false;

            presenter.SetDraggedCard(_held);

            if (presenter.TryGetCardView(_held, out CardView view))
            {
                view.BeginDrag(presenter.DragLayer);
            }

            UpdateCardDrag(ray);
            SetHint("Drop it on your side of the board.");
        }

        private void BeginAttackAiming(PointerHit hit, Ray ray)
        {
            PlayerId acting = session.State.CurrentPlayer;

            RejectionReason why = session.CanAttack(acting, hit.EntityId);

            if (why != RejectionReason.None)
            {
                SetHint(Explain(why));
                return;
            }

            ClearCardHover();

            _state = InteractionState.TargetingAttack;
            _held = hit.EntityId;
            _pressPoint = AimPoint(ray);
            _movedEnoughToDrop = false;

            if (presenter.TryGetMinionView(_held, out MinionView attacker))
            {
                attacker.SetSelected(true);
            }

            HighlightLegalTargets(acting, _held);
            UpdateAiming(ray);
            SetHint("Pick something to attack.");
        }

        // ------------------------------------------------------------------
        //  Continuing an interaction
        // ------------------------------------------------------------------

        private void UpdateCardHover()
        {
            EntityId under = _probe.TryFind(PointerTargetKind.HandCard, out PointerHit card)
                ? card.EntityId
                : EntityId.None;

            if (under == _hoveredCard)
            {
                return;
            }

            ClearCardHover();

            if (!under.IsNone && presenter.TryGetCardView(under, out CardView view))
            {
                view.SetHovered(true);
                _hoveredCard = under;
                _state = InteractionState.HoveringHandCard;
            }
            else
            {
                _state = InteractionState.Idle;
            }
        }

        private void UpdateCardDrag(Ray ray)
        {
            Vector3 point = DragPoint(ray);

            if (Vector3.Distance(point, _pressPoint) > stickThreshold)
            {
                _movedEnoughToDrop = true;
            }

            if (presenter.TryGetCardView(_held, out CardView view))
            {
                // Squared up with the camera rather than left lying at the angle
                // of the hand, which is what makes a card readable in the air.
                view.UpdateDrag(point, matchCamera.transform.rotation, dragScale);
            }

            presenter.SetInsertionPreview(ResolveInsertionSlot(ray));
        }

        private void UpdateAiming(Ray ray)
        {
            Vector3 point = AimPoint(ray);

            if (Vector3.Distance(point, _pressPoint) > stickThreshold)
            {
                _movedEnoughToDrop = true;
            }

            if (targetingArrow != null && presenter.TryGetMinionView(_held, out MinionView attacker))
            {
                Vector3 tip = point;

                // Snap the tip onto a legal target so the arrow commits to it
                // visibly, rather than hovering just short of the thing it is
                // about to hit.
                if (_probe.TryFindCharacter(out PointerHit character) && IsLegalTarget(character.EntityId))
                {
                    tip = character.Collider.bounds.center;
                }

                targetingArrow.Show(attacker.transform.position, tip);
            }

            RefreshHoveredTarget();
        }

        private void RefreshHoveredTarget()
        {
            EntityId under = EntityId.None;

            if (_probe.TryFindCharacter(out PointerHit character) && IsLegalTarget(character.EntityId))
            {
                under = character.EntityId;
            }

            if (under == _hoveredTarget)
            {
                return;
            }

            SetTargetHighlighted(_hoveredTarget, false);
            SetTargetHighlighted(under, true);
            _hoveredTarget = under;
        }

        // ------------------------------------------------------------------
        //  Finishing an interaction
        // ------------------------------------------------------------------

        private void ResolveCardDrop(Ray ray)
        {
            EntityId card = _held;
            int slot = ResolveInsertionSlot(ray);

            // Read the acting player before anything is cleared, so the command
            // is built for whoever actually made the gesture.
            PlayerId acting = session.State.CurrentPlayer;

            EndCardInteraction();

            if (slot < 0)
            {
                // Released somewhere that is not the player's own board. The
                // card is already on its way home.
                SetHint(string.Empty);
                return;
            }

            // Only clear the hint when the play went through. A refusal has
            // already put its reason there, and wiping it would hide the one
            // explanation the player is waiting for.
            if (session.Submit(new PlayCardCommand(acting, card, slot)))
            {
                SetHint(string.Empty);
            }
        }

        private void ResolveAttack()
        {
            EntityId attacker = _held;
            PlayerId acting = session.State.CurrentPlayer;

            EntityId target = _probe.TryFindCharacter(out PointerHit character) && IsLegalTarget(character.EntityId)
                ? character.EntityId
                : EntityId.None;

            EndAttackInteraction();

            if (target.IsNone)
            {
                SetHint(string.Empty);
                return;
            }

            if (session.Submit(new AttackCommand(acting, attacker, target)))
            {
                SetHint(string.Empty);
            }
        }

        /// <summary>Drops whatever is held, changing nothing about the match.</summary>
        private void CancelInteraction()
        {
            switch (_state)
            {
                case InteractionState.DraggingHandCard:
                    EndCardInteraction();
                    break;

                case InteractionState.TargetingAttack:
                    EndAttackInteraction();
                    break;

                case InteractionState.HoveringHandCard:
                    ClearCardHover();
                    break;
            }

            SetHint(string.Empty);
        }

        private void EndCardInteraction()
        {
            if (presenter.TryGetCardView(_held, out CardView view))
            {
                view.EndDrag(presenter.NearHandAnchor);
            }

            presenter.SetInsertionPreview(-1);
            presenter.SetDraggedCard(EntityId.None);

            _held = EntityId.None;
            _movedEnoughToDrop = false;
            _state = InteractionState.Idle;
        }

        private void EndAttackInteraction()
        {
            if (presenter.TryGetMinionView(_held, out MinionView attacker))
            {
                attacker.SetSelected(false);
            }

            ClearHighlights();

            if (targetingArrow != null)
            {
                targetingArrow.Hide();
            }

            _held = EntityId.None;
            _hoveredTarget = EntityId.None;
            _movedEnoughToDrop = false;
            _state = InteractionState.Idle;
        }

        /// <summary>
        /// The engine refused something the preview thought was fine. Nothing
        /// here assumes a preview guarantees acceptance, so this only has to put
        /// the view back in step and say what happened.
        /// </summary>
        private void OnCommandRejected(GameCommand command, RejectionReason reason)
        {
            CancelInteraction();

            if (presenter != null)
            {
                presenter.Rebuild();
            }

            SetHint(Explain(reason));
        }

        // ------------------------------------------------------------------
        //  Geometry
        // ------------------------------------------------------------------

        private void Probe(Ray ray)
        {
            _probe.Probe(ray, clickMask, session.State, session.State.CurrentPlayer);
            LastHit = _probe.Hits.Count > 0 ? _probe.Nearest.Describe() : "nothing";
        }

        /// <summary>
        /// Where a dragged card floats: on a plane square to the camera, a fixed
        /// distance away.
        ///
        /// Not the pointer position in pixels pushed into the world, and not a
        /// point on the table either. A plane facing the camera keeps the card
        /// exactly under the cursor and exactly the same size wherever it is
        /// taken, and being nearer than anything on the table means it is never
        /// behind a hero or a minion.
        /// </summary>
        private Vector3 DragPoint(Ray ray)
        {
            Transform eye = matchCamera.transform;
            Plane plane = new Plane(-eye.forward, eye.position + eye.forward * dragDistance);

            Vector3 point = plane.Raycast(ray, out float distance)
                ? ray.GetPoint(distance)
                : ray.GetPoint(dragDistance);

            // Held a little above the pointer rather than centred on it, so the
            // card does not sit on top of the slot it is being aimed at. The
            // pointer ends up just under its bottom edge, which is where a hand
            // holding a card would be.
            return point + eye.up * dragLift;
        }

        /// <summary>Where the arrow is aimed: a level plane just above the table.</summary>
        private Vector3 AimPoint(Ray ray)
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0f, aimHeight, 0f));

            return plane.Raycast(ray, out float distance)
                ? ray.GetPoint(distance)
                : ray.GetPoint(dragDistance);
        }

        /// <summary>
        /// Which slot the pointer is between, or -1 when it is not over the
        /// acting player's board at all.
        ///
        /// The drop zone collider answers whether the board is being aimed at,
        /// and the geometry answers where. Splitting it that way means the zone
        /// can be moved or resized in the scene without any of this changing.
        /// </summary>
        private int ResolveInsertionSlot(Ray ray)
        {
            if (!_probe.TryFind(PointerTargetKind.BoardDropZone, out PointerHit zone) ||
                zone.Zone == null || !zone.Zone.IsNearSide)
            {
                return -1;
            }

            Transform row = presenter.NearBoardAnchor;
            if (row == null)
            {
                return -1;
            }

            Plane plane = new Plane(Vector3.up, zone.Zone.transform.position);
            Vector3 point = plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : zone.Point;

            int count = session.State.GetPlayer(session.State.CurrentPlayer).Board.Count;

            return BoardDropResolver.Resolve(
                row.InverseTransformPoint(point).x, count, presenter.BoardSpacing);
        }

        private bool IsPointerOverHud() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // ------------------------------------------------------------------
        //  Highlights
        // ------------------------------------------------------------------

        private void HighlightLegalTargets(PlayerId player, EntityId attacker)
        {
            ClearHighlights();
            _highlighted.AddRange(session.GetLegalAttackTargets(player, attacker));

            for (int index = 0; index < _highlighted.Count; index++)
            {
                SetTargetable(_highlighted[index], true);
            }
        }

        private bool IsLegalTarget(EntityId id) => !id.IsNone && _highlighted.Contains(id);

        private void ClearHighlights()
        {
            for (int index = 0; index < _highlighted.Count; index++)
            {
                SetTargetable(_highlighted[index], false);
            }

            _highlighted.Clear();
        }

        private void SetTargetable(EntityId id, bool targetable)
        {
            if (presenter.TryGetMinionView(id, out MinionView minion))
            {
                minion.SetTargetable(targetable);
            }
            else if (presenter.TryGetHeroView(id, out HeroView hero))
            {
                hero.SetTargetable(targetable);
            }
        }

        private void SetTargetHighlighted(EntityId id, bool highlighted)
        {
            if (id.IsNone)
            {
                return;
            }

            if (presenter.TryGetMinionView(id, out MinionView minion))
            {
                minion.SetTargetHighlighted(highlighted);
            }
            else if (presenter.TryGetHeroView(id, out HeroView hero))
            {
                hero.SetTargetHighlighted(highlighted);
            }
        }

        private void ClearCardHover()
        {
            if (!_hoveredCard.IsNone && presenter.TryGetCardView(_hoveredCard, out CardView view))
            {
                view.SetHovered(false);
            }

            _hoveredCard = EntityId.None;
        }

        // ------------------------------------------------------------------
        //  Odds and ends
        // ------------------------------------------------------------------

        private void OnEndTurnRequested()
        {
            if (session == null || session.IsBusy || session.State.HasEnded)
            {
                return;
            }

            CancelInteraction();
            session.Submit(new EndTurnCommand(session.State.CurrentPlayer));
        }

        private void SetHint(string hint)
        {
            if (hud != null)
            {
                hud.SetHint(hint);
            }
        }

        /// <summary>
        /// Puts the engine's refusal into words. The words are ours; the
        /// judgement is entirely the engine's, and no case here is ever
        /// evaluated locally to reach the same answer a second time.
        /// </summary>
        private static string Explain(RejectionReason reason) => reason switch
        {
            RejectionReason.NotEnoughMana => "Not enough mana.",
            RejectionReason.BoardFull => "Your board is full.",
            RejectionReason.CardTypeNotPlayable => "That card cannot be played yet.",
            RejectionReason.SummoningSickness => "It was summoned this turn.",
            RejectionReason.AlreadyAttacked => "It has already attacked this turn.",
            RejectionReason.ZeroAttack => "It has no attack.",
            RejectionReason.NotYourTurn => "It is not your turn.",
            RejectionReason.InvalidBoardPosition => "That is not a place it can go.",
            RejectionReason.InvalidTarget => "That is not a legal target.",
            RejectionReason.None => string.Empty,
            _ => string.Empty
        };
    }
}
