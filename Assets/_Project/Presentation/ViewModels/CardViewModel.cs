using CoH.Core.Cards;
using CoH.Core.Identifiers;

namespace CoH.Presentation
{
    /// <summary>
    /// A flat snapshot of one card, ready to be displayed.
    ///
    /// The view receives this and shows it. It never walks the game state to
    /// work anything out, and above all it never decides
    /// <see cref="IsPlayable"/> for itself: that answer comes from the engine,
    /// so a greyed-out card and a refused command can never disagree.
    /// </summary>
    public readonly struct CardViewModel
    {
        public CardViewModel(
            EntityId entityId,
            CardId cardId,
            string displayName,
            int manaCost,
            int attack,
            int health,
            string rulesText,
            CardType cardType,
            Tribe tribe,
            Rarity rarity,
            bool isPlayable)
        {
            EntityId = entityId;
            CardId = cardId;
            DisplayName = displayName;
            ManaCost = manaCost;
            Attack = attack;
            Health = health;
            RulesText = rulesText;
            CardType = cardType;
            Tribe = tribe;
            Rarity = rarity;
            IsPlayable = isPlayable;
        }

        public EntityId EntityId { get; }

        public CardId CardId { get; }

        public string DisplayName { get; }

        /// <summary>Effective cost, modifiers already applied by the engine.</summary>
        public int ManaCost { get; }

        public int Attack { get; }

        public int Health { get; }

        public string RulesText { get; }

        public CardType CardType { get; }

        public Tribe Tribe { get; }

        public Rarity Rarity { get; }

        /// <summary>Whether the engine would accept playing this card right now.</summary>
        public bool IsPlayable { get; }

        public bool ShowsStatistics => CardType == CardType.Minion || CardType == CardType.Weapon;
    }

    /// <summary>
    /// A flat snapshot of one minion in play.
    ///
    /// <see cref="CanAttack"/> comes from the engine for the same reason
    /// <see cref="CardViewModel.IsPlayable"/> does.
    /// </summary>
    public readonly struct MinionViewModel
    {
        public MinionViewModel(
            EntityId entityId,
            CardId cardId,
            string displayName,
            int attack,
            int currentHealth,
            int maxHealth,
            bool isDamaged,
            bool canAttack)
        {
            EntityId = entityId;
            CardId = cardId;
            DisplayName = displayName;
            Attack = attack;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsDamaged = isDamaged;
            CanAttack = canAttack;
        }

        public EntityId EntityId { get; }

        public CardId CardId { get; }

        public string DisplayName { get; }

        public int Attack { get; }

        public int CurrentHealth { get; }

        public int MaxHealth { get; }

        public bool IsDamaged { get; }

        /// <summary>Whether the engine would accept an attack from this minion.</summary>
        public bool CanAttack { get; }
    }
}
