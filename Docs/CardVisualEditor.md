# Card visual editor

How a card's appearance is authored, and — the point of the whole arrangement —
what you have to touch when the game gains a new visual capability.

## The short answer

> **If we add a completely new card visual element six months from now, what
> steps are required?**

Add it to the data, and — for a property — wire it through to the renderer.

- A new **property** on a layer or a style: add the field, add a line reading it
  where it should take effect, and decide whether one card may differ on it. It
  then appears in the editor with its tooltip and its slider. **No editor code.**
- A new **layer**: add a row to the recipe with an `id`, and the conditions that
  select it. It appears in the layer list for every card those conditions admit.
  **No editor code.**
- A new **card type**: add it to `CardType`, then give the recipe layers
  conditions that mention it. The editor lists whatever that card draws. **No
  editor code.**
- A genuinely new **kind of value** — something that is not a number, a
  boolean, an enumeration, a string, a colour, a vector or a rectangle: one
  entry in `CardVisualPropertyField`, one case in `CardVisualValue`, and the
  type admitted by `CardVisualSchema.IsEditable`. **Three small additions, in
  files that exist for exactly this.**

An earlier version of this document said a new property "just appears and
works". Half of that was true and the half that was not is the more important
half: the editor discovers the field by reflection and offers a control for it
without being told, but nothing makes the *renderer* read it. A field added and
not wired through is a control that accepts a change and discards it, which is
worse for an authoring tool than not offering the control at all. That is why
authorability is now stated rather than assumed, and why a contract test
composes a card with every per-card property overridden and fails if the
composed result does not change.

## The chain

```
CardVisualLibrary           this card's artwork and its own adjustments
CardVisualLayerDefinition   the recipe's row for a layer
CardTextStyleDefinition     the recipe's row for a way of setting text
        ↓  conditions select which layers a card draws
        ↓  CardVisualInheritance applies that card's own sparse adjustments
CardVisualPlan              a finished description: pictures, words, rectangles
        ↓
CardVisualPainter           the only thing that touches a GameObject
```

Inheritance, in order, last wins:

1. **Global default** — the value the field was written with. Only reached when
   there is no authored layer or style at all.
2. **Type profile** — the recipe layer the card's conditions selected, and the
   style that layer names. This is where nearly all authoring belongs.
3. **Card adjustment** — one sparse row, for one property of one layer, on one
   card.

There is deliberately no fourth level. Variants — legendary, a class, a tribe —
are expressed as *conditions on layers*, which the system has had from the
beginning and which already give a legendary minion its own frame without a
separate profile asset.

## The runtime path

The editor and the game resolve a card's adjustments through the same code, and
this is the part to keep that way:

```
CardVisualLibraryAsset.OverridesFor(id)
   → CardVisualFactory.Describe(viewModel)
   → CardVisualDescriptor.FromViewModel(..., overrides)
   → CardVisualDescriptor.Overrides
   → CardVisualComposer.AddText / AddSprite
   → CardVisualInheritance.WithOverrides(layer, layer.LayerId, overrides)
   → CardVisualPlan
   → CardVisualPainter
   → CardView
```

`FromViewModel` used to accept the adjustments and never pass them to the
constructor. Every step either side of that line worked, so the library held
polish, the editor showed polish, and every card in a running match composed
without any — silently, because the editor built its descriptor by a different
route and looked correct. `An_adjustment_in_the_library_reaches_a_card_composed_the_way_a_match_composes_one`
goes through `Describe` from a view model for that reason: building a descriptor
by hand is exactly what hid the bug.

`CardVisualDescriptor.LooksTheSameAs` is the other half. It exists so a minion
being buffed re-letters two labels instead of re-resolving a stack of sprites,
and it decides whether a view recomposes. Comparing adjustments by *reference*
was wrong twice over: two equal sets forced a needless recompose, and — worse —
the editor edits one set in place, so the description held from last time and
the one being offered now are the same object, reference-equal however much it
changed. Content comparison fixes the first and cannot fix the second, because
an object is trivially equal to itself. So adjustments carry a non-serialised
`Revision`, stamped into the descriptor when it is built; the same object seen
twice at two revisions is a change.

## Layer identity

Every layer carries an `id` distinct from its `name`.

- `id` — permanent. What saved adjustments point at. Never editable in the
  window, and never changed once cards have been polished.
- `name` — a label. Free to rename; nothing is saved against it.

Adjustments used to be keyed by the label, which meant renaming a layer in the
inspector silently orphaned every card that had been polished on it: the rows
stayed in the asset, still loaded, and reached nothing. Ids are derived from the
label *once*, at migration, and independent from that moment on — the slug is
only how the first value is chosen.

`LayerId` falls back to the label when `id` is empty, so data authored before
the field existed still resolves, and `CardVisualDataValidator` reports every
layer relying on that fallback. Duplicate ids are reported too: two layers
answering to one id means an adjustment reaches both.

`Conquest of Hearthstone ▸ Migrate Card Visual Data` assigns missing ids and
retargets saved rows from labels to ids. It is idempotent, it reports every
change, and it never guesses: a row naming neither an id nor a label is left
exactly as it is for the validator to complain about, because discarding
authored data quietly is the failure this whole section exists to prevent.

## Property identity

A saved override names a property by a **stated id**, not by a C# field name:

```csharp
[CardVisualProperty(CardVisualAuthorability.PerCard,
    Id = "width", FormerIds = new[] { "boxWidth" })]
public float rectangleWidth;
```

The id defaults to the field name, which is what every id in the project
currently is. State it explicitly the moment the two need to diverge: renaming
the field then costs a `FormerIds` entry, and authored data keeps resolving.
Former ids are read, never written — anything saved afterwards carries the
current id, and the validator says so, so the migration completes itself as
cards are re-saved.

An id nothing answers to is not silently ignored. `CardVisualSchema.Find`
returns null, and the validator reports the row as reaching nothing — which is
precisely what a field renamed without a `FormerIds` entry produces.

## Authorability: only offer what works

`CardVisualAuthorability` states how far each property may be authored:

| Level | Profile | Per card | Meaning |
|---|---|---|---|
| `PerCard` | yes | yes | Propagates all the way to the composed plan. |
| `ProfileOnly` | yes | no | Deliberate: not a plumbing limit. |
| `Structural` | yes | no | Settled before adjustments are read. |
| `Unsupported` | no | no | Authored, serialised, read by nothing. |
| `Identity` | no | no | Other data points at this. |

The exceptions, and why:

- `layer.slot`, `layer.maskSlot`, `layer.text`, `layer.face`, `layer.required`,
  `layer.textStyle` — **Structural.** The composer reads all of these while it
  is still choosing which layers a card draws and which pictures they get, which
  happens before `WithOverrides` runs. An override of `slot` would change the
  slot recorded in the plan and not the sprite resolved from it: the tool would
  appear to work and change nothing visible.
- `style.role` — **ProfileOnly.** A card that chose its own font role would set
  its title in the rules face. Which family a card is set in is a project-wide
  invariant, not a per-card choice.
- `style.fillColor` — **Unsupported.** Nothing reads it. A label's colour comes
  from its layer's `tint`, which the painter actually applies. The authored data
  currently holds both — `RulesText (other).tint` at 0.12 and `RulesBody.fillColor`
  at 0.1176 — two nearly-equal values for one thing, of which only the tint has
  ever reached a renderer. It is marked rather than wired because wiring it would
  change the calibrated appearance of every label; marked rather than deleted
  because deleting authored data is the user's call, not the tool's.
- `layer.id`, `style.name` — **Identity.**

`Every_property_offered_per_card_actually_changes_the_composed_card` is the
guard: it overrides each per-card property in turn, on a picture layer and on a
text layer, recomposes, and requires the *rendered outcome* to differ. It
compares resolved sprites rather than recorded slots on purpose — comparing the
metadata would let a misclassified `slot` pass while changing no pixel.

## Validation

`CardVisualDataValidator` reports what would otherwise be silent:

- a layer with no id, or two layers sharing one
- an adjustment naming a layer no recipe defines
- an adjustment naming a property nothing answers to, or one now called
  something else
- an adjustment of a property no card may differ on
- the same property adjusted twice
- a value stored as the wrong kind — a colour where a number belongs reads back
  as `0` through `CardVisualValue.As` and is indistinguishable from an authored
  zero
- an enumeration value nothing defines
- two schema properties claiming one id

It runs in the editor window as a banner, from the setup command, and from the
tests. Resolution still falls back where it can, because a card that half-draws
beats a card that throws in a match — but the fallback is now reportable rather
than absorbed.

## Provenance

Every value the editor shows says where it came from: **Default**, the profile's
name, or **This card**.

An authored value came from the profile *whatever it happens to equal*. This
used to compare the authored value against the field's initialiser and report a
match as "Default", which answered the wrong question: a recipe that sets a
rotation to zero and a recipe that says nothing both end up at zero, and they are
not the same fact. Provenance is read precisely when those two need telling
apart, so answering by numeric coincidence made it useless exactly where it
mattered. In a Unity asset every field of an authored object carries a value, so
an authored object is the source of all of them; `GlobalDefault` now means only
that there was no layer or style at all.

## Editing scope

The window edits one of two things, and says loudly which:

- **Type profile** — writes to the recipe. Every card the layer's conditions
  admit changes.
- **This card** — writes one sparse row. Only that card changes.

Choosing "This card" requires a real card to be selected. Adjusting a property
starts it from the value it already had, so switching an adjustment on never
moves anything by itself. A control the contract does not allow is greyed out
*with its reason beside it* — a greyed control with no reason is
indistinguishable from a broken one.

## Sparse adjustments

A card that wants a wider title stores one row:

```
layer:    "nametext-other"     ← the layer's permanent id
property: "layer.width"        ← the property's stated id
value:    515
```

Everything else keeps resolving through the profile. So when the profile's title
moves down twelve pixels, that card moves down twelve pixels too — and keeps its
width.

Adjustments live on the card's entry in `CardVisualLibraryAsset`, beside its
artwork. They are created the first time somebody adjusts something, and a card
that has none has no entry.

## Source of truth

**The authored recipe is the source of truth for what a card looks like.**

`CardVisualSetup` also contains scaffolding that can build a recipe from
nothing, which is how the project was bootstrapped and how a new one still
could be. That was harmless while the recipe *was* the scaffolding, and became a
way to lose an evening's work the moment the editor made it authored data. So
the two are now separated by name and by behaviour:

- `Conquest of Hearthstone ▸ Create Missing Card Visual Assets` — creates what
  is missing and touches nothing that exists. It will not rewrite a recipe that
  already has layers, or a catalog that already has entries.
- `Conquest of Hearthstone ▸ Danger - Replace Authored Card Visuals With
  Scaffolding` — does the destructive thing, says exactly what it is about to
  destroy, and asks first.

`Creating_missing_assets_leaves_an_authored_recipe_alone` runs the safe command
and checks the authored layer count, ids, positions and font sizes afterwards.

## Picking a card

One button, top right: **Pick a card...** before anything is chosen, the card's
own name afterwards. Clicking it always opens the same searchable dropdown —
Unity's own (the one behind Add Component). The first entry is "Made up card
(synthetic preview)", which is how a real card is put back down.

- `CardRoster` — every `CardDefinitionAsset`, found in one `FindAssets` pass and
  kept until `Invalidate()`. Called when the button is clicked, never from
  `OnGUI`.
- `CardPickerDropdown` — builds the tree and hands the choice back.
- `CardVisualSelection` — turns the chosen asset into the descriptor to compose
  and the sparse row "This card" scope writes to.

## The three ways of looking at a card

They are presentations of one composition, not three styles.

- **General** — flat, square to the camera, orthographic. The one to edit in.
- **Hand rest** — the pose `HandFanLayout` gives that index in a hand of that
  size.
- **Hand hover** — the same, with the prefab's own hover.

The two hand looks are seen from the match camera's own place and pitch, then
magnified — the field of view narrowed and the camera slid across its own plane
until the card is centred. Both of those are a crop. Neither moves the camera
along its line of sight, so the perspective, the tilt and the foreshortening are
the ones a player sees. The magnification is fixed from the resting pose and the
pan applied afterwards, so rest and hover are drawn at one scale: in the captures
the hovered card measures 274×380 px against 216×293 at rest.

The exceptions to "nothing is reimplemented" are the fan's *settings* and the
camera's placement, which live in the match scene and cannot be read by a window
that has not opened it. The editor keeps one copy of each, in `HandPresentation`,
and `HandPresentationTests` reads the scene and fails when they disagree.

**Dimmed** is a toggle, not a style.

## What runs the preview

`CardPreviewCard` instantiates the real `P_Card` prefab, and **throws** if it
cannot. There used to be a fallback — a bare GameObject with a painter bolted
on, plus an error in the console — which is the worst of both worlds: the message
scrolls away, the window carries on, and what it draws is a card in
TextMeshPro's default face with no materials, which looks enough like a card to
tune against. Hours have already gone into exactly that picture. The window
catches `MissingCardPrefabException` and shows the reason where the card would
have been.

## Seeing it work

`Conquest of Hearthstone ▸ Capture Editor V2 Demonstration` writes the window's
own viewport to `CardCaptures/`, retunes the type profile in memory, captures
three cards before and after, and puts the recipe back. Nothing is saved. Cards
that asked for nothing follow the profile; a card with its own row does not, and
its two captures come out byte-identical.

## Adding things

### A new property

```csharp
[Tooltip("How far the ribbon's shadow falls.")]
[Range(0f, 8f)]
public float shadowDrop;
```

1. Add the field to `CardVisualLayerDefinition` or `CardTextStyleDefinition`.
2. **Read it** where it should take effect — the composer, `CardTextStyle.From`,
   the painter. Without this the editor offers a control that changes nothing.
3. Decide its authorability. The default is `PerCard`; mark it otherwise if it
   is read before adjustments are applied.

The editor needs nothing. `Every_property_offered_per_card_actually_changes_the_composed_card`
fails until step 2 is done, which is the point.

### A new layer

Add a row to `CardVisualRecipe_Standard` **with an `id`** — or run Migrate Card
Visual Data, which fills in any that are missing. Give it a slot or a text slot,
a sorting order and its conditions.

### A new card type

Add it to `CardType`. Give the recipe layers conditions that mention it.

### A new kind of value

1. `CardVisualValue` — a field, a case in `Of`, `As` and `KindFor`.
2. `CardVisualPropertyField` — one entry in the table from type to control.
3. `CardVisualSchema.IsEditable` — admit the type.

## What is deferred

- **Object references per card.** A sprite or a font can be authored on the
  profile, but a card cannot yet override one: `CardVisualValue` stores values,
  not references. The seam is a field holding an asset GUID, resolved on read.
- **Condition editing.** Shown in words, not editable.
- **Adding, removing and reordering layers** from the window. The recipe is an
  ordinary serialized asset and can be edited in the inspector meanwhile. A new
  layer added that way needs an id.
- **The validation grid.**
- **Direct manipulation in the new window.** The older Card Visual Preview keeps
  its drag handles.
- **`HandPresentation` as a tested mirror** rather than shared configuration.
- **Artwork having two potential sources** (the card definition and the library).
- **Sharding `CardVisualLibrary`** when the roster grows.

## What should never need editor changes

- a new property of an existing type
- a new layer, style, card type or condition
- any amount of retuning

## What legitimately needs editor changes

- a new category of value
- a new *kind* of authored object beyond layers and styles
- new window behaviour: the validation grid, condition editing, layer surgery
