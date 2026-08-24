using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Queues damage to one character for later.
    ///
    /// A thin wrapper over <see cref="DamageRules"/>, for damage that genuinely
    /// happens as its own step, such as fatigue. Anything that must land at the
    /// same instant as something else calls DamageRules directly instead, since
    /// a death phase runs between queued actions.
    /// </summary>
    internal sealed class DealDamageAction : ResolutionAction
    {
        private readonly EntityId _sourceId;
        private readonly EntityId _targetId;
        private readonly int _amount;

        public DealDamageAction(EntityId sourceId, EntityId targetId, int amount)
        {
            _sourceId = sourceId;
            _targetId = targetId;
            _amount = amount;
        }

        public override void Resolve(ResolutionContext context)
        {
            DamageRules.Deal(context, _sourceId, _targetId, _amount);
        }
    }
}
