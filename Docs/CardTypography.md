# Card typography

How the reference renderer sets the writing on a card, and what this project does
about it.

Everything below marked **confirmed** was read out of the renderer's own public
source — its layer templates and its script bundle — rather than matched by eye.
That distinction is the point of the document: the first attempt at the card
components guessed twelve filenames out of fifteen wrong, and the lesson was that
a value belongs here because something says it, not because it looks right.

## What was read

Mirrored with `Tools/HearthCards/mirror_renderer.py`, which follows the entry
page, the scripts and stylesheets that page links, and the layer templates those
name. It invents no paths and downloads no fonts or images.

| File | Size | What it holds |
|---|---|---|
| `static/js/main.2bc94273.js` | 2 659 537 B | the whole renderer |
| `static/css/main.84b0c257.css` | 442 322 B | the `@font-face` rules |
| `assets_template_new/minion.json` | 11 425 B | a minion's layers, texts and boxes |
| `assets_template_new/spell.json` | 10 887 B | the same for a spell |
| `assets_template_new/weapon.json`, `hero.json`, `location.json` | | not used yet |

`hero_power.json` does not exist: it answers 200 with the site's own index page,
which is the same soft-404 that made the first filename guesses look successful.

## The title has two treatments, and the curved-path one is not the default

**Confirmed.** A setting called `cardTitleStyle` chooses between them. It has
exactly two values, and it defaults to `game-like`:

    cardTitleStyle: localStorage.getItem("cardTitleStyle") || "game-like"
    <option value="game-like">   <option value="promotional">

The SVG layer that would draw a title along `pathD` skips it whenever the
template has a `titleMesh` and the setting is not `promotional`:

    const usesMesh = Boolean(template.titleMesh) && settings.cardTitleStyle !== "promotional";
    ...
    if (usesMesh && text.bindTo === "Title") return null;

So `pathD` is the **promotional** style — a flat, printed treatment — and the
default is a text texture mapped onto a warped mesh. This reverses what an
earlier pass through the same bundle assumed.

### The default: a texture on a mesh

**Confirmed.** The title is drawn to an offscreen 2048 × (2 × `height`) canvas,
then that canvas is used as a texture on a small 3D mesh, projected and drawn
into the card.

Building the texture:

| Step | Value |
|---|---|
| canvas | 2048 wide, `2 * height` tall (512) |
| start size | 202 px, `normal <size>px 'Belwe', serif` |
| shrink step | −4 px while wider than `2 * span`, floor 40 px |
| vertical scale | `ctx.scale(1, stretch)` |
| baseline | `canvasHeight / 2 / stretch + ascentOf("H") / 2` |
| stroke | `lineWidth = max(8, 0.17 * size)`, `rgb(1,1,1)`, round joins |
| fill | `#ffffff`, shadow `rgba(0,0,0,0.9)`, blur 20, offsetY `12 / stretch` |
| order | stroke first, then fill over it |

Projecting the mesh:

    rx = wx * scale;  ry = wy * scale;  rz = cameraZ - wz * scale
    t  = tan(45° / 2)
    x  = 800 * (rx / (rz * (1600/600) * t) + 1)
    y  = 300 * (1 - ry / (rz * t))

onto a 1600 × 600 canvas, drawn into the card at `placement` with
`imageSmoothingQuality = "high"` and `filter = "blur(0.4px)"`.

### What the mesh actually does, measured

`Tools/HearthCards/decode_title_mesh.py` decodes both meshes and projects them
the way the bundle does. The numbers matter, because reading the calibration
without them produced a title that looked nothing like a title:

| | ally | spell |
|---|---|---|
| projected width, in placement pixels | 601 | 588 |
| midline amplitude | 46.9 px = **7.80%** of the span | 41.8 px = **7.10%** |
| surface height varies by | **3.7%** | **5.7%** |
| texture → card, vertical | 0.168 | 0.158 |
| texture → card, horizontal | 0.294 | 0.287 |
| aspect of that mapping | 0.573 | 0.551 |
| **net glyph aspect** = aspect × stretch | **0.918** | **0.936** |
| horizontal density, ends against middle | **24% tighter** | **4% tighter** |
| midline slope | −26.4° … +10.6° | −15.6° … +15.6° |

Three things follow, and each of them was got wrong first time round.

- **The stretch is not a card space number.** The template's 1.6 is applied
  inside a 2048 × 512 texture that is then mapped onto a surface a third as tall
  as it is wide. On a finished card a title comes out at 0.918 — very slightly
  *shorter* than the face draws it, not two thirds taller.
- **The surface does not turn letters.** Its height is all but constant from end
  to end, so a letter's top and bottom are lifted by the same amount: the letter
  slides, and its uprights stay upright. The midline reaching −26° is what the
  *sheet* does, not what a glyph does.
- **The perspective is horizontal only.** The ends are a quarter tighter across
  on the minion banner and a twenty-fifth on the spell, while neither loses more
  than a twentieth of its height.

The mesh itself is two base64 blobs in the bundle — 63 vertices for `ally`, 234
for `spell`, little-endian float32 position at +0/+4/+8 and UV at +24/+28, stride
detected from `[48,40,32,44,56,64]`, indices as `Uint16`, remapped
`(x, y, z) → (−x, −z, y)` and centred on their own bounds. **Confirmed** as a
format, and deliberately **not reproduced here**: it is the renderer's own
geometry asset.

### Title data, minion against spell

**Confirmed**, from the two templates:

| | minion (`ally`) | spell (`spell`) |
|---|---|---|
| `meshType` | `ally` | `spell` |
| `span` | 1010 | 1010 |
| `stretch` | **1.6** | **1.7** |
| `height` | 256 | 256 |
| `scale`, `cameraZ` | 2.3, 2.3 | 2.3, 2.3 |
| `placement` | 27, 500, 759 × 290 | 7, 481, 800 × 300 |
| `pathD` | `m 103.84,678.19 c 69.23,53.58 423.42,-109.05 587.48,0.95` | `m 107.00,682.00 c 0,0 290.37,-118.96 598.64,0` |
| `fontSize` | 58 | 55 |
| `maxWidth` | 580 | 575 |
| `strokeWidth` | 8.6 | 8.6 |

The minion baseline is lopsided — it rises on the left and falls on the right —
and the spell baseline is a symmetric arch. That single difference is most of why
the two titles read differently.

### The promotional treatment

**Confirmed.** Two `<textPath>` elements on the same path, `textAnchor: middle`,
`startOffset: 50%`; the first filled with the stroke colour and stroked with
round joins and caps, the second filled with the fill colour on top. The size is
fitted first:

    for (ctx.font = `normal 400 ${size}px ${family}`; ctx.measureText(t).width > maxWidth && size > 20;)
        size -= 1;
    dy = -(fontSize - fittedSize) / 3

## Numbers and tribes

**Confirmed.** Every one of them is `renderType: "rect"`, drawn as two SVG texts,
stroke under fill, with no warp, no rotation and no kerning attribute:

    x = rect.x + rect.width / 2
    y = rect.y + rect.height / 2 - 10
    textAnchor = "middle"

| Text | font | size (minion) | fill | stroke | width |
|---|---|---|---|---|---|
| Cost | Belwe | 180 | `#ffffff` | `#0a0805` | 10 |
| Attack | Belwe | 173.3 | `#ffffff` | `#0a0805` | 10 |
| Health | Belwe | 173.3 | `#ffffff` | `#0a0805` | 10 |
| Tribe | Belwe | 50 (48 on a spell) | `#ffffff` | `#0a0805` | 7 (6) |

A tribe containing a space or comma is split across two rows 44 px apart, by
`valueTransform: split_0` / `split_1` and a condition on the value. The `-10`
offset is a constant in the renderer, not a per-card value.

## Rules text

**Confirmed** as parameters, from `descriptionBoxNew`:

| | minion | spell |
|---|---|---|
| font | FranklinGothic | FranklinGothic |
| colour | `[30, 23, 16]` | `[30, 23, 16]` |
| `defaultFontSize` | 40 | 40 |
| `minFontSize` | 0.1 | 20 |
| `charSize` | 5.0 | 5 |
| `lineSpacing` | **0.77** | **0.75** |
| `pixelsPerUnit` | 271 | 271 |
| `widthUnity` | 1.95 | 1.7 |
| `heightUnity` | 0.87 | 0.82 |
| `useUnderwear` | true | true |
| `isFlipped` | **false** | **true** |
| `underwearWidthPercent` | 0.25 | 0.15 |
| `underwearHeightPercent` | 0.17 | 0.05 |
| `dropX`, `dropY` | 138, 770 | 169, 781 |

And **confirmed** as an algorithm:

    boxWidth   = widthUnity * pixelsPerUnit
    boxHeight  = heightUnity * pixelsPerUnit
    penalty    = (tribe contains "/") ? 48 : 0        // only minions and weapons
    usable     = boxHeight - penalty
    renderSize = fontSize * (0.01 * charSize) * 0.1 * pixelsPerUnit
    advance    = 1.3 * renderSize * (lineSpacing * 1.03)

- **Centring.** Each line is centred horizontally in the box; the block is
  centred vertically, with the first baseline at
  `(usable - totalHeight) / 2 + 0.78 * renderSize`.
- **Shrinking.** At most 40 passes. With `resizeToFitAndGrow` false — which both
  templates leave — it only ever shrinks: `size *= 1 - 0.025` until the text fits
  or reaches `minFontSize`.
- **Avoiding the gems.** The "underwear" is a narrowed band at the bottom of the
  box (top, when `isFlipped`). A line falling inside it is wrapped to
  `round(boxWidth * (1 - underwearWidthPercent))` instead of the full width, but
  only when it is wide enough to reach the inset. That is what keeps the last
  line clear of the attack and health gems on a minion, and clear of the school
  banner on a spell.
- **Bold.** There is no bold face. It is drawn four extra times at ±1 px
  diagonals and then normally, so the glyph thickens. Italic is real.
- Keywords are bolded by a large built-in list plus whatever the user adds.
- `fontKerning = "none"` and `imageSmoothingQuality = "high"` on both the
  measuring canvas and the drawn one. **Confirmed.**

## Fonts

**Confirmed**, from `@font-face` in the stylesheet. Nothing was downloaded.

| Family | File | Role |
|---|---|---|
| `Belwe` | `/fonts/Belwe_en.ttf?v=2` | titles, costs, attack, health, tribes |
| `SetBelwe`, `PublicSetBelwe` | `/fonts/Belwe.ttf` | set and library headings |
| `FranklinGothic` | `/fonts/FranklinGothic-dehinted.ttf?v=2` | rules text |
| `BNT85` | `/fonts/BNT85.ttf` | the Cyrillic stand-in for Belwe |

All under `https://www.hearthcards.net`. The Cyrillic substitution is a rule, not
a preference:

    const cyrillic = /[Ѐ-ԯᲀ-᲏ⷠ-ⷿꙀ-ꚟ]/;
    familyFor = (family, text) =>
        String(family).toLowerCase().includes("belwe") && cyrillic.test(text) ? "BNT85" : family;

Belwe and Franklin Gothic are commercial typefaces. Whether these particular
files may be used here is a licensing question, deliberately left open: this
project holds **no font files** and the roles below are slots waiting to be
filled.

## What this project does with it

The renderer's decisions become data on the recipe rather than code:

    CardVisualRecipe
      └── text styles          AllyTitle, SpellTitle, RulesBody, StatNumber, TribePlate
            ├── role           which font slot to ask for
            ├── render mode    straight, curved path, warped banner
            ├── outline        colour and width
            ├── stretch/taper  the banner's shape
            └── baseline       a cubic curve, in widths of the label's rectangle

A layer names a style; the composer resolves it into the plan; the painter draws
it. Nothing downstream knows that a minion is a minion.

### What carried over exactly

- The two baselines, as normalised cubics. Every offset in the `pathD` divided by
  the span it covers, so the shape survives the rectangle being moved or resized
  by the layout tool.
- `stretch`: 1.6 for a minion, 1.7 for a spell.
- The rules-text colour, `[30, 23, 16]`.
- Stroke colours: `rgb(1,1,1)` on a title, `#0a0805` on a number.

### What could not

- **Outline widths.** The renderer strokes on a canvas in pixels; TextMeshPro
  spreads an outline through a signed distance field. The source ratios are
  recorded beside the values that replace them (`0.17 × size` for a title,
  `10/173.3` for a number), and the values themselves are **inferred**.
- **The mesh.** The taper and the stretch are two numbers standing in for the
  renderer's own 63- and 234-vertex geometry, but they are **measured** off it
  rather than guessed: the decoder above reports what the surface does and those
  are the values the styles carry. What is not reproduced is the geometry itself.
- **The foreshortening is modelled as a density and integrated.** The measured
  24% is how much tighter the *spacing* is at the ends; applying it to positions
  instead pulls the ends in by the whole 24% rather than the 8% the mesh does,
  and leaves half the banner empty.
- **The rules-text engine.** TextMeshPro wraps and shrinks text itself. The
  underwear band — the narrowing that dodges the attack and health gems — is
  **not implemented**; the rules rectangle is a plain rectangle today.
