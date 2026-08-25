using System;
using System.Text;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// A hash that means the same thing everywhere, forever.
    ///
    /// Written out by hand rather than taken from the runtime. String
    /// GetHashCode is randomised per process by design, and System.HashCode is
    /// explicitly documented as unstable across runs; either would produce a
    /// fingerprint that disagreed with itself between two launches, which is
    /// the one thing a fingerprint may never do.
    ///
    /// FNV-1a over UTF-8 bytes: a few lines, no dependency, and good enough for
    /// what this is for. It is a diagnostic, not a security measure, and no
    /// rule ever reads it.
    /// </summary>
    public static class StableHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <summary>The 64 bit hash of a canonical description.</summary>
        public static ulong Of(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            ulong hash = OffsetBasis;

            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash = unchecked(hash * Prime);
            }

            return hash;
        }

        /// <summary>
        /// The same hash as sixteen uppercase hex digits, which is what gets
        /// written into a replay file and pasted into a bug report.
        /// </summary>
        public static string Hex(string text) => Of(text).ToString("X16");
    }
}
