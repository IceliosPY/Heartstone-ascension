using System.Collections.Generic;
using System.Linq;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The bug this whole phase exists to prevent: several characters dying at
    /// the same instant, where removing the first quietly stops the second from
    /// dying at all.
    /// </summary>
    public sealed class SimultaneousDeathTests
    {
        private static List<EntityId> DeadIds(IEnumerable<GameEvent> events) =>
            events.OfType<MinionDiedEvent>().Select(death => death.MinionId).ToList();

        [Test]
        public void Two_minions_killed_by_the_same_action_both_die()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion mine = TestFactory.PutMinionOnBoard(engine, PlayerId.One, attack: 3, health: 2);
            Minion theirs = TestFactory.PutMinionOnBoard(engine, PlayerId.Two, attack: 2, health: 3);

            // What a 2/3 attacking a 3/2 will look like in Phase 5: both blows
            // land before anything is removed.
            IReadOnlyList<GameEvent> events = TestFactory.DamageTogether(
                engine,
                (mine.Id, 2),
                (theirs.Id, 3));

            Assert.That(mine.IsInPlay, Is.False, "The first minion died.");
            Assert.That(theirs.IsInPlay, Is.False, "The second must die too, not survive the first's removal.");
            Assert.That(DeadIds(events), Has.Count.EqualTo(2));
            Assert.That(engine.State.GetPlayer(PlayerId.One).Board.Count, Is.EqualTo(0));
            Assert.That(engine.State.GetPlayer(PlayerId.Two).Board.Count, Is.EqualTo(0));
        }

        [Test]
        public void Both_deaths_are_reported_in_the_same_resolution()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion first = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1);
            Minion second = TestFactory.PutMinionOnBoard(engine, PlayerId.Two, health: 1);

            IReadOnlyList<GameEvent> events = TestFactory.DamageTogether(engine, (first.Id, 1), (second.Id, 1));

            // Both damages, then both deaths: no death is reported before every
            // hit of the action has landed.
            List<string> names = events.Select(e => e.GetType().Name).ToList();
            Assert.That(names, Is.EqualTo(new List<string>
            {
                nameof(DamageDealtEvent),
                nameof(DamageDealtEvent),
                nameof(MinionDiedEvent),
                nameof(MinionDiedEvent)
            }));
        }

        [Test]
        public void Three_simultaneous_deaths_are_ordered_by_play_order()
        {
            GameEngine engine = TestFactory.StartedMatch();

            // Summoned oldest first, then placed so that board order and play
            // order disagree: the oldest sits on the right.
            Minion oldest = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1);
            Minion middle = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1, position: 0);
            Minion newest = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1, position: 0);

            Zone<Minion> board = engine.State.GetPlayer(PlayerId.One).Board;
            Assert.That(board[0], Is.SameAs(newest), "Board order is the reverse of play order here.");

            IReadOnlyList<GameEvent> events = TestFactory.DamageTogether(
                engine,
                (newest.Id, 1),
                (middle.Id, 1),
                (oldest.Id, 1));

            Assert.That(DeadIds(events), Is.EqualTo(new List<EntityId> { oldest.Id, middle.Id, newest.Id }),
                "Deaths resolve in the order the minions entered play, not in board order.");
        }

        [Test]
        public void The_order_does_not_depend_on_the_order_the_damage_was_dealt()
        {
            GameEngine left = TestFactory.StartedMatch(seed: 31UL);
            GameEngine right = TestFactory.StartedMatch(seed: 31UL);

            Minion leftA = TestFactory.PutMinionOnBoard(left, PlayerId.One, health: 1);
            Minion leftB = TestFactory.PutMinionOnBoard(left, PlayerId.Two, health: 1);

            Minion rightA = TestFactory.PutMinionOnBoard(right, PlayerId.One, health: 1);
            Minion rightB = TestFactory.PutMinionOnBoard(right, PlayerId.Two, health: 1);

            List<EntityId> leftOrder = DeadIds(
                TestFactory.DamageTogether(left, (leftA.Id, 1), (leftB.Id, 1)));

            // Same board, damage listed the other way round.
            List<EntityId> rightOrder = DeadIds(
                TestFactory.DamageTogether(right, (rightB.Id, 1), (rightA.Id, 1)));

            Assert.That(rightOrder, Is.EqualTo(leftOrder));
        }

        [Test]
        public void A_board_clear_removes_everything_in_one_phase()
        {
            GameEngine engine = TestFactory.StartedMatch();
            List<EntityId> everyone = new List<EntityId>();

            for (int index = 0; index < 3; index++)
            {
                everyone.Add(TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 5).Id);
                everyone.Add(TestFactory.PutMinionOnBoard(engine, PlayerId.Two, health: 5).Id);
            }

            IReadOnlyList<GameEvent> events = TestFactory.Destroy(engine, everyone.ToArray());

            Assert.That(DeadIds(events), Has.Count.EqualTo(6));
            Assert.That(engine.State.GetPlayer(PlayerId.One).Board.Count, Is.EqualTo(0));
            Assert.That(engine.State.GetPlayer(PlayerId.Two).Board.Count, Is.EqualTo(0));
            Assert.That(DeadIds(events), Is.Ordered.Using<EntityId>((a, b) => a.CompareTo(b)),
                "Play order and id order coincide here, so deaths come out ordered.");
        }

        [Test]
        public void The_same_situation_always_produces_the_same_death_order()
        {
            List<string> Run(ulong seed)
            {
                GameEngine engine = TestFactory.StartedMatch(seed);
                List<EntityId> targets = new List<EntityId>();

                for (int index = 0; index < 4; index++)
                {
                    targets.Add(TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1).Id);
                    targets.Add(TestFactory.PutMinionOnBoard(engine, PlayerId.Two, health: 1).Id);
                }

                var hits = targets.Select(id => (Target: id, Amount: 1)).ToArray();
                return TestFactory.DamageTogether(engine, hits)
                    .OfType<MinionDiedEvent>()
                    .Select(death => death.ToString())
                    .ToList();
            }

            Assert.That(Run(77UL), Is.EqualTo(Run(77UL)));
        }
    }
}
