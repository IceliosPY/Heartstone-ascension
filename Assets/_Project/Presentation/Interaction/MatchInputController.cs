using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoH.Presentation
{
    /// <summary>
    /// Turns clicks into commands.
    ///
    /// A small explicit state machine rather than a pile of flags, because every
    /// Hearthstone interaction is a state: idle, holding a card, aiming an
    /// attack. Dragging and a targeting arrow will be new states here, not a
    /// rewrite.
    ///
    /// It decides nothing about the rules. Which cards may be played and which
    /// targets are legal both come from the engine; this class only asks, shows
    /// the answer, and sends the intent.
    /// </summary>
    public sealed class MatchInputController : MonoBehaviour
    {
        private enum Mode
        {
            Idle,
            CardSelected,
            AttackerSelected
        }

        [SerializeField] private GameSession session;
        [SerializeField] private MatchPresenter presenter;
        [SerializeField] private MatchHud hud;
        [SerializeField] private Camera matchCamera;
        [SerializeField] private LayerMask clickMask = ~0;

        private Mode _mode = Mode.Idle;
        private EntityId _selected = EntityId.None;
        private readonly List<EntityId> _highlighted = new List<EntityId>();

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
        }

        private void OnDisable()
        {
            if (hud != null)
            {
                hud.EndTurnRequested -= OnEndTurnRequested;
            }
        }

        private void Update()
        {
            if (session == null || !session.IsReady)
            {
                return;
            }

            bool locked = session.IsBusy || session.State.HasEnded;

            if (hud != null)
            {
                hud.SetInteractable(!locked);
            }

            if (locked)
            {
                if (_mode != Mode.Idle)
                {
                    ClearSelection();
                }

                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            OnClick(mouse.position.ReadValue());
        }

        private void OnEndTurnRequested()
        {
            if (session == null || session.IsBusy || session.State.HasEnded)
            {
                return;
            }

            ClearSelection();
            session.Submit(new EndTurnCommand(session.State.CurrentPlayer));
        }

        private void OnClick(Vector2 screenPosition)
        {
            if (matchCamera == null)
            {
                return;
            }

            HandleClick(matchCamera.ScreenPointToRay(screenPosition));
        }

        /// <summary>
        /// The whole click path, from a ray onwards.
        ///
        /// Split out from the mouse so tests can drive exactly what a click
        /// drives. A pointer device does not exist in batch mode, and testing
        /// the pieces around the routing rather than the routing itself is what
        /// let a completely unplayable build pass its tests once already.
        /// </summary>
        internal void HandleClick(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f, clickMask))
            {
                ClearSelection();
                SetHint(string.Empty);
                return;
            }

            LastHit = hit.collider != null ? hit.collider.name : "nothing";

            switch (_mode)
            {
                case Mode.Idle:
                    BeginSelection(hit);
                    break;

                case Mode.CardSelected:
                    ResolveCardClick(hit);
                    break;

                case Mode.AttackerSelected:
                    ResolveAttackClick(hit);
                    break;
            }
        }

        /// <summary>What the last click landed on. Diagnostics only.</summary>
        internal string LastHit { get; private set; } = "none";

        internal bool HasSelection => _mode != Mode.Idle;

        internal EntityId SelectedEntity => _selected;

        private void BeginSelection(RaycastHit hit)
        {
            PlayerId active = session.State.CurrentPlayer;

            CardView card = hit.collider.GetComponentInParent<CardView>();
            if (card != null && card.EntityId != EntityId.None)
            {
                if (!card.IsPlayable)
                {
                    SetHint("That card cannot be played right now.");
                    return;
                }

                _mode = Mode.CardSelected;
                _selected = card.EntityId;
                card.SetSelected(true);
                SetHint("Click your half of the board to play it.");
                return;
            }

            MinionView minion = hit.collider.GetComponentInParent<MinionView>();
            if (minion != null && minion.CanAttack)
            {
                _mode = Mode.AttackerSelected;
                _selected = minion.EntityId;
                minion.SetSelected(true);
                HighlightLegalTargets(active, minion.EntityId);
                SetHint("Click an enemy minion or the enemy hero.");
            }
        }

        private void ResolveCardClick(RaycastHit hit)
        {
            PlayerId active = session.State.CurrentPlayer;

            // The acting player always plays onto the near half of the screen,
            // whichever seat they hold.
            BoardDropZone zone = hit.collider.GetComponentInParent<BoardDropZone>();
            bool ownBoard = zone != null && zone.IsNearSide;

            if (!ownBoard)
            {
                // Clicking the same card again, or anywhere unhelpful, cancels.
                ClearSelection();
                SetHint("");
                return;
            }

            EntityId card = _selected;
            ClearSelection();

            // Rightmost for now: choosing a slot arrives with drag and drop.
            session.Submit(new PlayCardCommand(active, card, PlayCardCommand.Rightmost));
            SetHint("");
        }

        private void ResolveAttackClick(RaycastHit hit)
        {
            PlayerId active = session.State.CurrentPlayer;
            EntityId target = EntityId.None;

            MinionView minion = hit.collider.GetComponentInParent<MinionView>();
            if (minion != null)
            {
                target = minion.EntityId;
            }
            else
            {
                HeroView hero = hit.collider.GetComponentInParent<HeroView>();
                if (hero != null)
                {
                    target = hero.EntityId;
                }
            }

            if (target == EntityId.None || !_highlighted.Contains(target))
            {
                ClearSelection();
                SetHint("");
                return;
            }

            EntityId attacker = _selected;
            ClearSelection();

            session.Submit(new AttackCommand(active, attacker, target));
            SetHint("");
        }

        private void HighlightLegalTargets(PlayerId player, EntityId attacker)
        {
            _highlighted.Clear();
            _highlighted.AddRange(session.GetLegalAttackTargets(player, attacker));

            for (int index = 0; index < _highlighted.Count; index++)
            {
                EntityId id = _highlighted[index];

                if (presenter.TryGetMinionView(id, out MinionView minion))
                {
                    minion.SetTargetable(true);
                }
                else if (presenter.TryGetHeroView(id, out HeroView hero))
                {
                    hero.SetTargetable(true);
                }
            }
        }

        private void ClearSelection()
        {
            if (presenter != null)
            {
                foreach (KeyValuePair<EntityId, MinionView> pair in presenter.MinionViews)
                {
                    if (pair.Value != null)
                    {
                        pair.Value.SetSelected(false);
                        pair.Value.SetTargetable(false);
                    }
                }

                foreach (KeyValuePair<EntityId, CardView> pair in presenter.CardViews)
                {
                    if (pair.Value != null)
                    {
                        pair.Value.SetSelected(false);
                    }
                }

                if (presenter.NearHero != null)
                {
                    presenter.NearHero.SetTargetable(false);
                }

                if (presenter.FarHero != null)
                {
                    presenter.FarHero.SetTargetable(false);
                }
            }

            _highlighted.Clear();
            _selected = EntityId.None;
            _mode = Mode.Idle;
        }

        private void SetHint(string hint)
        {
            if (hud != null)
            {
                hud.SetHint(hint);
            }
        }
    }
}
