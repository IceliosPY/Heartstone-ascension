# Card visuals

How a card gets its appearance, without a prefab being made for it.

```
CardVisualDescriptor  →  CardVisualFactory  →  CardVisualPlan  →  CardVisualPainter
     what the card is        recipe + catalog       the layers          the objects
```

One `CardView` shows a neutral minion, a spell, a legendary and a weapon. There
is no second prefab, no branch on card type, and no card id anywhere in the
decision. The composer is to the presentation what `EffectDefinition` is to the
rules.

---

## The five pieces

| | |
|---|---|
| **Descriptor** | What a card *is*: type, class, rarity, tribe, style, artwork, numbers, words. Not which card it is |
| **Recipe** | Which layers a card can have, where each sits, and when it applies |
| **Catalog** | Which picture fills a slot, for a card like this |
| **Library** | Which painting belongs to this particular card |
| **Painter** | The only thing that touches a GameObject |

The first four are data. The composer between them is a static function of its
arguments, which is why it can be tested without a scene and why the editor
preview cannot drift away from the game.

---

## How a card asks for its appearance

```csharp
CardVisualDescriptor card = factory.Describe(model);   // + artwork, style
factory.Compose(card, plan);                           // → layers
painter.Apply(plan);                                   // → renderers
```

`CardView.Bind` does exactly that, and nothing else. The preview window and the
capture tool do exactly that too — same factory, same plan, same painter, a
different camera.

**A card id reaches the artwork library and stops there.** Mapping an id to a
painting is that file's entire job; every other decision is made from what the
card *is*. A test walks the source of the composer, the catalog, the recipe and
the painter and fails if any of them so much as mentions `CardId`.

---

## Resolving a slot

Every entry in the catalog opts in to the constraints it cares about. An entry
constraining nothing is the default for its slot; an entry constraining type and
class is the picture for that pair.

```
Frame                        → any card that gets this far
Frame + Minion               → beats it for a minion
Frame + Minion + Neutral     → beats that for a neutral minion
```

Asking for a frame scores every entry for that slot and takes the most specific
one that applies. So:

- **overriding** is authoring a more specific entry, and nothing else;
- **falling back** is the more specific entry not existing;
- there is no combination to enumerate, and nothing to explode.

### The order of precedence

```
type  16      the whole shape of the card
style  8      which family of components it is drawn from
class  4      recolours it
rarity 2      changes a gem and a border
tribe  1      changes one plaque
```

Each weight is larger than the sum of everything below it, so this is a strict
priority rather than a vote: no number of small constraints outranks one big
one. A legendary mage minion asked for a frame prefers *mage minion* over
*legendary*, because the type and class decide what the card is and the rarity
decorates it.

### Two rules that keep it honest

**Nothing is ever chosen arbitrarily.** When no entry applies, the answer is
"missing" — not "the first one in the list". The layer is skipped and the
composer records the gap.

**Ambiguity is an authoring mistake, not a coin toss.** Two equally specific
entries for one slot mean the card's appearance depends on list order, so the
validator reports it.

---

## Missing pictures

Most layers are optional, and a missing one is silence. A frame that draws its
own name banner leaves that slot empty on purpose; a set symbol nobody has drawn
is not a fault; a spell has no health gem.

Layers marked **required** are the exception. A missing one is collected into
`plan.Gaps` — the card still draws, with the hole, and the report names the file
that would fill it. A missing picture is never an exception in the middle of a
match.

---

## Layers

Authored in pixels of an **800 × 1100 canvas**, the proportions the project's
card layout was measured on. Nothing in a recipe is in world units: the recipe
says what the card *is*, and the layout says how big it is. That separation is
what will let a hand, an inspect view and a future collection share one
composition at three sizes.

```
  0  Backdrop           120  CardBack (face down)
 10  Artwork            130  Name
 20  Frame              140  Mana cost
 30  EliteFrame         150  Rules text
 40  NameBanner         160  Attack
 50  RulesPanel         170  Health
 60  ManaGem            180  Tribe
 70  AttackGem
 80  HealthGem
 90  RarityGem
100  TribeBanner
110  ExpansionEmblem
```

Artwork sits **under** the frame, because a frame is a window rather than a
picture.

Gaps of ten, so a layer can be slid between two without renumbering. Two layers
at the same depth are reported: which draws in front would otherwise depend on
list order, which nobody looking at the card can see.

### Conditions

A layer carries its own reasons for appearing:

```
RarityGem     Rarity     ≠  Free
RulesPanel    HasRulesText  =  true
AttackGem     ShowsStatistics = true
EliteFrame    IsElite       =  true
```

The alternative is the chain every card renderer grows — if minion, else if
spell, and inside each of those a chain for class and another for rarity. Here
the composer never learns what any condition means, and an artist adds a layer
without touching code.

**Face up and face down** is a mode on the layer rather than a condition,
because it is which side of the card you are looking at rather than something
about the card. Written as a condition it would have to be repeated, negated, on
every other layer, and the first one somebody forgot would print a mana cost on
the back of an opponent's card.

---

## Runtime values

A card in a match changes constantly and almost never changes appearance. So
`CardView.Show` takes one of two paths:

- the card is now a **different card** — type, class, rarity, artwork, style —
  and is composed again;
- the card is the **same card with different numbers**, and only its labels are
  rewritten.

`CardVisualDescriptor.LooksTheSameAs` is the test, and it deliberately ignores
the numbers and the words. A minion buffed from 2/3 to 4/5 is the same pictures
in the same order, and re-resolving the catalog to discover that would be work
done every time anything on the board moved.

---

## Adding things

**A new card.** Author the `CardDefinitionAsset` as always, add a row to the
artwork library for its painting, and it composes. No Presentation code.

**A new picture.** Drop the file in, add a catalog row, constrain it to the
cards it applies to. No code.

**A new class.** Append a value to `CardClass` in the engine — it is a gameplay
fact, not a decoration, and the rule there is append-only, never renumber. Then
one catalog row per component that differs. The composer needs no change; it has
never known what a class is.

**A new card type.** Append to `CardType`, add a frame row. Visual support for a
type is not gameplay support for it: `Weapon` draws today and has no rules
behind it.

**A new style.** A second recipe with a different style, and catalog rows
constrained to it. A card selects one through its library entry.

**A new slot.** A value on `CardVisualSlot`, a layer in the recipe, entries in
the catalog. This is the only one that touches code, and it is one enum member.

---

## The tools

| | |
|---|---|
| **Rebuild Card Visuals** | Regenerates the recipe, catalog, library and factory |
| **Report Card Visual Coverage** | What is missing, and what is falling back |
| **Capture Card Variants** | One image per variant, into `CardCaptures/` |
| **Tools → Card Visual Preview** | Any combination, interactively |

All four compose through the same factory. The preview is not an approximation
of what the game will draw; it is the same composition rendered by the same
code.

---

## What is deliberately not here

Dual class, card styles beyond one, expansion emblems and hero frames are all
**declared and unimplemented**: the descriptor carries them, the catalog can be
constrained on them, and nothing fills them yet. That is the difference between
a field costing a line now and a redesign later.

The sprites in `Assets/_Project/Art/CardVisuals/Placeholder/` are scaffolding —
flat, grey and ugly on purpose, so nobody mistakes them for a decision about how
the game should look. They exist so the architecture could be finished and
tested before the intended artwork arrived. See
`Assets/ThirdParty/HearthCards/README.md` for what replaces them.

No deck builder, no collection, no card creator for players. The preview window
is a development tool.
