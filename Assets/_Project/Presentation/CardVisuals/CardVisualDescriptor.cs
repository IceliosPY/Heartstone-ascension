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
            bool faceDown = false,
            CardVisualOverrides overrides = null)
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
            Overrides = overrides;
            OverridesRevision = overrides?.Revision ?? 0;
        }

        /// <summary>
        /// What <see cref="Overrides"/> had been edited to when this was built.
        ///
        /// Only <see cref="LooksTheSameAs"/> reads it, and only to tell "the
        /// same adjustments" from "the same object, since changed".
        /// </summary>
        public int OverridesRevision { get; }

        /// <summary>
        /// Which side of the card is being shown.
        ///
        /// A face down card is still composed rather than hidden behind a lid:
        /// it is the same card with a different set of layers, so the back can
        /// vary by style and class exactly as the front does.
        /// </summary>
        public bool IsFaceDown { get; }

        /// <summary>
        /// What this one card wants done differently from its recipe, or null.
        ///
        /// Resolved data rather than an identity. Whatever built this looked the
        /// card up once, by its id, and what travels on from here is a set of
        /// optional numbers — so there is still nowhere downstream to ask which
        /// card is being drawn, and nowhere to write a special case for one.
        /// </summary>
        public CardVisualOverrides Overrides { get; }

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
            HasRulesText == other.HasRulesText &&
            SameAdjustments(other);

        /// <summary>
        /// Whether two descriptions ask for the same per-card adjustments.
        ///
        /// Two separate sets holding the same rows compose the same card, so
        /// they are compared by content rather than by reference - reference
        /// equality reported a needless difference every time a description was
        /// rebuilt.
        ///
        /// The same object seen twice is the harder case, and content
        /// comparison cannot help with it at all: the editor edits one of these
        /// in place, so the description held from last time and the one being
        /// offered now point at the same object, whose contents are trivially
        /// equal to themselves however much they changed. That is what the
        /// revision stamp is for - it was taken when each description was
        /// built, and it is the only witness that the object moved underneath
        /// them both.
        /// </summary>
        private bool SameAdjustments(in CardVisualDescriptor other) =>
            ReferenceEquals(Overrides, other.Overrides)
                ? OverridesRevision == other.OverridesRevision
                : CardVisualOverrides.SameContent(Overrides, other.Overrides);

        /// <summary>The same card with a different painting.</summary>
        public CardVisualDescriptor With(Sprite newArtwork) =>
            new CardVisualDescriptor(
                Type, Class, Rarity, Tribe, newArtwork, Name, RulesText,
                ManaCost, Attack, Health, ShowsCost, ShowsStatistics,
                Style, SecondaryClass, Expansion, IsFaceDown, Overrides);

        /// <summary>
        /// The same card as its recipe alone would draw it.
        ///
        /// What a polishing tool needs in order to say what a number is being
        /// overridden *from*. Reading the adjusted plan instead would report
        /// each override as though it were inherited, and the figures would
        /// creep every time the panel redrew.
        /// </summary>
        public CardVisualDescriptor WithoutOverrides() =>
            new CardVisualDescriptor(
                Type, Class, Rarity, Tribe, Artwork, Name, RulesText,
                ManaCost, Attack, Health, ShowsCost, ShowsStatistics,
                Style, SecondaryClass, Expansion, IsFaceDown, null);

        /// <summary>The same card, seen from the other side.</summary>
        public CardVisualDescriptor Reversed(bool faceDown) =>
            new CardVisualDescriptor(
                Type, Class, Rarity, Tribe, Artwork, Name, RulesText,
                ManaCost, Attack, Health, ShowsCost, ShowsStatistics,
                Style, SecondaryClass, Expansion, faceDown, Overrides);

        /// <summary>
        /// Builds a description of a card in a hand during a match.
        ///
        /// The overrides are passed on, which is the whole reason this takes
        /// them. They used to be accepted here and then quietly dropped on the
        /// way to the constructor, so every card in a running match composed as
        /// though it had none: the library held them, the factory fetched them,
        /// and this was where they stopped. Nothing failed and nothing was
        /// logged - a polished card simply drew unpolished, and only in the
        /// game, because the editor built its descriptor by another route and
        /// looked correct.
        /// </summary>
        public static CardVisualDescriptor FromViewModel(
            in CardViewModel model,
            Sprite artwork,
            CardVisualStyle style = default,
            string expansion = "",
            CardVisualOverrides overrides = null) =>
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
                secondaryClass: CardClass.Neutral,
                expansion: expansion,
                faceDown: false,
                overrides: overrides);

        public override string ToString() =>
            Type + " / " + Class + " / " + Rarity +
            (HasTribe ? " / " + Tribe : string.Empty) +
            " / " + Style;
    }
}
