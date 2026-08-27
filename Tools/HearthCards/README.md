# HearthCards asset pipeline

```
manifest  ->  verify  ->  fetch  ->  convert  ->  import  ->  catalog
```

Four commands, no crawling, and completing the list never needs new code.

## The manifest is the allowlist

`hearthcards-assets.json` names every file that may be downloaded, and nothing
else can be. There is no discovery step and no link following: the fetcher reads
the list and fetches exactly those URLs.

Each entry also says **where the file belongs on a card** — its slot, and which
cards it applies to — so filling the catalog is reading this file rather than
wiring anything by hand:

```json
{
  "id": "frame_minion_neutral",
  "filename": "Card_Inhand_Minion_Neutral.webp",
  "url": "https://www.hearthcards.net/assets/Card_Inhand_Minion_Neutral.webp",
  "status": "known",
  "category": "frame",
  "purpose": "The neutral minion frame, with its own art window.",
  "slot": "Frame",
  "cardType": "Minion",
  "cardClass": "Neutral",
  "rarity": null,
  "priority": 2
}
```

A field left `null` is **not a constraint**. One mana gem serves every card; one
frame serves one card type. That is the same matching the composer uses, and
there is no way to write a card id here — a test fails if one appears.

### `status`

`known` means the URL was given to us and is real. `unverified` means the
filename follows the same pattern and **has not been checked**. Only one entry
is `known`; the rest are proposals.

`verify` resolves every one of them and writes what it actually found. A URL
that does not answer is a filename guessed wrong, not a file that does not
exist — correct it in the manifest and run `verify` again.

## Running it

```bash
python Tools/HearthCards/fetch_card_assets.py verify
```

```bash
python Tools/HearthCards/fetch_card_assets.py all
```

| | |
|---|---|
| `status` | What is on disk and what is not. Touches no network |
| `verify` | Asks what exists. Writes no image files |
| `fetch` | Downloads what is missing or changed |
| `convert` | `Raw/*.webp` → `Imported/*.png`, losslessly |
| `all` | All four, in order |

Every run writes `hearthcards-assets.lock.json`: URL, HTTP status, SHA-256,
size, and when it was fetched. A file whose hash still matches is not
downloaded again. That file is what makes a second run reproducible, and what
turns the manifest's proposals into recorded fact.

`convert` needs Pillow (`pip install pillow`). Conversion is RGBA and lossless —
a frame is mostly transparent, and a mode that dropped the alpha would fill the
window the artwork shows through.

## Then, in Unity

```
Conquest of Hearthstone → Import HearthCards Components
```

Reads the same manifest, finds each converted file, sets its importer settings,
and writes the catalog rows. It runs **on top of the placeholders** rather than
instead of them. A row with identical constraints is overwritten; anything else
is added. So importing `Card_Inhand_Minion_Neutral` adds a *Minion + Neutral*
row and leaves the scaffolding *Minion* row underneath it as the fallback for a
class nobody has drawn yet — it does not claim to be every minion frame.

The catalog is never full of holes, the game always draws, and no card silently
ends up with a grey rectangle.

The log says which rows are real and which are still standing in.

**Rebuild Card Visuals runs this for you.** Rebuilding starts the catalog
again from scaffolding, so it lays whatever has been downloaded back over the
top before it finishes. The two commands compose; neither undoes the other.

```
Conquest of Hearthstone → Report Card Visual Coverage
Conquest of Hearthstone → Capture Card Variants
```

are how you check the result.

## Adding a component later

Add an entry. That is the whole procedure — the fetcher, the converter and the
importer all read the manifest and none of them knows what any particular
component is.

## Before committing anything downloaded

`Assets/ThirdParty/README.md` currently forbids assets derived from Blizzard
games, and every pack needs a licence record in `Assets/ThirdParty/LICENSES/`.
Neither has been changed. Both are the project owner's to settle.
