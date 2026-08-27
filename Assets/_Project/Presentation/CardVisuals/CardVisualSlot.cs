namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// One place on a card that a picture can go.
    ///
    /// A slot is a question ("what frame does this card use?"), never an answer.
    /// The catalog turns a slot plus a card's description into one sprite, and
    /// the recipe decides where that sprite sits and whether it appears at all.
    ///
    /// Adding a slot here is how the card grows a new kind of component. It is
    /// deliberately a long, flat list rather than a hierarchy: a card is a stack
    /// of pictures, and pretending otherwise buys nothing.
    /// </summary>
    public enum CardVisualSlot
    {
        /// <summary>Not a picture at all. Used by layers that only carry text.</summary>
        None = 0,

        /// <summary>The drop shadow and whatever sits behind the frame.</summary>
        Backdrop = 1,

        /// <summary>
        /// The card's artwork. Supplied per card rather than looked up by kind,
        /// so the same frame serves any number of paintings.
        /// </summary>
        Artwork = 2,

        /// <summary>
        /// The main frame: the one picture that says minion or spell, and which
        /// class it belongs to.
        ///
        /// In a real card set this single image usually carries the name banner
        /// and the rules panel baked into it, which is why those have slots of
        /// their own that are allowed to stay empty.
        /// </summary>
        Frame = 3,

        /// <summary>The legendary treatment laid over the frame.</summary>
        EliteFrame = 4,

        /// <summary>Only used when the frame does not already draw one.</summary>
        NameBanner = 5,

        /// <summary>Likewise: the panel the rules text is printed on.</summary>
        RulesPanel = 6,

        ManaGem = 7,
        AttackGem = 8,
        HealthGem = 9,

        /// <summary>The small stone that says common, rare, epic or legendary.</summary>
        RarityGem = 10,

        /// <summary>The plaque a minion's tribe is printed on.</summary>
        TribeBanner = 11,

        /// <summary>The set symbol.</summary>
        ExpansionEmblem = 12,

        /// <summary>The back of the card, for a hand nobody is allowed to read.</summary>
        CardBack = 13,

        /// <summary>
        /// The shape the artwork is clipped to.
        ///
        /// Never drawn. It is a picture only so that it can be authored,
        /// resolved and overridden exactly like every other component: a minion
        /// window is an oval and a spell window is a rectangle, and that is a
        /// fact about a kind of card rather than a branch in a renderer.
        /// </summary>
        ArtworkMask = 14
    }
}
