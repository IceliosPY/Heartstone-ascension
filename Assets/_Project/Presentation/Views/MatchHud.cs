using System;
using CoH.Core.Identifiers;
using CoH.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoH.Presentation
{
    /// <summary>
    /// The screen-space readout.
    ///
    /// Split in two on purpose. The player panel carries the three things
    /// somebody actually plays from, whose turn it is, which turn, and how much
    /// mana is left; everything else, health, deck and hand counts, now lives on
    /// the hero views where it belongs. The developer overlay keeps phase, seed
    /// and entity count, small and out of the way.
    ///
    /// It reads the state and prints it. It decides nothing, and the End Turn
    /// button does not end a turn: it raises an intent that the input layer
    /// turns into a command for the engine to accept or refuse.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        [Header("Player panel")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI activePlayerText;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("Developer overlay")]
        [SerializeField] private TextMeshProUGUI debugText;

        [Header("Controls")]
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TextMeshProUGUI endTurnLabel;

        [Header("Turn banner")]
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TextMeshProUGUI bannerText;

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private CanvasGroup resultGroup;
        [SerializeField] private RectTransform resultRect;

        /// <summary>Raised when the player asks to end the turn.</summary>
        public event Action EndTurnRequested;

        private void Awake()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(() => EndTurnRequested?.Invoke());
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            SetBannerAlpha(0f);
        }

        /// <summary>Names the turn that is beginning. Shown by the turn animation.</summary>
        public void SetBannerText(string text)
        {
            if (bannerText != null)
            {
                bannerText.text = text;
            }
        }

        /// <summary>
        /// Fades the turn banner. Driven from outside rather than run here, so
        /// the presentation queue can wait for it like anything else.
        /// </summary>
        public void SetBannerAlpha(float alpha)
        {
            if (bannerGroup != null)
            {
                bannerGroup.alpha = Mathf.Clamp01(alpha);
                bannerGroup.gameObject.SetActive(alpha > 0.001f);
            }
        }

        /// <summary>Brings the result in, from nothing to fully present.</summary>
        public void SetResultReveal(float amount)
        {
            float t = Mathf.Clamp01(amount);

            if (resultGroup != null)
            {
                resultGroup.alpha = t;
            }

            if (resultRect != null)
            {
                // Unclamped, so an overshooting curve is allowed to overshoot.
                resultRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, amount);
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (endTurnButton != null)
            {
                endTurnButton.interactable = interactable;
            }
        }

        /// <summary>
        /// Read back for tests rather than duplicated by them: whether End
        /// Turn currently accepts a click. A disabled <see cref="Button"/>
        /// already draws itself dimmed through its own transition colours,
        /// so disabling it while a modal choice is open is both the
        /// interaction fix and the visual one, at no extra cost.
        /// </summary>
        public bool IsEndTurnInteractable => endTurnButton != null && endTurnButton.interactable;

        /// <summary>End Turn's own screen rectangle, read back for tests rather than duplicated by them.</summary>
        public RectTransform EndTurnRect => endTurnButton != null ? (RectTransform)endTurnButton.transform : null;

        private CanvasGroup _endTurnGroup;

        /// <summary>How faint End Turn goes while a modal choice is open over it - visible, never hidden.</summary>
        private const float EndTurnDimmedAlpha = 0.28f;

        /// <summary>
        /// Fades End Turn toward the background without hiding, moving or
        /// resizing it - for as long as a modal selection like a Raise
        /// choice is open and may be drawn in front of it.
        ///
        /// A Screen Space - Overlay canvas always draws after every
        /// world-space object, so nothing a world-space CardView does with
        /// its own sorting order can make it literally paint over these
        /// pixels; fading End Turn low enough to read as background is what
        /// makes the choice cards read as being in front of it, without
        /// touching where End Turn sits or what it is - the button keeps
        /// existing exactly where a future board-integrated version of it
        /// will replace it.
        /// </summary>
        public void SetEndTurnModalDimmed(bool dimmed)
        {
            if (endTurnButton == null)
            {
                return;
            }

            if (_endTurnGroup == null)
            {
                _endTurnGroup = endTurnButton.GetComponent<CanvasGroup>();

                if (_endTurnGroup == null)
                {
                    _endTurnGroup = endTurnButton.gameObject.AddComponent<CanvasGroup>();
                }
            }

            _endTurnGroup.alpha = dimmed ? EndTurnDimmedAlpha : 1f;
        }

        /// <summary>Read back for tests rather than duplicated by them.</summary>
        public bool IsEndTurnModalDimmed => _endTurnGroup != null && _endTurnGroup.alpha < 0.999f;

        public void SetHint(string hint)
        {
            if (hintText != null)
            {
                hintText.text = hint;
            }
        }

        public void Refresh(GameState state)
        {
            Set(turnText, "TURN " + state.TurnNumber);

            if (state.CurrentPlayer.IsNone)
            {
                Set(activePlayerText, "MATCH OVER");
                Set(manaText, "-");
                Set(endTurnLabel, "END TURN");
                return;
            }

            Player active = state.GetPlayer(state.CurrentPlayer);

            // The button says whose turn it is ending, so nobody passes for the
            // wrong player on a shared screen.
            Set(activePlayerText, Describe(state.CurrentPlayer) + " TO PLAY");
            Set(manaText, active.AvailableMana + " / " + active.MaxMana + "  MANA");
            Set(endTurnLabel, "END " + Describe(state.CurrentPlayer) + " TURN");

            Set(debugText,
                "phase " + state.Phase +
                "   seed " + state.Seed +
                "   entities " + state.EntityCount);
        }

        /// <summary>
        /// Puts the result up, hidden. The reveal is a separate step so the
        /// animation controls when it is actually seen.
        /// </summary>
        public void ShowResult(GameResult result)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            SetResultReveal(0f);

            Set(resultText, result switch
            {
                GameResult.PlayerOneWins => "PLAYER 1 WINS",
                GameResult.PlayerTwoWins => "PLAYER 2 WINS",
                GameResult.Draw => "DRAW",
                _ => string.Empty
            });
        }

        public static string Describe(PlayerId player) =>
            player == PlayerId.One ? "PLAYER 1" : "PLAYER 2";

        private static void Set(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
