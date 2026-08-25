using System.Collections;
using TMPro;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// A number that rises off a character and fades out.
    ///
    /// Nothing but presentation: it reports a value that has already been
    /// decided, and removes itself when it is done. The engine neither creates
    /// it nor knows it exists.
    /// </summary>
    public sealed class FloatingNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshPro label;

        [SerializeField] private float rise = 0.9f;

        [SerializeField] private float startScale = 0.7f;

        [SerializeField] private float endScale = 1.15f;

        /// <summary>Starts the rise. The object destroys itself at the end.</summary>
        public void Show(Vector3 worldPosition, string text, Color colour, float duration, Camera facing)
        {
            transform.position = worldPosition;

            if (facing != null)
            {
                transform.rotation = facing.transform.rotation;
            }

            if (label != null)
            {
                label.text = text;
                label.color = colour;
            }

            StartCoroutine(Rise(worldPosition, colour, duration));
        }

        private IEnumerator Rise(Vector3 from, Color colour, float duration)
        {
            Vector3 to = from + Vector3.up * rise;

            yield return Tweens.Over(duration, Easing.OutQuad, t =>
            {
                transform.position = Vector3.Lerp(from, to, t);
                transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

                if (label != null)
                {
                    // Holds full opacity for the first half, then goes.
                    label.color = new Color(colour.r, colour.g, colour.b, 1f - Mathf.Clamp01((t - 0.5f) * 2f));
                }
            });

            Destroy(gameObject);
        }
    }
}
