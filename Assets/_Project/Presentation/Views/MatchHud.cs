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

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultText;

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
        }

        public void SetInteractable(bool interactable)
        {
            if (endTurnButton != null)
            {
                endTurnButton.interactable = interactable;
            }
        }

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

        public void ShowResult(GameResult result)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

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
