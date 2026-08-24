using System.Runtime.CompilerServices;

// EditMode tests need to reach the engine's internal types without forcing us
// to expose a wider public API than necessary.
[assembly: InternalsVisibleTo("CoH.Tests.EditMode")]
