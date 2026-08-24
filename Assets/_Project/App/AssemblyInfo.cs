// -----------------------------------------------------------------------
//  CoH.App
//
//  Racine de composition : bootstrap de la scene, cycle de vie de la
//  partie, cablage entre le moteur (CoH.Core), les donnees (CoH.Data) et
//  la presentation (CoH.Presentation).
//
//  C'est la seule assembly autorisee a connaitre les trois autres. Elle
//  evite ainsi que Presentation ait besoin de dependre de Data.
//
//  Les premiers types arrivent en Phase 7 (GameSession, LocalGameServer,
//  MatchBootstrap).
//
//  NB : CoH.Data et CoH.Presentation sont bien referencees dans
//  CoH.App.asmdef, mais on ne peut pas encore ecrire « using CoH.Data; »
//  ici : ces namespaces ne contiennent aucun type avant les Phases 6 et 7,
//  et un namespace vide ne produit aucune metadonnee.
// -----------------------------------------------------------------------

using CoH.Core;
using UnityEngine;
