using System;
using CoH.Core.Identifiers;
using CoH.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoH.Presentation
{
    /// <summary>
    /// The screen-space readout: whose turn it is, what everybody's health and
    /// mana look like, and the button that ends a turn.
    ///
    /// It reads the state and prints it. It decides nothing, and the End Turn
    /// button does not end a turn: it raises an intent that the input layer
    /// turns into a command for the engine to accept or refuse.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        [Header("Readout")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI activePlayerText;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private TextMeshProUGUI playerOneText;
        [SerializeField] private TextMeshProUGUI playerTwoText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI debugText;

        [Header("Controls")]
        [SerializeField] private Button endTurnButton;

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
            Player one = state.GetPlayer(PlayerId.One);
            Player two = state.GetPlayer(PlayerId.Two);

            Set(turnText, "TURN " + state.TurnNumber);

            Set(activePlayerText, state.CurrentPlayer.IsNone
                ? "-"
                : Describe(state.CurrentPlayer) + " TO PLAY");

            if (state.CurrentPlayer.IsNone)
            {
                Set(manaText, "MANA -/-");
            }
            else
            {
                Player active = state.GetPlayer(state.CurrentPlayer);
                Set(manaText, "MANA " + active.AvailableMana + " / " + active.MaxMana);
            }

            Set(playerOneText, Line(one, state.CurrentPlayer == PlayerId.One));
            Set(playerTwoText, Line(two, state.CurrentPlayer == PlayerId.Two));

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

            string message = result switch
            {
                GameResult.PlayerOneWins => "PLAYER 1 WINS",
                GameResult.PlayerTwoWins => "PLAYER 2 WINS",
                GameResult.Draw => "DRAW",
                _ => string.Empty
            };

            Set(resultText, message);
        }

        private static string Describe(PlayerId player) =>
            player == PlayerId.One ? "PLAYER 1" : "PLAYER 2";

        private static string Line(Player player, bool isActive)
        {
            string armour = player.Hero.Armor > 0 ? "  +" + player.Hero.Armor + " armor" : string.Empty;

            return (isActive ? "> " : "  ")
                   + Describe(player.Id)
                   + "   " + player.Hero.CurrentHealth + " HP" + armour
                   + "   hand " + player.Hand.Count
                   + "   deck " + player.Deck.Count;
        }

        private static void Set(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
