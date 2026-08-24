// -----------------------------------------------------------------------
//  CoH.App
//
//  Composition root: scene bootstrap, match lifecycle, and wiring between
//  the engine (CoH.Core), the data layer (CoH.Data) and the presentation
//  layer (CoH.Presentation).
//
//  This is the only assembly allowed to know all three. That is what keeps
//  Presentation from having to depend on Data.
//
//  First types land in Phase 7 (GameSession, LocalGameServer,
//  MatchBootstrap).
//
//  Note: CoH.Data and CoH.Presentation are indeed referenced in
//  CoH.App.asmdef, but we cannot write "using CoH.Data;" here yet: those
//  namespaces hold no type before Phases 6 and 7, and an empty namespace
//  emits no metadata.
// -----------------------------------------------------------------------

using CoH.Core;
using UnityEngine;
