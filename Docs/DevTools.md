# Developer tools

Replays, fingerprints and debug scenarios. Everything here exists to answer one
question faster: *what exactly happened, and how do I make it happen again?*

None of it is part of the game. No rule reads a fingerprint, no scenario can
describe a position the engine could not have reached on its own, and the
engine does not know that any of it exists.

---

## The debug panel

Press **F1** while `Match.unity` is playing. Press it again to get out of the
way. It is closed on start and builds its own interface at run time, so it
costs the scene nothing and cannot ship by accident.

Four columns:

| Column | Shows |
|---|---|
| **STATE** | The match, written out for a person, ending in its fingerprint |
| **COMMANDS** | What was submitted, whether the engine took it, and the state after each |
| **EVENTS** | What the engine reported, newest last |
| **TOOLS** | The buttons below |

It refreshes when something happens — a command, a scenario, a button — never
on a timer.

---

## Recording a replay

Nothing to switch on. Every match records itself from the moment it starts,
because having to remember to press record before the interesting thing happens
is how bugs get away.

The recording holds **inputs, not outcomes**: a seed, the decks or the scenario
it began from, the mulligans the host settled, and the ordered list of commands
somebody actually submitted. The results are carried alongside as the expected
answer.

**Export Replay** writes it to:

```
<persistentDataPath>/Debug/Replays/<label>-<timestamp>.cohreplay.json
```

The full path is shown in the panel and logged once. Refused commands are in
there too, on purpose: *"the engine refused something it should have taken"* is
a bug, and a recording that dropped the attempt could never show it.

---

## Verifying one

**Verify Current Replay** builds a brand new engine from the same inputs,
re-executes every command, and compares each step.

It does **not** apply the recorded events to a state. It re-runs the commands
and lets the engine produce its own events. That difference is the whole point:
applying the recording would reproduce the match without testing anything,
whereas re-running it means a replay that still matches is evidence the engine
is deterministic, and one that does not has found something.

The match on screen is never touched. The verifier owns its engine and throws
it away.

Either you get:

```
DETERMINISTIC. 24 commands replayed identically.
```

or the first thing that disagreed, and only the first — a divergence makes
every later command land in a different position, so continuing turns one
finding into a hundred:

```
DIVERGENCE AT COMMAND #17
Kind:     StateFingerprintMismatch
Command:  P1 Attack attacker=#41 target=#22
Expected: 7F42A1B9C0D3E5F6
Actual:   1AB3C4D5E6F70819
Randomness was consumed a different number of times: 12 then, 13 now.
```

`DivergenceKind` says what sort of thing went wrong:

| Kind | Meaning |
|---|---|
| `ReplayFormatMismatch` | Written by a build this one cannot read |
| `CatalogMismatch` | The cards no longer do what they did |
| `UnknownScenario` | The replay names a scenario this build lacks |
| `CommandResultMismatch` | Accepted then, refused now, or the reverse |
| `RejectionReasonMismatch` | Refused both times, for a different reason |
| `EventMismatch` | Same outcome, different sequence of events |
| `StateFingerprintMismatch` | The match ended up somewhere else |
| `ReplayFailed` | It could not be run at all |

`ReplayVerificationResult` is data, not a message. The panel formats it; a test
asserts on `Kind` and `DivergenceSequence`.

---

## Loading and watching one

**Reload Replay List** lists the folder. Click a file to select it, then:

- **Play Selected** — replays it on screen. It goes through the same
  `GameSession → IGameServer → PresentationQueue` road a player does, with the
  Phase 9 animations, because a second renderer for replays would be a second
  thing to keep correct.
- **Verify Selected** — the headless check above.

Speed buttons (**1x / 2x / 4x / Instant**) drive `PresentationTiming`. Instant
is not a second code path: the same sequences run in the same order, and every
tween applies its end state without spending a frame.

---

## Debug scenarios

Prepared positions, so a situation that takes fifteen turns to reach takes one
click. Click any of them in the panel and play on normally from there.

| Scenario | Position |
|---|---|
| `ready_combat` | A Test Soldier each, player one free to attack |
| `both_survive` | Two undamaged soldiers; attacking trades and kills nothing |
| `double_death` | Two soldiers on two health; attacking kills both at once |
| `hero_lethal` | Player two on two health, empty board; one attack ends it |
| `full_hand` | Ten cards held with a deck left, so the next draw burns |
| `fatigue` | An empty deck; ending the turn twice hurts |
| `seven_minion_board` | A full row, so nothing else can be played onto it |

They are built only from what the current cards can already be. A two for three
that has taken a point of damage is a two for two, which is how a mutual kill
is set up without inventing a card to set it up with.

Loading one is a legitimate use of a full rebuild from state: nothing on screen
relates to what is about to be there. Every action afterwards goes back to being
driven by events.

### Writing a new one

Add it to `DebugScenarios` as data:

```csharp
public static DebugScenario MyCase => new DebugScenario(
    "my_case",
    "What this position is for.",
    one: Side(board: new[] { Soldier(damage: 1) }),
    two: Side(heroHealth: 4),
    turnNumber: 5,
    activePlayer: PlayerId.One);
```

`DebugScenarioBuilder` turns it into a real `GameState`. It is strict on
purpose: every minion gets its zone, controller, timestamp and place in the row
exactly as a summon would have given them, and an unknown card id is refused
rather than skipped. A scenario producing a slightly broken state would spend
its life manufacturing bugs that do not exist.

Entities are created in a written order — both heroes, then seat one's deck,
hand and board, then seat two's — so **the same scenario always produces the
same entity ids**. A test can name a minion before the match has run.

### From a test

```csharp
GameState state = DebugScenarioBuilder.Build(DebugScenarios.DoubleDeath, catalog);
GameEngine engine = DebugScenarioBuilder.Start(DebugScenarios.DoubleDeath, catalog).Engine;
```

---

## Fingerprints

Three of them, all built as canonical text first and hashed second. When
something diverges, what you want is the two descriptions side by side, not
"the hashes differ".

**`StateFingerprint`** — everything the rules can see: phase, turn, both
players in seat order, every zone in its own order, every entity id, damage,
attack counters, timestamps, and how many random values have been drawn.
Nothing is read out of a dictionary and nothing uses `GetHashCode`.

**`CatalogFingerprint`** — what the cards do. The same seed and the same
commands do not reproduce a match if a card has been re-tuned in between, so
this is recorded alongside the seed. Artwork, names and rules text are excluded:
they never reach the engine, and rewording or redrawing a card must not
invalidate a replay of a match it was in.

**`EventFingerprint`** — one canonical line per event, payload included. A
match can reach the same final state by a different route, and that still
matters: the presentation animates the route and triggers will fire along it.

The hash is FNV-1a, written out by hand. `string.GetHashCode` is randomised per
process by design and `System.HashCode` is documented as unstable across runs;
either would produce a fingerprint that disagreed with itself between two
launches, which is the one thing a fingerprint may never do.

**`StateDump`** is the readable cousin — the same order, minus what is almost
always zero. **Copy State Dump** and **Export State Dump** both write it.

---

## The file format

`*.cohreplay.json`, version 1, indented. Text because a replay gets opened,
read, diffed and pasted into a message far more often than it gets loaded.

The JSON reader and writer are hand written, in about three hundred lines. The
alternative was a serialisation library, and `CoH.Core` is plain C# with no
Unity in it, which rules out `JsonUtility`; pulling in a dependency for one
debug format would have been a large purchase for a small need.

Seeds and entity ids are written **as strings**. JSON numbers are doubles, and a
64 bit seed does not survive one intact — a seed that came back off by one would
produce an entirely different match with nothing to show why.

A file whose version this build does not know is refused by name:

```
Unsupported replay format version: 4. This build reads version 1.
```

Bump `ReplayFormat.CurrentVersion` whenever an older file could be
misunderstood by a newer build.

---

## Validating the catalog

**Conquest of Hearthstone → Validate Card Data** in the Unity menu bar. It is
the Phase 6 validator, unchanged; there is no second one.

---

## What this is for

When a card added in a later phase breaks something:

```
load the exact scenario
  → play the effect
  → read the ordered events in the panel
  → export the replay
  → reproduce it deterministically, on any machine, forever
```

That is the whole point of Phase 10.
