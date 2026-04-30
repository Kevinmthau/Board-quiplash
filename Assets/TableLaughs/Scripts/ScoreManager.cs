using System.Collections.Generic;
using UnityEngine;

namespace TableLaughs
{
    public sealed class ScoreManager : MonoBehaviour
    {
        private const int SweepBonusBase = 250;

        public RoundScoreSummary ApplyStandardRoundScores(
            IReadOnlyList<AnswerSlot> answers,
            int pointsPerVote,
            int playerCount)
        {
            var summary = new RoundScoreSummary();
            var possibleVotes = Mathf.Max(0, playerCount - 1);

            foreach (var answer in answers)
            {
                Award(answer.Player, answer.Votes, possibleVotes, pointsPerVote, summary);
            }

            return summary;
        }

        public RoundScoreSummary ApplyFinalRoundScores(
            IReadOnlyList<FinalAnswer> finalAnswers,
            int pointsPerVote,
            int playerCount)
        {
            var summary = new RoundScoreSummary();
            var possibleVotes = Mathf.Max(0, playerCount - 1);

            foreach (var answer in finalAnswers)
            {
                Award(answer.Player, answer.Votes, possibleVotes, pointsPerVote, summary);
            }

            return summary;
        }

        private static void Award(
            PlayerData player,
            int votes,
            int possibleVotes,
            int pointsPerVote,
            RoundScoreSummary summary)
        {
            var points = votes * pointsPerVote;
            var earnedSweep = possibleVotes > 0 && votes == possibleVotes;
            if (earnedSweep)
            {
                points += SweepBonusBase * Mathf.Max(1, pointsPerVote / 100);
            }

            player.Score += points;

            if (points > 0)
            {
                summary.Lines.Add(earnedSweep
                    ? $"{player.DisplayName}: +{points} including a table sweep bonus"
                    : $"{player.DisplayName}: +{points}");
            }
        }
    }
}
