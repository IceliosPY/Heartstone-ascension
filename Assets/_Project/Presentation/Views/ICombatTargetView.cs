using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Something an attack can land on.
    ///
    /// Minions and heroes are the same thing as far as damage is concerned, and
    /// the engine already treats them that way: one DamageDealtEvent covers
    /// both. This is that idea on the presentation side, so the combat sequence
    /// aims, hits and updates a number without ever asking which kind of thing
    /// it is hitting.
    ///
    /// It carries no rule. Whether the hit is legal was settled long before, and
    /// the numbers arrive already decided.
    /// </summary>
    public interface ICombatTargetView
    {
        /// <summary>Where an attacker should aim, in world space.</summary>
        Vector3 ImpactPoint { get; }

        /// <summary>
        /// Shows the numbers as they are at the moment of the hit, which is not
        /// the same as what the engine currently holds.
        /// </summary>
        void ShowDamage(int remainingHealth, int remainingArmor);

        /// <summary>A recoil and a flash. Runs on the view and is not waited on.</summary>
        void PlayHitFeedback(float duration);
    }
}
