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
    /// The queue itself: work is resolved in order, follow-up work goes through
    /// the queue rather than through recursion, and the whole thing settles.
    /// </summary>
    public sealed class ResolutionPipelineTests
    {
        [Test]
        public void An_empty_queue_settles_immediately()
        {
            GameEngine engine = TestFactory.StartedMatch();

            IReadOnlyList<GameEvent> events = engine.ResolvePending();

            Assert.That(events, Is.Empty);
            Assert.That(engine.State.Result, Is.EqualTo(GameResult.InProgress));
        }

        [Test]
        public void An_action_can_queue_the_next_one()
        {
            GameEngine engine = TestFactory.StartedMatch();
            List<string> log = new List<string>();

            engine.Resolve(new ChainingAction("first", log,
                new ChainingAction("second", log,
                    new ChainingAction("third", log))));

            Assert.That(log, Is.EqualTo(new List<string> { "first", "second", "third" }));
        }

        [Test]
        public void Queued_work_is_resolved_in_the_order_it_was_queued()
        {
            GameEngine engine = TestFactory.StartedMatch();
            List<string> log = new List<string>();

            engine.Resolve(new ChainingAction("root", log,
                new ChainingAction("a", log),
                new ChainingAction("b", log),
                new ChainingAction("c", log)));

            Assert.That(log, Is.EqualTo(new List<string> { "root", "a", "b", "c" }));
        }

        [Test]
        public void Work_queued_by_a_child_comes_after_its_siblings()
        {
            GameEngine engine = TestFactory.StartedMatch();
            List<string> log = new List<string>();

            // "a" queues "a.child" while "b" is still waiting: the queue is
            // first in, first out, so "b" goes before "a.child".
            engine.Resolve(new ChainingAction("root", log,
                new ChainingAction("a", log, new ChainingAction("a.child", log)),
                new ChainingAction("b", log)));

            Assert.That(log, Is.EqualTo(new List<string> { "root", "a", "b", "a.child" }));
        }

        [Test]
        public void A_death_phase_runs_between_two_queued_actions()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion doomed = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1);
            List<string> log = new List<string>();

            IReadOnlyList<GameEvent> events = engine.Resolve(new ChainingAction("root", log,
                SimultaneousDamageAction.Against((doomed.Id, 1)),
                new ChainingAction("after", log)));

            // The minion is already gone by the time "after" resolves.
            Assert.That(events.OfType<MinionDiedEvent>().Count(), Is.EqualTo(1));
            Assert.That(doomed.IsInPlay, Is.False);
            Assert.That(log, Is.EqualTo(new List<string> { "root", "after" }));
        }

        [Test]
        public void A_later_action_can_trigger_a_fresh_death_phase()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion first = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 1);
            Minion second = TestFactory.PutMinionOnBoard(engine, PlayerId.Two, health: 1);
            List<string> log = new List<string>();

            IReadOnlyList<GameEvent> events = engine.Resolve(new ChainingAction("root", log,
                SimultaneousDamageAction.Against((first.Id, 1)),
                SimultaneousDamageAction.Against((second.Id, 1))));

            List<string> names = events.Select(e => e.GetType().Name).ToList();

            // Two separate death phases, one after each damaging action.
            Assert.That(names, Is.EqualTo(new List<string>
            {
                nameof(DamageDealtEvent),
                nameof(MinionDiedEvent),
                nameof(DamageDealtEvent),
                nameof(MinionDiedEvent)
            }));
        }

        [Test]
        public void Nothing_queued_behind_the_end_of_the_match_is_resolved()
        {
            GameEngine engine = TestFactory.StartedMatch();
            List<string> log = new List<string>();
            EntityId heroId = engine.State.GetPlayer(PlayerId.One).Hero.Id;

            engine.Resolve(new ChainingAction("root", log,
                SimultaneousDamageAction.Against((heroId, 30)),
                new ChainingAction("never", log)));

            Assert.That(engine.State.Result, Is.EqualTo(GameResult.PlayerTwoWins));
            Assert.That(log, Is.EqualTo(new List<string> { "root" }), "Work behind a finished match is dropped.");
        }

        [Test]
        public void A_long_but_finite_chain_settles()
        {
            GameEngine engine = TestFactory.StartedMatch();
            List<string> log = new List<string>();

            ChainingAction chain = new ChainingAction("step0", log);
            for (int index = 1; index < 200; index++)
            {
                chain = new ChainingAction("step" + index, log, chain);
            }

            engine.Resolve(chain);

            Assert.That(log, Has.Count.EqualTo(200));
            Assert.That(log.First(), Is.EqualTo("step199"));
            Assert.That(log.Last(), Is.EqualTo("step0"));
        }

        [Test]
        public void The_same_seed_and_the_same_pipeline_work_produce_the_same_events()
        {
            List<string> Run(ulong seed)
            {
                GameEngine engine = TestFactory.StartedMatch(seed);
                Minion a = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 2);
                Minion b = TestFactory.PutMinionOnBoard(engine, PlayerId.Two, health: 2);
                Minion c = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 4);

                List<GameEvent> all = new List<GameEvent>();
                all.AddRange(TestFactory.DamageTogether(engine, (a.Id, 2), (b.Id, 2)));
                all.AddRange(TestFactory.Damage(engine, c.Id, 4));
                all.AddRange(TestFactory.EndTurn(engine).Events);

                return all.Select(e => e.ToString()).ToList();
            }

            Assert.That(Run(555UL), Is.EqualTo(Run(555UL)));
        }
    }
}
