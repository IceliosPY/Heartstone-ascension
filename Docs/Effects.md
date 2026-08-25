# Effects

How a card does something, without a class being written for it.

```
TRIGGER  →  SELECTOR  →  ACTION
```

A card is a list of those. `Test Sharpshooter` is not a type; it is a row of
data that says *Battlecry, a chosen enemy character, deal 2 damage*. Changing
it to deal five is changing a number.

---

## Writing a card

Open a `CardDefinitionAsset` and fill in **Effects**. No JSON, no text field.

| Field | Meaning |
|---|---|
| Trigger | `OnPlay` for a spell, `Battlecry` for a minion arriving from a hand, `Deathrattle` for one that died |
| Selector | Who it reaches. `ChosenTarget` asks the player |
| Target Filter | Only for `ChosenTarget`: what may be pointed at |
| Action | `DealDamage`, `DrawCards`, `Summon`, `GainTemporaryMana`, `ModifyStats` |
| Amount | Damage, cards, or mana, depending on the action |
| Attack / Health Delta | For `ModifyStats` |
| Summon Card Id / Count | For `Summon` |

A card may hold several. They resolve **in the order they were written** — a
card that damages and then draws does exactly that, and nothing anywhere sorts
or groups the list.

The layout is flat: an enum and a few numbers rather than a hierarchy of
polymorphic objects. `SerializeReference` can do polymorphism, but it brings
duplication hazards, an inspector that needs custom drawers, and a shape that is
harder to compare and fingerprint. The price of the flat form is a handful of
fields that are meaningless for some actions, and the validator says so when one
is filled in by mistake.

**Effects are gameplay data.** Changing one changes the catalog fingerprint, so
an old replay is refused rather than quietly replaying something else. Changing
the rules text or the artwork does not.

---

## Triggers

**`OnPlay`** — a card was played from a hand. A spell is nothing but its
effects, so playing it and resolving it are the same moment. It is in the
graveyard before it does anything.

**`Battlecry`** — a minion played from a hand has arrived. It resolves with the
minion **already standing on the board**, which is where Hearthstone resolves it
from and why a battlecry that sweeps every minion hits its own body.

**`Deathrattle`** — a death phase has taken the minion off the board. It
resolves *after* every death of that phase has been applied, so it sees a board
already cleared rather than one being cleared around it. Several at once follow
the death order settled in Phase 3: oldest by order of entry, never board
position.

Summoning is not playing. A token put down by an effect has no battlecry,
however many it was printed with.

---

## Targeting

Two questions, both answered by the engine:

```csharp
engine.GetPlayTargetRequirement(player, card)   // None / Required / Optional
engine.GetLegalPlayTargets(player, card)        // ordered ids
```

The view asks; it never reads a card's effects to invent a rule of its own, so
what it highlights is exactly what the engine will accept.

**The rule differs between a spell and a minion**, and that difference is
Hearthstone's:

| With no legal target | |
|---|---|
| Targeted **spell** | Cannot be played. It is only its effect, so there is nothing to buy |
| Targeted **minion** | Played anyway. It is also a body; the battlecry finds nobody |

Where a target *does* exist, both must point at one. Hearthstone gives no option
to decline.

A battlecry minion cannot target itself, and nothing was written to arrange
that: legal targets are worked out before the card is played, when the minion is
not on the board yet.

A target that has become invalid by the time an effect resolves is simply not
reached. No crash, no retarget.

---

## Actions and the pipeline

Effects **produce work**; they never reach past the rules.

```
Battlecry
  → ResolveEffectsAction queued
    → DamageRules / DrawSystem / SummonRules / ManaSystem
      → the events that already existed
        → the death phase, if anything died
```

Which is why every effect animates for free: `DealDamage` produces
`DamageDealt`, `DrawCards` produces `CardDrawn`, `Summon` produces
`MinionSummoned`, and Phase 9 already knows what to do with all three.

One card's effects for one trigger resolve inside **a single action**, so no
death phase interrupts a battlecry halfway and a sweep damages everything before
anything dies. Follow-up work is queued rather than called, so a deathrattle
that summons a minion that dies and has its own deathrattle is walked by the
resolution queue one flat step at a time, under the loop protection already
there.

Partial success is normal: draw two from a deck of one draws one and then takes
fatigue; summon three with one slot free summons one. Nothing rolls back.

---

## Statistics

Buffs are a **list of modifiers**, not two running totals:

```
Minion
  base stats from the CardDefinition
  + modifiers, in the order applied
  = effective attack and maximum health
```

A list because almost everything that comes later needs to find one again:
silence removes them all, an expiring buff removes its own, an aura adds and
removes as minions move. Totals can be added to but never unpicked.

Health goes up; damage already taken does not. A three health minion on one
damage given `+0/+2` has **five** maximum health, one damage, and four effective
health. That falls out of storing damage rather than current health, and it is
the reason we store it that way.

The printed card is never touched.

---

## Debugging a card

Six scenarios drop you straight into a situation: `coin`, `battlecry_target`,
`deathrattle`, `summon`, `buff`, `aoe`. Press **F1**, click one, play.

The event history in the panel shows exactly what an effect did, in order, and
the replay tools verify that it did the same thing twice. **Conquest of
Hearthstone → Validate Card Data** checks the authored effects, including a
summon that names a card nobody has or one that turns out to be a spell.

---

## What is deliberately not here

No keywords (Taunt, Charge, Divine Shield and the rest), no auras, no silence,
no conditions, no random selectors, no Discover, and no scripted escape hatch.
Every demonstration card is built from the generic parts, which was the point:
if one of them had needed an exception, the design would not have been finished.
