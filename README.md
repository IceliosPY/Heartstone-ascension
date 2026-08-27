<p align="center">
  <img src="./conquest-of-azeroth-logo.png" alt="Hearthstone: Conquest of Azeroth" width="720">
</p>

<h1 align="center">Hearthstone: Conquest of Azeroth</h1>

<p align="center">
  A fan-made card game project blending the feel of <strong>Hearthstone</strong> with the class and gameplay ideas of <strong>Project Ascension: Conquest of Azeroth</strong>.
</p>

---

## About

**Hearthstone: Conquest of Azeroth** is a non-commercial fan project built in Unity.

The goal is to recreate the fast, readable and tactile feel of a Hearthstone match while adapting it around the class concepts, cards and mechanics inspired by *Project Ascension: Conquest of Azeroth*.

The project is being developed with a strong separation between deterministic game rules, authored card data and presentation so that the same ruleset can later support replays, testing and online multiplayer.

## Core vision

The long-term ambition is to **rework the entire Hearthstone card roster** and reinterpret it through the identity, classes, mechanics and presentation of **Project Ascension: Conquest of Azeroth**.

The goal is not to stop at a small custom set or a handful of adapted cards: the project is intended to eventually cover **the full Hearthstone roster**, progressively redesigned so that each card feels coherent inside the *Conquest of Azeroth* framework while preserving the recognizable structure and readability of a Hearthstone-style card game.

In practice, this means revisiting cards across all eras of Hearthstone and adapting their class identity, effects, wording, visuals, synergies and balance where necessary so they fit the *Conquest of Azeroth* version of the game.

## Current status

The project currently includes:

- deterministic match setup and turn flow;
- mana, card draw, fatigue and hand management;
- minion play and combat;
- targeted interactions and drag-and-drop card handling;
- Battlecry, Deathrattle and data-driven card effects;
- deterministic replays and debug scenarios;
- a data-driven card visual composer;
- Hearthstone-like card frames and layered rendering;
- per-card visual polish tools;
- curved and warped title rendering;
- authored typography with Belwe-style titles and Franklin Gothic rules text;
- hotseat play for local testing.

The game is still in active development. Visuals, card content, board presentation and gameplay systems are subject to change.

## Technology

- **Engine:** Unity 6
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Language:** C#
- **Architecture:** deterministic Core + Data + Presentation + App layers
- **Testing:** Unity EditMode and PlayMode tests

## Project structure

```text
CoH.Core
  Pure deterministic game rules and state.

CoH.Data
  Unity authoring assets converted into runtime definitions.

CoH.Presentation
  Card views, board presentation, input, animation and visual composition.

CoH.App
  Match bootstrap, local server/session layer and application wiring.
```

## Card rendering

Cards are not built as one prefab per card.

Their visuals are composed from data:

```text
Card definition
      ↓
Visual descriptor
      ↓
Recipe + catalog + optional card overrides
      ↓
Card visual plan
      ↓
CardVisualPainter
```

This allows the same rendering system to create different card types, rarities and visual styles while keeping card-specific polish optional.

## Development goals

The long-term goals include:

- a complete playable card set;
- broader keyword and status support;
- polished Hearthstone-like board presentation;
- deck building and collection tools;
- AI opponents;
- authoritative online multiplayer;
- WebGL support;
- replay sharing and deterministic match verification.

## Legal / fan-project notice

This is an unofficial, non-commercial fan project.

**Hearthstone**, **Warcraft**, **World of Warcraft** and related names, characters, artwork and trademarks are the property of Blizzard Entertainment.

**Project Ascension / Conquest of Azeroth** and their respective assets and trademarks belong to their respective owners.

This project is not affiliated with, endorsed by, or sponsored by Blizzard Entertainment or Project Ascension.

Third-party assets and fonts used during development should be documented in the project's license and attribution files before any public release or distribution.

---

<p align="center">
  <strong>Hearthstone: Conquest of Azeroth</strong><br>
  Fan-made. Non-commercial. Work in progress.
</p>
