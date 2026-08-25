# HearthCards components

Card frame components used by the card visual composer.

Drop the image files in `Raw/`, then point the matching rows of
`Assets/_Project/Data/CardVisuals/CardVisualCatalog.asset` at them. That is the
whole integration: no code changes, no recipe changes, no prefab changes. A row
whose sprite is replaced is a card that looks different the next time it is
drawn.

## Before anything is imported

`Assets/ThirdParty/README.md` currently forbids assets derived from Blizzard
games, and a licence record in `Assets/ThirdParty/LICENSES/` is required for
every pack. Neither has been changed here. **The project owner has to settle
both before these files are committed**: update or qualify that rule, and add
`HearthCards-Components.txt` recording the source and the terms it is used
under.

Until then this folder is a slot, not an import.

## Format

Unity does not import `.webp`. Convert to `.png`, keeping transparency and the
original pixel dimensions. Keep the original file name so the source of each
file stays obvious.

The importer settings that matter, once a file is in place:

```
Texture Type        Sprite (2D and UI)
Sprite Mode         Single
Alpha Is Transparency   on
Generate Mip Maps   off
Max Size            at least the file's own width
```

## What the composer needs

Run **Conquest of Hearthstone → Report Card Visual Coverage** for the current,
generated version of this list: it names every slot that is unfilled and every
slot being served by a more general entry than it should be. The list below is
that report written out, as of the composer being built.

### Frames — one per card type, then per class

The example URL given for these is

```
https://www.hearthcards.net/assets/Card_Inhand_Minion_Neutral.webp
```

so the pattern appears to be `Card_Inhand_<Type>_<Class>`. Keep whatever the
real names are; the catalog stores a reference, not a name.

| Purpose | Catalog row | Expected at |
|---|---|---|
| Minion frame, neutral | Frame + Minion + Neutral | `Raw/Card_Inhand_Minion_Neutral.png` |
| Spell frame, neutral | Frame + Spell + Neutral | `Raw/Card_Inhand_Spell_Neutral.png` |
| Weapon frame, neutral | Frame + Weapon + Neutral | `Raw/Card_Inhand_Weapon_Neutral.png` |
| Hero frame, neutral *(optional)* | Frame + Hero + Neutral | `Raw/Card_Inhand_Hero_Neutral.png` |

A class frame is added the same way: one file, one catalog row constraining the
class, and nothing else anywhere. The class also has to exist as a value of
`CardClass` in the engine, which is one line — see `Docs/CardVisuals.md`.

### Shared components

These do not vary by type or class, so one file each covers every card.

| Purpose | Catalog row | Expected at |
|---|---|---|
| Mana crystal | ManaGem | `Raw/ManaGem.png` |
| Attack gem | AttackGem | `Raw/AttackGem.png` |
| Health gem | HealthGem | `Raw/HealthGem.png` |
| Card back | CardBack | `Raw/CardBack.png` |

### Rarity stones — one per rarity, none for basic

| Purpose | Catalog row | Expected at |
|---|---|---|
| Common | RarityGem + Common | `Raw/Rarity_Common.png` |
| Rare | RarityGem + Rare | `Raw/Rarity_Rare.png` |
| Epic | RarityGem + Epic | `Raw/Rarity_Epic.png` |
| Legendary | RarityGem + Legendary | `Raw/Rarity_Legendary.png` |
| Legendary frame treatment | EliteFrame + Legendary | `Raw/Frame_Elite.png` |

A basic card wears no stone. That is a condition on the layer, not a missing
file, so nothing needs to be supplied for it.

### Probably not needed

A finished frame usually draws the name banner and the rules panel into itself.
If the imported frames do, leave these three slots empty: the composer skips a
layer whose picture is missing, and a card with no separate banner is a card
whose frame already has one.

| Purpose | Catalog row |
|---|---|
| Name banner | NameBanner |
| Rules panel | RulesPanel |
| Tribe plaque | TribeBanner |

If they are needed, the rectangles they are drawn into are already in the
recipe, measured on the same 800 × 1100 canvas the frames use.

## Checking an import

1. **Conquest of Hearthstone → Capture Card Variants** writes one image per
   variant to `CardCaptures/`, composed by the same code the game uses.
2. **Tools → Conquest of Hearthstone → Card Visual Preview** does the same
   thing interactively, for any combination.
3. **Conquest of Hearthstone → Report Card Visual Coverage** says what is still
   missing or still falling back.
