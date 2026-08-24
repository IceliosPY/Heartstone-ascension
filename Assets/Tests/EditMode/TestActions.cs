using System.Collections.Generic;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Rules.Resolution;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Internal actions that exist only so tests can build situations no
    /// command can produce yet.
    ///
    /// Playing a card arrives in Phase 4 and combat in Phase 5, so until then
    /// this is how a test makes several characters take damage at the very same
    /// instant. They live in the test assembly, never in the engine.
    /// </summary>
    internal sealed class SimultaneousDamageAction : ResolutionAction
    {
        private readonly List<KeyValuePair<EntityId, int>> _hits;

        private SimultaneousDamageAction(List<KeyValuePair<EntityId, int>> hits)
        {
            _hits = hits;
        }

        public static SimultaneousDamageAction Against(params (EntityId Target, int Amount)[] hits)
        {
            List<KeyValuePair<EntityId, int>> collected = new List<KeyValuePair<EntityId, int>>();
            foreach ((EntityId target, int amount) in hits)
            {
                collected.Add(new KeyValuePair<EntityId, int>(target, amount));
            }

            return new SimultaneousDamageAction(collected);
        }

        /// <summary>
        /// All the damage lands inside this one action, so no death phase can
        /// run in between. That is what makes the hits simultaneous.
        /// </summary>
        public override void Resolve(ResolutionContext context)
        {
            for (int index = 0; index < _hits.Count; index++)
            {
                DamageRules.Deal(context, EntityId.None, _hits[index].Key, _hits[index].Value);
            }
        }
    }

    /// <summary>
    /// Resolves nothing and queues another action, to check that follow-up work
    /// really goes through the queue rather than being called directly.
    /// </summary>
    internal sealed class ChainingAction : ResolutionAction
    {
        private readonly List<ResolutionAction> _next;
        private readonly List<string> _log;
        private readonly string _name;

        public ChainingAction(string name, List<string> log, params ResolutionAction[] next)
        {
            _name = name;
            _log = log;
            _next = new List<ResolutionAction>(next);
        }

        public override void Resolve(ResolutionContext context)
        {
            _log.Add(_name);

            for (int index = 0; index < _next.Count; index++)
            {
                context.Enqueue(_next[index]);
            }
        }
    }
}
