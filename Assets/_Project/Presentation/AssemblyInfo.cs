// -----------------------------------------------------------------------
//  CoH.Presentation
//
//  Couche de presentation Unity : vues, layout, interaction, animations,
//  VFX, HUD. Elle envoie des commandes au moteur et rejoue les evenements
//  qu'il retourne. Elle ne decide JAMAIS d'une regle de jeu.
//
//  Dependances autorisees : CoH.Core, UnityEngine, TextMeshPro,
//  Input System, uGUI.
//  Interdit : referencer CoH.App (c'est App qui cable Presentation).
//
//  Les premiers types arrivent en Phase 7 (CardView, layouts, HUD).
//
//  Les directives ci-dessous valident a la compilation que les references
//  declarees dans CoH.Presentation.asmdef se resolvent reellement.
// -----------------------------------------------------------------------

using CoH.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
