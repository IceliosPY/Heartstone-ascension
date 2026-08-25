using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CoH.Core.Events;
using CoH.Core.Identifiers;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// What the engine reported, written down so two runs can be compared.
    ///
    /// A match can reach the same final state by a different route, and that
    /// still matters: the presentation animates the route, and triggers will
    /// one day fire along it. So the events are compared as well as the state,
    /// and a replay that ends correctly but reports a different sequence is
    /// still a divergence worth stopping on.
    ///
    /// A written switch rather than reflection over properties. Reflection
    /// would silently start including a field the day someone adds one, and
    /// silently reorder them, which is exactly the kind of instability a
    /// fingerprint cannot have. Adding an event type here is a deliberate line
    /// of code, and forgetting it produces a visible "unknown event" rather
    /// than a quietly weaker comparison.
    /// </summary>
    public static class EventFingerprint
    {
        /// <summary>One line describing one event, payload included.</summary>
        public static string Describe(GameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                throw new ArgumentNullException(nameof(gameEvent));
            }

            switch (gameEvent)
            {
                case GameStartedEvent started:
                    return "GameStarted first=" + Seat(started.StartingPlayer) +
                           " seed=" + started.Seed.ToString(CultureInfo.InvariantCulture);

                case MulliganStartedEvent _:
                    return "MulliganStarted";

                case MulliganResolvedEvent resolved:
                    return "MulliganResolved p=" + Seat(resolved.PlayerId) +
                           " replaced=" + Number(resolved.ReplacedCount);

                case TurnStartedEvent turnStarted:
                    return "TurnStarted p=" + Seat(turnStarted.PlayerId) +
                           " turn=" + Number(turnStarted.TurnNumber) +
                           " taken=" + Number(turnStarted.TurnsTakenByPlayer);

                case TurnEndedEvent turnEnded:
                    return "TurnEnded p=" + Seat(turnEnded.PlayerId) +
                           " turn=" + Number(turnEnded.TurnNumber);

                case ManaCrystalGainedEvent gained:
                    return "ManaCrystalGained p=" + Seat(gained.PlayerId) +
                           " max=" + Number(gained.MaxMana);

                case ManaRefilledEvent refilled:
                    return "ManaRefilled p=" + Seat(refilled.PlayerId) +
                           " mana=" + Number(refilled.AvailableMana) +
                           "/" + Number(refilled.MaxMana);

                case ManaSpentEvent spent:
                    return "ManaSpent p=" + Seat(spent.PlayerId) +
                           " amount=" + Number(spent.Amount) +
                           " left=" + Number(spent.RemainingMana);

                case CardDrawnEvent drawn:
                    return "CardDrawn p=" + Seat(drawn.PlayerId) +
                           " card=" + Id(drawn.CardInstanceId) +
                           " def=" + drawn.CardId.Value +
                           " deck=" + Number(drawn.CardsLeftInDeck);

                case CardBurnedEvent burned:
                    return "CardBurned p=" + Seat(burned.PlayerId) +
                           " card=" + Id(burned.CardInstanceId) +
                           " def=" + burned.CardId.Value +
                           " deck=" + Number(burned.CardsLeftInDeck);

                case CardGeneratedEvent generated:
                    return "CardGenerated p=" + Seat(generated.PlayerId) +
                           " card=" + Id(generated.CardInstanceId) +
                           " def=" + generated.CardId.Value;

                case CardPlayedEvent played:
                    return "CardPlayed p=" + Seat(played.PlayerId) +
                           " card=" + Id(played.CardInstanceId) +
                           " def=" + played.CardId.Value +
                           " target=" + Id(played.TargetId);

                case MinionSummonedEvent summoned:
                    return "MinionSummoned p=" + Seat(summoned.Controller) +
                           " minion=" + Id(summoned.MinionId) +
                           " def=" + summoned.CardId.Value +
                           " slot=" + Number(summoned.BoardPosition);

                case AttackDeclaredEvent declared:
                    return "AttackDeclared p=" + Seat(declared.AttackingPlayer) +
                           " attacker=" + Id(declared.AttackerId) +
                           " target=" + Id(declared.TargetId);

                case DamageDealtEvent damage:
                    return "DamageDealt source=" + Id(damage.SourceId) +
                           " target=" + Id(damage.TargetId) +
                           " controller=" + Seat(damage.TargetController) +
                           " amount=" + Number(damage.Amount) +
                           " armor=" + Number(damage.AbsorbedByArmor) +
                           " hp=" + Number(damage.RemainingHealth) +
                           " left=" + Number(damage.RemainingArmor);

                case FatigueDamageEvent fatigue:
                    return "FatigueDamage p=" + Seat(fatigue.PlayerId) +
                           " amount=" + Number(fatigue.Amount);

                case MinionDiedEvent died:
                    return "MinionDied controller=" + Seat(died.Controller) +
                           " owner=" + Seat(died.Owner) +
                           " minion=" + Id(died.MinionId) +
                           " def=" + died.CardId.Value +
                           " slot=" + Number(died.BoardPosition);

                case HeroDiedEvent heroDied:
                    return "HeroDied p=" + Seat(heroDied.PlayerId) +
                           " hero=" + Id(heroDied.HeroId);

                case GameEndedEvent ended:
                    return "GameEnded result=" + ended.Result;

                default:
                    // Visible rather than silent. A new event type that nobody
                    // described here would otherwise weaken every comparison
                    // without anyone noticing.
                    return "UNKNOWN " + gameEvent.GetType().Name;
            }
        }

        /// <summary>The whole batch, one event per line.</summary>
        public static string Describe(IReadOnlyList<GameEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            StringBuilder text = new StringBuilder();

            for (int index = 0; index < events.Count; index++)
            {
                text.Append(Describe(events[index])).Append('\n');
            }

            return text.ToString();
        }

        public static string Of(IReadOnlyList<GameEvent> events) => StableHash.Hex(Describe(events));

        /// <summary>Just the type names, for a compact history.</summary>
        public static string[] TypesOf(IReadOnlyList<GameEvent> events)
        {
            if (events == null)
            {
                return Array.Empty<string>();
            }

            string[] names = new string[events.Count];

            for (int index = 0; index < events.Count; index++)
            {
                names[index] = events[index].GetType().Name;
            }

            return names;
        }

        private static string Seat(PlayerId id) => id.IsNone ? "none" : Number(id.Number);

        private static string Id(EntityId id) => id.IsNone ? "-" : "#" + Number(id.Value);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
