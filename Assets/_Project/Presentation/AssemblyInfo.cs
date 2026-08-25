using System.Runtime.CompilerServices;

// The play mode tests drive the real interaction path rather than a copy of it,
// which means reaching the click routing and a little diagnostic state. Those
// stay internal so nothing in the game itself can depend on them.
[assembly: InternalsVisibleTo("CoH.Tests.PlayMode")]

// The card visual assets are built by an editor command rather than clicked
// together by hand, so that one menu item rebuilds a recipe, a catalog and a
// factory that agree with each other. Authoring stays internal: nothing in a
// running game may write to them.
[assembly: InternalsVisibleTo("CoH.Editor")]
[assembly: InternalsVisibleTo("CoH.Tests.VisualEditMode")]
