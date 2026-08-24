using System.Runtime.CompilerServices;

// The play mode tests drive the real interaction path rather than a copy of it,
// which means reaching the click routing and a little diagnostic state. Those
// stay internal so nothing in the game itself can depend on them.
[assembly: InternalsVisibleTo("CoH.Tests.PlayMode")]
