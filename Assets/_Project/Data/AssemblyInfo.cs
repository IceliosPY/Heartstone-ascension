// -----------------------------------------------------------------------
//  CoH.Data
//
//  Authoring layer. Holds the ScriptableObjects used to enter data in the
//  Unity editor, along with their conversion into the immutable POCOs
//  consumed by CoH.Core.
//
//  Allowed dependencies: CoH.Core, UnityEngine.
//  Forbidden: referencing CoH.Presentation or CoH.App.
//
//  First types land in Phase 6 (CardDefinitionAsset, card catalog).
//  This file exists so the assembly is actually compiled from Phase 0
//  onwards, which validates its references.
// -----------------------------------------------------------------------

using CoH.Core;
using UnityEngine;
