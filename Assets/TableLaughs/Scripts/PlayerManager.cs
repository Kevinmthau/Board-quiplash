using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TableLaughs
{
    public sealed class PlayerManager : MonoBehaviour
    {
        public const int MinPlayers = 3;
        public const int MaxPlayers = 6;

        private readonly List<PlayerData> players = new List<PlayerData>();
        private int nextPlayerId = 1;

        private readonly Color[] colorPalette =
        {
            new Color(0.96f, 0.24f, 0.34f),
            new Color(0.13f, 0.67f, 0.95f),
            new Color(0.18f, 0.76f, 0.42f),
            new Color(1.00f, 0.72f, 0.18f),
            new Color(0.66f, 0.42f, 0.93f),
            new Color(1.00f, 0.45f, 0.18f),
            new Color(0.20f, 0.86f, 0.79f),
            new Color(0.95f, 0.38f, 0.78f)
        };

        public IReadOnlyList<PlayerData> Players => players;

        public bool CanStartGame => players.Count >= MinPlayers;

        public PlayerData JoinSeat(int seatIndex)
        {
            if (seatIndex < 0
                || seatIndex >= MaxPlayers
                || players.Count >= MaxPlayers
                || GetPlayerAtSeat(seatIndex) != null)
            {
                return null;
            }

            var color = colorPalette[(nextPlayerId - 1) % colorPalette.Length];
            var player = new PlayerData(nextPlayerId, seatIndex, $"Player {nextPlayerId}", color);
            nextPlayerId++;
            players.Add(player);
            return player;
        }

        public void LeaveSeat(int playerId)
        {
            players.RemoveAll(player => player.Id == playerId);
        }

        public PlayerData GetPlayerAtSeat(int seatIndex)
        {
            return players.FirstOrDefault(player => player.SeatIndex == seatIndex);
        }

        public PlayerData GetPlayer(int playerId)
        {
            return players.FirstOrDefault(player => player.Id == playerId);
        }

        public void RenamePlayer(int playerId, string displayName)
        {
            var player = GetPlayer(playerId);
            if (player == null)
            {
                return;
            }

            player.DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? $"Player {player.Id}"
                : displayName.Trim();
        }

        public void CyclePlayerColor(int playerId)
        {
            var player = GetPlayer(playerId);
            if (player == null)
            {
                return;
            }

            var currentIndex = 0;
            for (var i = 0; i < colorPalette.Length; i++)
            {
                if (Approximately(player.Color, colorPalette[i]))
                {
                    currentIndex = i;
                    break;
                }
            }

            player.Color = colorPalette[(currentIndex + 1) % colorPalette.Length];
        }

        public void ResetScores()
        {
            foreach (var player in players)
            {
                player.Score = 0;
            }
        }

        public void ClearPlayers()
        {
            players.Clear();
            nextPlayerId = 1;
        }

        public List<PlayerData> GetLeaderboard()
        {
            return players
                .OrderByDescending(player => player.Score)
                .ThenBy(player => player.DisplayName)
                .ToList();
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r)
                   && Mathf.Approximately(a.g, b.g)
                   && Mathf.Approximately(a.b, b.b);
        }
    }
}
