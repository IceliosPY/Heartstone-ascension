using System.Globalization;
using System.Text;

namespace CoH.Core.Diagnostics
{
    /// <summary>What kind of thing stopped a replay matching.</summary>
    public enum DivergenceKind
    {
        None = 0,

        /// <summary>The file was written by a build this one cannot read.</summary>
        ReplayFormatMismatch = 1,

        /// <summary>The cards do not do what they did when this was recorded.</summary>
        CatalogMismatch = 2,

        /// <summary>The replay names a scenario this build does not have.</summary>
        UnknownScenario = 3,

        /// <summary>Accepted then, refused now, or the other way round.</summary>
        CommandResultMismatch = 4,

        /// <summary>Refused both times, but for a different reason.</summary>
        RejectionReasonMismatch = 5,

        /// <summary>The same outcome, reported by a different sequence of events.</summary>
        EventMismatch = 6,

        /// <summary>The match itself ended up somewhere else.</summary>
        StateFingerprintMismatch = 7,

        /// <summary>The replay could not be run at all.</summary>
        ReplayFailed = 8
    }

    /// <summary>
    /// The outcome of checking a replay, as data.
    ///
    /// Data rather than a message, because the message is the least useful part
    /// of it: a panel wants to colour a line, a test wants to assert on the kind
    /// and the position, and only a person wants the prose. Formatting is left
    /// to whoever is displaying it.
    /// </summary>
    public sealed class ReplayVerificationResult
    {
        private ReplayVerificationResult(
            bool success, DivergenceKind kind, int sequence, int commandsChecked,
            string expected, string actual, string commandDescription, long expectedDraws, long actualDraws)
        {
            Success = success;
            Kind = kind;
            DivergenceSequence = sequence;
            CommandsChecked = commandsChecked;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            CommandDescription = commandDescription ?? string.Empty;
            ExpectedRandomDraws = expectedDraws;
            ActualRandomDraws = actualDraws;
        }

        public bool Success { get; }

        public DivergenceKind Kind { get; }

        /// <summary>Which command went wrong, or -1 when nothing did.</summary>
        public int DivergenceSequence { get; }

        public int CommandsChecked { get; }

        public string Expected { get; }

        public string Actual { get; }

        /// <summary>The command being replayed when it went wrong.</summary>
        public string CommandDescription { get; }

        /// <summary>
        /// How much randomness had been consumed, then and now.
        ///
        /// A count rather than a generator state, because what it is here to
        /// answer is "was a random value taken at a different moment", and that
        /// is the question a mismatched state most often hides. Only meaningful
        /// on a state divergence.
        /// </summary>
        public long ExpectedRandomDraws { get; }

        public long ActualRandomDraws { get; }

        /// <summary>True when the two runs consumed randomness differently.</summary>
        public bool RandomProgressionDiffers =>
            ExpectedRandomDraws >= 0 && ActualRandomDraws >= 0 &&
            ExpectedRandomDraws != ActualRandomDraws;

        public static ReplayVerificationResult Deterministic(int commandsChecked) =>
            new ReplayVerificationResult(
                true, DivergenceKind.None, -1, commandsChecked,
                string.Empty, string.Empty, string.Empty, -1, -1);

        public static ReplayVerificationResult Diverged(
            DivergenceKind kind, int sequence, int commandsChecked,
            string expected, string actual, string commandDescription = "",
            long expectedDraws = -1, long actualDraws = -1) =>
            new ReplayVerificationResult(
                false, kind, sequence, commandsChecked,
                expected, actual, commandDescription, expectedDraws, actualDraws);

        /// <summary>A report to read, or to paste into a message.</summary>
        public string Describe()
        {
            if (Success)
            {
                return "DETERMINISTIC. " + CommandsChecked + " commands replayed identically.";
            }

            StringBuilder text = new StringBuilder();

            text.Append("DIVERGENCE");

            if (DivergenceSequence >= 0)
            {
                text.Append(" AT COMMAND #").Append(DivergenceSequence.ToString(CultureInfo.InvariantCulture));
            }

            text.Append('\n');
            text.Append("Kind:     ").Append(Kind).Append('\n');

            if (CommandDescription.Length > 0)
            {
                text.Append("Command:  ").Append(CommandDescription).Append('\n');
            }

            text.Append("Expected: ").Append(Expected).Append('\n');
            text.Append("Actual:   ").Append(Actual).Append('\n');

            if (RandomProgressionDiffers)
            {
                text.Append("Randomness was consumed a different number of times: ")
                    .Append(ExpectedRandomDraws.ToString(CultureInfo.InvariantCulture))
                    .Append(" then, ")
                    .Append(ActualRandomDraws.ToString(CultureInfo.InvariantCulture))
                    .Append(" now.\n");
            }

            return text.ToString();
        }

        public override string ToString() => Describe();
    }
}
