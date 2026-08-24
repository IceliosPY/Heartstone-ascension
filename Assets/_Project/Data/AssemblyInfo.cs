// -----------------------------------------------------------------------
//  CoH.Data
//
//  Couche d'authoring. Contient les ScriptableObjects qui servent a saisir
//  les donnees dans l'editeur Unity, ainsi que leur conversion vers les
//  POCO immuables consommes par CoH.Core.
//
//  Dependances autorisees : CoH.Core, UnityEngine.
//  Interdit : referencer CoH.Presentation ou CoH.App.
//
//  Les premiers types arrivent en Phase 6 (CardDefinitionAsset, catalogue).
//  Ce fichier existe pour que l'assembly soit reellement compilee des la
//  Phase 0, ce qui valide ses references.
// -----------------------------------------------------------------------

using CoH.Core;
using UnityEngine;
