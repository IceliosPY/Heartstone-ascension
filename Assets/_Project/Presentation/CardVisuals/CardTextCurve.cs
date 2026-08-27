using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// A title's baseline described the way somebody adjusting it thinks about
    /// it: how deep the arch is, which way it leans, and where its top sits.
    ///
    /// The curve itself is still three control points, and those remain the only
    /// thing stored. This is a lens over them, not a second copy: the three
    /// numbers below are turned into control points when somebody moves a
    /// slider, and read back out of the control points to show where the sliders
    /// are. Keeping a copy of both would mean two records of one shape, and they
    /// would disagree the first time anybody edited the raw points.
    ///
    /// Everything below works in the curve's *parameter*, not in its x. That is
    /// not a shortcut: the warp asks for the baseline at a fraction of the way
    /// across the title and passes that fraction straight in as the parameter,
    /// never reading the x it gets back. The x components of the control points
    /// therefore do not move the baseline at all — they only turn glyphs, and
    /// only in <see cref="CardTextRenderMode.CurvedPath"/>. So the two are set
    /// to a third and two thirds, which is the one pair that makes the parameter
    /// equal the fraction exactly, and the curve authored is then the curve
    /// drawn.
    ///
    /// The relationship, written down once so it can be checked:
    ///
    ///   the chord runs from (0, 0) to (1, tilt), which in parameter terms is
    ///       simply tilt x t;
    ///   both controls are pushed off that chord, by a shared amount when the
    ///       arch is centred and by unequal ones when it is not;
    ///   the top of the arch sits where the push is heaviest, and a cubic with
    ///       one hump can only put that between a third and two thirds of the
    ///       way across — further than that it has to become an S.
    ///
    /// Which is exactly what the minion banner is, so it cannot be written this
    /// way at all. <see cref="Fits"/> says so, and a tool can warn before a
    /// slider replaces a measured shape with the nearest arch.
    /// </summary>
    public readonly struct CardTextCurve
    {
        /// <summary>
        /// The x controls that make the parameter equal the fraction across.
        ///
        /// With these two, x(t) works out to t exactly, which is the assumption
        /// the warp already makes when it samples the baseline.
        /// </summary>
        private const float FirstX = 1f / 3f;

        private const float SecondX = 2f / 3f;

        /// <summary>
        /// How far off centre the top of a single humped cubic can be pushed.
        ///
        /// A third either way. Past it the two controls have to lean in opposite
        /// directions and the arch becomes an S, which is a different shape
        /// rather than a further adjustment of this one.
        /// </summary>
        public const float NearestCentre = 1f / 3f;

        public const float FurthestCentre = 2f / 3f;

        public CardTextCurve(float amount, float tilt, float centre)
        {
            Amount = amount;
            Tilt = tilt;
            Centre = centre;
        }

        /// <summary>
        /// How deep the arch is, as a fraction of the title's width. Positive
        /// arches upward, which is the way a name plate curves; zero is flat.
        /// </summary>
        public float Amount { get; }

        /// <summary>
        /// How much higher the far end sits than the near one, in the same
        /// units. Zero leaves the two ends level.
        ///
        /// This leans the baseline itself. It is not a rotation of the label:
        /// the rectangle does not move and the letters are not turned — the line
        /// they sit on simply runs uphill.
        /// </summary>
        public float Tilt { get; }

        /// <summary>Where the top of the arch sits across the title. Half is centred.</summary>
        public float Centre { get; }

        /// <summary>The control points this describes.</summary>
        public void ToControls(out Vector2 controlA, out Vector2 controlB, out Vector2 end)
        {
            float centre = Mathf.Clamp(Centre, NearestCentre, FurthestCentre);
            float lean = LeanFor(centre);

            // How deep a shared push of one would make the arch at that top.
            // Dividing the depth wanted by it is what keeps the amount meaning
            // the depth however far off centre the top has been dragged.
            float reach = 3f * centre * (1f - centre) * (1f + lean * (1f - 2f * centre));

            float push = Mathf.Abs(reach) < 1e-6f ? 0f : -Amount / reach;

            // The style measures y downward, so an arch that rises is a negative
            // push. That sign lives here and nowhere else.
            controlA = new Vector2(FirstX, Tilt / 3f + push * (1f + lean));
            controlB = new Vector2(SecondX, 2f * Tilt / 3f + push * (1f - lean));
            end = new Vector2(1f, Tilt);
        }

        /// <summary>
        /// What a curve looks like in these terms.
        ///
        /// The top is found rather than assumed: it is where the curve strays
        /// furthest from its own chord, which is the top of the arch for the
        /// shapes this describes and still the most meaningful answer for the
        /// ones it does not.
        /// </summary>
        public static CardTextCurve From(Vector2 controlA, Vector2 controlB, Vector2 end)
        {
            float tilt = end.y;

            float centre = 0.5f;
            float depth = 0f;

            for (int step = 1; step < 200; step++)
            {
                float t = step / 200f;
                float strayed = Height(controlA, controlB, end, t) - tilt * t;

                if (Mathf.Abs(strayed) > Mathf.Abs(depth))
                {
                    depth = strayed;
                    centre = t;
                }
            }

            // A baseline that barely strays from its chord has no top to find,
            // and the deepest point of nothing is wherever the search happened
            // to start. Centred is the honest answer, and it leaves a slider
            // where somebody would expect it.
            if (Mathf.Abs(depth) < 0.002f)
            {
                centre = 0.5f;
            }

            return new CardTextCurve(-depth, tilt, centre);
        }

        /// <summary>
        /// Whether these three numbers really describe that curve.
        ///
        /// Compared on the height of the baseline all the way along, because the
        /// height is the whole of what the warp reads. False for a shape this
        /// cannot express — an S, most usefully, which is what the minion banner
        /// is.
        /// </summary>
        public static bool Fits(
            Vector2 controlA, Vector2 controlB, Vector2 end, float tolerance = 0.006f)
        {
            CardTextCurve curve = From(controlA, controlB, end);
            curve.ToControls(out Vector2 rebuiltA, out Vector2 rebuiltB, out Vector2 rebuiltEnd);

            for (int step = 0; step <= 40; step++)
            {
                float t = step / 40f;

                float was = Height(controlA, controlB, end, t);
                float now = Height(rebuiltA, rebuiltB, rebuiltEnd, t);

                if (Mathf.Abs(was - now) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// How unevenly the two controls are pushed, to put the top at a given
        /// place.
        ///
        /// Follows from setting the slope of the curve to nothing there: the top
        /// of the arch is where 1 - 2t + lean(6t² - 6t + 1) comes out at nought,
        /// which rearranges to this. It runs from one at a third of the way
        /// across to minus one at two thirds, and there is nothing outside that
        /// a single hump can do.
        /// </summary>
        private static float LeanFor(float centre)
        {
            float bottom = 6f * centre * centre - 6f * centre + 1f;

            if (Mathf.Abs(bottom) < 1e-6f)
            {
                return 0f;
            }

            return Mathf.Clamp((2f * centre - 1f) / bottom, -1f, 1f);
        }

        /// <summary>The baseline's height at a fraction of the way across.</summary>
        private static float Height(Vector2 controlA, Vector2 controlB, Vector2 end, float t)
        {
            float u = 1f - t;

            return 3f * u * u * t * controlA.y +
                   3f * u * t * t * controlB.y +
                   t * t * t * end.y;
        }
    }
}
