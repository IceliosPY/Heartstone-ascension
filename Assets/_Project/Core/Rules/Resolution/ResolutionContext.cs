using System;
using System.Collections.Generic;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Effects;
using CoH.Core.State;

namespace CoH.Core.Rules.Resolution
{
    /// <summary>
    /// Runs one resolution from start to finish.
    ///
    /// The shape is always the same:
    ///
    ///   death phase
    ///   -> take the next action off the queue
    ///   -> resolve it, which may queue more work
    ///   -> death phase
    ///   -> repeat until the queue is empty
    ///   -> settle the match result
    ///
    /// Nothing recurses. Every piece of follow-up work goes through the queue,
    /// so the whole resolution is one flat, ordered sequence that can be read,
    /// tested and reproduced.
    /// </summary>
    internal sealed class ResolutionContext
    {
        /// <summary>
        /// Guard against an engine bug looping forever. Set far above anything
        /// a real match could reach, and it throws rather than stopping
        /// quietly: silently truncating a resolution would corrupt the match
        /// instead of reporting the problem.
        /// </summary>
        private const int MaxActions = 10000;

        private const int MaxDeathPhaseRounds = 100;

        private readonly Queue<ResolutionAction> _queue = new Queue<ResolutionAction>();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private readonly List<Entity> _dyingBuffer = new List<Entity>();
        private readonly List<DeathrattleWork> _deathrattleBuffer = new List<DeathrattleWork>();

        public ResolutionContext(GameState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public GameState State { get; }

        /// <summary>Everything that happened, in resolution order.</summary>
        public IReadOnlyList<GameEvent> Events => _events;

        /// <summary>Reports an observable result to whoever asked for this resolution.</summary>
        public void Emit(GameEvent gameEvent)
        {
            _events.Add(gameEvent);
        }

        /// <summary>
        /// Queues follow-up work. Actions are resolved in the order they were
        /// queued, so a caller that queues two actions gets them in that order.
        /// </summary>
        public void Enqueue(ResolutionAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _queue.Enqueue(action);
        }

        /// <summary>
        /// Drains the queue. Safe to call with nothing queued: the death phase
        /// still runs, which is what lets a caller change state directly and
        /// then have the consequences processed properly.
        /// </summary>
        public void Run()
        {
            int resolvedActions = 0;

            while (true)
            {
                RunDeathPhase();

                if (_queue.Count == 0)
                {
                    break;
                }

                if (State.Result != GameResult.InProgress)
                {
                    // The match is decided. Nothing queued behind it happens.
                    _queue.Clear();
                    break;
                }

                resolvedActions++;
                if (resolvedActions > MaxActions)
                {
                    throw new InvalidOperationException(
                        "Resolution did not settle after " + MaxActions +
                        " actions. This is an engine bug, not a legal game state.");
                }

                _queue.Dequeue().Resolve(this);
            }
        }

        /// <summary>
        /// Removes everything that died, all at once.
        ///
        /// Collect first, then remove. Removing as we go would let the first
        /// removal change the board under the feet of the second, which is
        /// exactly the bug that makes two minions trading fail to kill each
        /// other.
        ///
        /// The loop repeats because processing deaths can create new ones: a
        /// deathrattle that summons something into a board sweep, or that
        /// finishes a hero off, produces more work and another pass.
        /// </summary>
        private void RunDeathPhase()
        {
            int rounds = 0;

            while (true)
            {
                CollectPendingDeaths(_dyingBuffer);

                if (_dyingBuffer.Count == 0)
                {
                    break;
                }

                rounds++;
                if (rounds > MaxDeathPhaseRounds)
                {
                    throw new InvalidOperationException(
                        "A death phase did not settle after " + MaxDeathPhaseRounds +
                        " rounds. This is an engine bug, not a legal game state.");
                }

                _dyingBuffer.Sort(DeathOrder.Comparer);

                // Removed first, all of them, and only then are deathrattles
                // queued. A deathrattle therefore sees a board with every one of
                // this phase's dead already gone, rather than a board that is
                // still being cleared around it.
                _deathrattleBuffer.Clear();

                for (int index = 0; index < _dyingBuffer.Count; index++)
                {
                    int boardPosition = RemoveFromPlay(_dyingBuffer[index]);

                    if (_dyingBuffer[index] is Minion dead)
                    {
                        _deathrattleBuffer.Add(new DeathrattleWork(dead, boardPosition));
                    }
                }

                // In the order the deaths were sequenced in, which is oldest
                // first by order of entry, the ordering settled in Phase 3.
                for (int index = 0; index < _deathrattleBuffer.Count; index++)
                {
                    EffectResolver.TriggerDeathrattle(
                        this, State, _deathrattleBuffer[index].Minion, _deathrattleBuffer[index].BoardPosition);
                }

                ContinuousEffects.Recalculate(State);
            }

            SettleResult();
        }

        /// <summary>
        /// Gathers the doomed in a fixed walk of the state: seat one then seat
        /// two, hero then board left to right. No dictionary or hash set is
        /// involved, and the list is sorted afterwards anyway, so the result
        /// cannot depend on memory layout.
        /// </summary>
        private void CollectPendingDeaths(List<Entity> destination)
        {
            destination.Clear();

            for (int seat = 0; seat < State.Players.Count; seat++)
            {
                Player player = State.Players[seat];

                if (player.Hero.IsPendingDeath)
                {
                    destination.Add(player.Hero);
                }

                for (int slot = 0; slot < player.Board.Count; slot++)
                {
                    Minion minion = player.Board[slot];
                    if (minion.IsPendingDeath)
                    {
                        destination.Add(minion);
                    }
                }
            }
        }

        /// <summary>Removes one entity and reports where it stood, or -1.</summary>
        private int RemoveFromPlay(Entity entity)
        {
            if (entity is Minion minion)
            {
                return RemoveMinion(minion);
            }

            if (entity is Hero hero)
            {
                hero.HasDied = true;
                Emit(new HeroDiedEvent(hero.Owner, hero.Id));
            }

            return -1;
        }

        private int RemoveMinion(Minion minion)
        {
            Player controller = State.GetPlayer(minion.Controller);
            int boardPosition = controller.Board.IndexOf(minion);

            controller.Board.Remove(minion);
            minion.Zone = ZoneType.Graveyard;

            // A stolen minion is put to rest by its original owner, as in
            // Hearthstone, which is why the owner's graveyard is used here.
            State.GetPlayer(minion.Owner).Graveyard.TryAdd(minion);

            Emit(new MinionDiedEvent(
                minion.Controller,
                minion.Owner,
                minion.Id,
                minion.CardId,
                boardPosition));

            return boardPosition;
        }

        /// <summary>
        /// A death waiting to have its deathrattle queued, with the place it
        /// happened, which the state has already forgotten.
        /// </summary>
        private readonly struct DeathrattleWork
        {
            public DeathrattleWork(Minion minion, int boardPosition)
            {
                Minion = minion;
                BoardPosition = boardPosition;
            }

            public Minion Minion { get; }

            public int BoardPosition { get; }
        }

        /// <summary>
        /// Decides the match, once, after every death of the phase is done.
        ///
        /// Waiting until here is what makes a mutual kill a draw: both heroes
        /// have already been processed, so neither wins simply for having been
        /// handled first.
        /// </summary>
        private void SettleResult()
        {
            if (State.Result != GameResult.InProgress)
            {
                return;
            }

            bool oneIsDown = State.GetPlayer(PlayerId.One).Hero.HasDied;
            bool twoIsDown = State.GetPlayer(PlayerId.Two).Hero.HasDied;

            if (!oneIsDown && !twoIsDown)
            {
                return;
            }

            if (oneIsDown && twoIsDown)
            {
                State.Result = GameResult.Draw;
            }
            else if (oneIsDown)
            {
                State.Result = GameResult.PlayerTwoWins;
            }
            else
            {
                State.Result = GameResult.PlayerOneWins;
            }

            State.Phase = GamePhase.Ended;
            State.CurrentPlayer = PlayerId.None;

            Emit(new GameEndedEvent(State.Result));
        }
    }
}
