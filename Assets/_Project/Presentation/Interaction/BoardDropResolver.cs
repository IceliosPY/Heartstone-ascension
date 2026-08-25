using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Turns a pointer position over the board into the slot a minion would
    /// land in.
    ///
    /// Nothing more. It does not ask whether the card is affordable, whether the
    /// board has room, or whether the slot is legal; the engine already answers
    /// all three and answers them again when the command arrives. This only
    /// works out what the player is pointing between, so the preview can show it
    /// and the command can carry it.
    ///
    /// Pure geometry, and deliberately the mirror of
    /// <see cref="BoardRowLayout"/>: the boundaries it splits on are the very
    /// positions that layout will put the minions at, so what the marker
    /// promises is what the board delivers.
    /// </summary>
    public static class BoardDropResolver
    {
        /// <summary>
        /// The insertion index for a pointer at <paramref name="localX"/>, in
        /// the board anchor's own space.
        ///
        /// Returns 0 to the left of everything and <paramref name="count"/> to
        /// the right of everything, which are exactly the bounds the engine
        /// accepts for a board of that size.
        /// </summary>
        public static int Resolve(float localX, int count, float spacing)
        {
            if (count <= 0)
            {
                return 0;
            }

            for (int index = 0; index < count; index++)
            {
                // Splitting on each minion's centre rather than on the gap
                // between two means the marker moves the moment the pointer
                // crosses a minion, which is what makes it feel like the row is
                // opening up around the cursor.
                if (localX < BoardRowLayout.GetPosition(index, count, spacing).x)
                {
                    return index;
                }
            }

            return count;
        }

        /// <summary>
        /// Where a minion already on the board should stand while a gap is being
        /// held open at <paramref name="insertion"/>.
        ///
        /// The row is laid out as though the new minion were already there, and
        /// the existing ones take every slot but that one. Which is precisely
        /// where they will end up if the card is dropped, so nothing jumps.
        /// </summary>
        public static Vector3 PositionWithGap(int slot, int count, int insertion, float spacing)
        {
            if (insertion < 0)
            {
                return BoardRowLayout.GetPosition(slot, count, spacing);
            }

            int shifted = slot < insertion ? slot : slot + 1;
            return BoardRowLayout.GetPosition(shifted, count + 1, spacing);
        }

        /// <summary>Where the minion being dropped would stand.</summary>
        public static Vector3 GapPosition(int count, int insertion, float spacing) =>
            BoardRowLayout.GetPosition(Mathf.Clamp(insertion, 0, count), count + 1, spacing);
    }
}
