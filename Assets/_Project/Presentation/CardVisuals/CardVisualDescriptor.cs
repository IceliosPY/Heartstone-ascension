using CoH.Core.Cards;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Everything needed to decide what a card looks like, and nothing else.
    ///
    /// This is the request the composer answers. It is deliberately not a
    /// <see cref="CardViewModel"/>: a view model is a snapshot of a card in a
    /// match, and most of what a card looks like has nothing to do with a match.
    /// The preview tool in the editor builds one of these out of nothing, and
    /// gets exactly the picture the game would draw, because there is only one
    /// road from here to a finished card.
    ///
    /// Fields the project cannot yet fill are still declared, because an empty
    /// field costs a line and a missing one costs a redesign. A style, an
    /// expansion emblem and a second class are all read by the composer today
    /// and simply resolve to nothing until assets exist for them.
    /// </summary>
    public readonly struct CardVisualDescriptor
    {
        public CardVisualDescriptor(
            CardType type,
            CardClass cardClass,
            Rarity rarity = Rarity.Free,
            Tribe tribe = Tribe.None,
            Sprite artwork = null,
            string name = "",
            string rulesText = "",
            int manaCost = 0,
            int attack = 0,
            int health = 0,
            bool showsCost = true,
            bool showsStatistics = false,
            CardVisualStyle style = default,
            CardClass secondaryClass = CardClass.Neutral,
            string expansion = "",
            bool faceDown = false)
        {
            IsFaceDown = faceDown;
            Type = type;
            Class = cardClass;
            SecondaryClass = secondaryClass;
            Rarity = rarity;
            Tribe = tribe;
            Artwork = artwork;
            Name = name ?? string.Empty;
            RulesText = rulesText ?? string.Empty;
            ManaCost = manaCost;
            Attack = attack;
            Health = health;
            ShowsCost = showsCost;
            ShowsStatistics = showsStatistics;
            Style = style.IsNone ? CardVisualStyle.Default : style;
            Expansion = expansion ?? string.Empty;
        }

        /// <summary>
        /// Which side of the card is being shown.
        ///
        /// A face down card is still composed rather than hidden behind a lid:
        /// it is the same card with a different set of layers, so the back can
        /// vary by style and class exactly as the front does.
        /// </summary>
        public bool IsFaceDown { get; }

        public CardType Type { get; }

        public CardClass Class { get; }

        /// <summary>
        /// The other half of a dual class card, or <see cref="CardClass.Neutral"/>.
        ///
        /// Nothing produces one yet. It is here because a class stored as a
        /// single value is the kind of decision that is cheap now and expensive
        /// in a year, and because the catalog can already be asked for a second
        /// class without knowing what it will be used for.
        /// </summary>
        public CardClass SecondaryClass { get; }

        public Rarity Rarity { get; }

        public Tribe Tribe { get; }

        /// <summary>
        /// The painting, supplied per card rather than looked up by kind. One
        /// frame serves any number of these, which is the whole reason artwork
        /// is not part of the catalog.
        /// </summary>
        public Sprite Artwork { get; }

        public string Name { get; }

        public string RulesText { get; }

        /// <summary>Effective cost: the engine has already applied any change to it.</summary>
        public int ManaCost { get; }

        public int Attack { get; }

        public int Health { get; }

        public bool ShowsCost { get; }

        /// <summary>Whether this card prints an attack and a health at all.</summary>
        public bool ShowsStatistics { get; }

        /// <summary>Which family of components to compose from.</summary>
        public CardVisualStyle Style { get; }

        /// <summary>Set symbol identifier, or empty for none.</summary>
        public string Expansion { get; }

        public bool HasArtwork => Artwork != null;

        public bool HasTribe => Tribe != Tribe.None;

        public bool HasRulesText => !string.IsNullOrEmpty(RulesText);

        /// <summary>Whether this card gets the legendary treatment.</summary>
        public bool IsElite => Rarity == Rarity.Legendary;

        /// <summary>
        /// Whether two descriptions would compose to the same stack of pictures.
        ///
        /// Only the fields the catalog and the conditions read. The numbers and
        /// the words are excluded on purpose: a minion being buffed from 2/3 to
        /// 4/5 changes two labels and not one sprite, and re-resolving the whole
        /// card to discover that would be work done for nothing every time
        /// anything on the board moved.
        /// </summary>
        public bool LooksTheSameAs(in CardVisualDescriptor other) =>
            Type == other.Type &&
            Class == other.Class &&
            SecondaryClass == other.SecondaryClass &&
            Rarity == other.Rarity &&
            Tribe == other.Tribe &&
            Artwork == other.Artwork &&
            ShowsCost == other.ShowsCost &&
            ShowsStatistics == other.ShowsStatistics &&
            IsFaceDown == other.IsFaceDown &&
            Style.Equals(other.Style) &&
            string.Equals(Expansion, other.Expansion, System.StringComparison.Ordinal) &&
            HasRulesText == other.HasRulesText;

        /// <summary>The same card with a different painting.</summary>
        public CardVisualDescriptor With(Sprite newArtwork) =>
            new CardVisualDescriptor(
                Type, Class, Rarity, Tribe, newArtwork, Name, RulesText,
                ManaCost, Attack, Health, ShowsCost, ShowsStatistics,
                Style, SecondaryClass, Expansion, IsFaceDown);

        /// <summary>The same card, seen from the other side.</summary>
        public CardVisualDescriptor Reversed(bool faceDown) =>
            new CardVisualDescriptor(
                Type, Class, Rarity, Tribe, Artwork, Name, RulesText,
                ManaCost, Attack, Health, ShowsCost, ShowsStatistics,
                Style, SecondaryClass, Expansion, faceDown);

        /// <summary>Builds a description of a card in a hand during a match.</summary>
        public static CardVisualDescriptor FromViewModel(
            in CardViewModel model, Sprite artwork, CardVisualStyle style = default, string expansion = "") =>
            new CardVisualDescriptor(
                model.CardType,
                model.CardClass,
                model.Rarity,
                model.Tribe,
                artwork,
                model.DisplayName,
                model.RulesText,
                model.ManaCost,
                model.Attack,
                model.Health,
                showsCost: true,
                showsStatistics: model.ShowsStatistics,
                style: style,
                expansion: expansion);

        public override string ToString() =>
            Type + " / " + Class + " / " + Rarity +
            (HasTribe ? " / " + Tribe : string.Empty) +
            " / " + Style;
    }
}
