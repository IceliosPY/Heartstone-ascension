// -----------------------------------------------------------------------
//  CoH.Presentation
//
//  Unity presentation layer: views, layout, interaction, animation, VFX,
//  HUD. It sends commands to the engine and replays the events the engine
//  returns. It NEVER decides a game rule.
//
//  Allowed dependencies: CoH.Core, UnityEngine, TextMeshPro, Input System,
//  uGUI.
//  Forbidden: referencing CoH.App (App is what wires Presentation up).
//
//  First types land in Phase 7 (CardView, layouts, HUD).
//
//  The using directives below validate at compile time that the references
//  declared in CoH.Presentation.asmdef actually resolve.
// -----------------------------------------------------------------------

using CoH.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
