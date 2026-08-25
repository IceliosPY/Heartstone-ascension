using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The arrow drawn from an attacker to wherever the player is aiming.
    ///
    /// Drawn in the world, on the table, rather than as a line across the
    /// screen. The board is a 3D object seen at an angle, and a screen-space
    /// line laid over it reads as an overlay pointing at nothing in particular;
    /// a curve that lifts off the attacker and comes back down onto the target
    /// belongs to the same space as the pieces it connects.
    ///
    /// The curve is a quadratic Bezier whose control point rises above the
    /// midpoint, so the arrow arcs over the board instead of cutting through it.
    /// The lift grows with the distance, which keeps a short arrow from looking
    /// like a loop and a long one from looking flat.
    ///
    /// It shows where the player is pointing and nothing else. It never asks
    /// whether the thing at the far end may be attacked.
    /// </summary>
    public sealed class TargetingArrow : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private MeshFilter headFilter;
        [SerializeField] private MeshRenderer headRenderer;
        [SerializeField] private Camera matchCamera;

        [Header("Shape")]
        [Tooltip("Points along the curve. More is smoother and costs nothing here.")]
        [SerializeField] private int segments = 24;

        [Tooltip("How high the curve arcs, as a fraction of its length.")]
        [SerializeField] private float liftRatio = 0.28f;

        [Tooltip("Height above the attacker the arrow leaves from.")]
        [SerializeField] private float originLift = 0.45f;

        [SerializeField] private float startWidth = 0.10f;
        [SerializeField] private float endWidth = 0.22f;
        [SerializeField] private float headSize = 0.62f;

        private Mesh _head;

        /// <summary>True while the arrow is being shown.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Where the arrow currently points. Read by tests.</summary>
        public Vector3 Tip { get; private set; }

        private void Awake()
        {
            if (matchCamera == null)
            {
                matchCamera = Camera.main;
            }

            BuildHeadMesh();
            Hide();
        }

        /// <summary>Draws the arrow from a character to a point being aimed at.</summary>
        public void Show(Vector3 from, Vector3 to)
        {
            if (line == null)
            {
                return;
            }

            IsVisible = true;
            Tip = to;

            // The head is generated rather than authored, so make sure it
            // exists however this was reached. Awake is not the only way in:
            // the editor preview draws an arrow without ever entering play.
            BuildHeadMesh();

            Vector3 start = from + Vector3.up * originLift;
            float length = Vector3.Distance(start, to);
            Vector3 control = Vector3.Lerp(start, to, 0.5f) + Vector3.up * (length * liftRatio);

            int points = Mathf.Max(2, segments);

            line.enabled = true;
            line.useWorldSpace = true;
            line.positionCount = points;
            line.widthCurve = AnimationCurve.Linear(0f, startWidth, 1f, endWidth);

            Vector3 last = start;
            Vector3 beforeLast = start;

            for (int index = 0; index < points; index++)
            {
                float t = index / (float)(points - 1);
                Vector3 position = Bezier(start, control, to, t);
                line.SetPosition(index, position);

                beforeLast = last;
                last = position;
            }

            PlaceHead(beforeLast, last);
        }

        public void Hide()
        {
            IsVisible = false;

            if (line != null)
            {
                line.enabled = false;
                line.positionCount = 0;
            }

            if (headRenderer != null)
            {
                headRenderer.enabled = false;
            }
        }

        private void PlaceHead(Vector3 approach, Vector3 tip)
        {
            if (headRenderer == null || matchCamera == null)
            {
                return;
            }

            headRenderer.enabled = true;
            headRenderer.transform.position = tip;
            headRenderer.transform.localScale = Vector3.one * headSize;

            // Face the camera squarely, then roll within that plane so the point
            // follows the curve. Working the roll out on screen rather than in
            // the world is what makes it read as pointing at the target from
            // every angle the arrow can arrive at.
            Quaternion facing = Quaternion.LookRotation(
                matchCamera.transform.forward, matchCamera.transform.up);

            Vector3 screenTip = matchCamera.WorldToScreenPoint(tip);
            Vector3 screenApproach = matchCamera.WorldToScreenPoint(approach);
            Vector2 direction = (Vector2)(screenTip - screenApproach);

            float roll = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;

            headRenderer.transform.rotation = facing * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>
        /// A flat triangle pointing along its own +X, so the roll worked out on
        /// screen applies directly.
        /// </summary>
        private void BuildHeadMesh()
        {
            if (headFilter == null || headFilter.sharedMesh != null)
            {
                return;
            }

            _head = new Mesh { name = "TargetingArrowHead" };

            _head.SetVertices(new[]
            {
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.35f, 0.45f, 0f),
                new Vector3(-0.35f, -0.45f, 0f)
            });

            // Wound so the face pointing back at the camera is the front one.
            // The object is turned so its +Z runs away along the view
            // direction, which puts the camera on the -Z side, and a triangle
            // is only drawn from the side its winding reads clockwise from.
            _head.SetTriangles(new[] { 0, 2, 1 }, 0);
            _head.SetNormals(new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward });
            _head.RecalculateBounds();

            headFilter.sharedMesh = _head;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float inverse = 1f - t;
            return (inverse * inverse * a) + (2f * inverse * t * b) + (t * t * c);
        }

        private void OnDestroy()
        {
            if (_head != null)
            {
                Destroy(_head);
            }
        }
    }
}
