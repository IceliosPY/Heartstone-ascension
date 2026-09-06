using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Queues a Freeze on one character. A thin wrapper over
    /// <see cref="FreezeRules"/>, exactly like <see cref="DealDamageAction"/>
    /// is over <see cref="DamageRules"/> - test support for exercising the
    /// generic mechanism directly, without a card in between.
    /// </summary>
    internal sealed class FreezeAction : ResolutionAction
    {
        private readonly EntityId _sourceId;
        private readonly EntityId _targetId;

        public FreezeAction(EntityId sourceId, EntityId targetId)
        {
            _sourceId = sourceId;
            _targetId = targetId;
        }

        public override void Resolve(ResolutionContext context)
        {
            FreezeRules.Apply(context, _sourceId, _targetId);
        }
    }
}
