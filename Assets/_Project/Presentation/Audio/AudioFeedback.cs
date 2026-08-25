using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The seam between what happened and what it sounds like.
    ///
    /// Animations call <see cref="Play"/> with a <see cref="FeedbackSound"/> and
    /// know nothing more. Dropping real audio in later means assigning clips in
    /// the inspector; not one line of gameplay or animation code changes, which
    /// is the entire point of putting this in now rather than later.
    ///
    /// With nothing assigned it synthesises a short tone per sound. No file is
    /// downloaded and none is committed: the clips are built in memory at
    /// startup from a few numbers. They are meant to be replaced, and an
    /// assigned clip always wins over the generated one.
    /// </summary>
    public sealed class AudioFeedback : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int SoundCount = 10;

        [SerializeField] private AudioSource source;

        [Tooltip("Real clips, indexed by FeedbackSound. Any left empty falls back to a generated tone.")]
        [SerializeField] private AudioClip[] clips = new AudioClip[SoundCount];

        [Tooltip("Synthesise placeholder tones for sounds with no clip assigned.")]
        [SerializeField] private bool useGeneratedPlaceholders = true;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.35f;

        private AudioClip[] _generated;

        /// <summary>How many sounds were asked for. Read by tests.</summary>
        internal int PlayedCount { get; private set; }

        /// <summary>The last sound asked for. Read by tests.</summary>
        internal FeedbackSound LastPlayed { get; private set; } = FeedbackSound.None;

        private void Awake()
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            if (clips == null || clips.Length < SoundCount)
            {
                clips = new AudioClip[SoundCount];
            }
        }

        public void Play(FeedbackSound sound)
        {
            if (sound == FeedbackSound.None)
            {
                return;
            }

            PlayedCount++;
            LastPlayed = sound;

            AudioClip clip = ClipFor(sound);

            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }

        private AudioClip ClipFor(FeedbackSound sound)
        {
            int index = (int)sound;

            if (index < clips.Length && clips[index] != null)
            {
                return clips[index];
            }

            if (!useGeneratedPlaceholders)
            {
                return null;
            }

            _generated ??= new AudioClip[SoundCount];

            if (index < _generated.Length && _generated[index] == null)
            {
                _generated[index] = Generate(sound);
            }

            return index < _generated.Length ? _generated[index] : null;
        }

        /// <summary>
        /// A decaying tone, shaped only enough that the sounds are told apart:
        /// pitch says which event, decay says how heavy it is, and a touch of
        /// noise makes an impact read as a hit rather than a beep.
        /// </summary>
        private static AudioClip Generate(FeedbackSound sound)
        {
            (float frequency, float seconds, float noise) = ShapeOf(sound);

            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            float[] data = new float[samples];

            // A fixed generator, never the match's random source: audio must not
            // be able to influence a deterministic match.
            uint state = 0x9E3779B9u ^ (uint)sound;

            for (int index = 0; index < samples; index++)
            {
                float t = index / (float)SampleRate;
                float decay = Mathf.Exp(-t / (seconds * 0.32f));

                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float hiss = ((state & 0xFFFF) / 32768f) - 1f;

                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
                data[index] = Mathf.Clamp(Mathf.Lerp(tone, hiss, noise) * decay * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Placeholder_" + sound, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static (float Frequency, float Seconds, float Noise) ShapeOf(FeedbackSound sound) => sound switch
        {
            FeedbackSound.CardDraw => (880f, 0.10f, 0.15f),
            FeedbackSound.CardBurn => (180f, 0.22f, 0.55f),
            FeedbackSound.CardPlay => (520f, 0.10f, 0.10f),
            FeedbackSound.Summon => (330f, 0.16f, 0.10f),
            FeedbackSound.Attack => (240f, 0.09f, 0.35f),
            FeedbackSound.Impact => (140f, 0.16f, 0.65f),
            FeedbackSound.Death => (110f, 0.30f, 0.45f),
            FeedbackSound.TurnStart => (660f, 0.18f, 0.05f),
            FeedbackSound.GameEnd => (440f, 0.55f, 0.05f),
            _ => (440f, 0.10f, 0.2f)
        };
    }
}
