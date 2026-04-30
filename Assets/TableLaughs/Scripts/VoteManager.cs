using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TableLaughs
{
    public sealed class VoteManager : MonoBehaviour
    {
        private readonly HashSet<int> eligibleVoters = new HashSet<int>();
        private readonly HashSet<int> votersWhoVoted = new HashSet<int>();

        private IReadOnlyList<AnswerSlot> activeRoundAnswers;
        private IReadOnlyList<FinalAnswer> activeFinalAnswers;

        public int EligibleVoteCount => eligibleVoters.Count;
        public int SubmittedVoteCount => votersWhoVoted.Count;
        public bool AllEligibleVotesSubmitted => eligibleVoters.Count > 0 && votersWhoVoted.Count >= eligibleVoters.Count;

        public void BeginRoundVote(IReadOnlyList<AnswerSlot> roundAnswers, IReadOnlyList<PlayerData> players)
        {
            activeRoundAnswers = roundAnswers;
            activeFinalAnswers = null;
            eligibleVoters.Clear();
            votersWhoVoted.Clear();

            foreach (var player in players)
            {
                eligibleVoters.Add(player.Id);
            }
        }

        public bool CastRoundVote(PlayerData voter, int answerIndex)
        {
            if (activeRoundAnswers == null || voter == null || answerIndex < 0 || answerIndex >= activeRoundAnswers.Count)
            {
                return false;
            }

            if (!eligibleVoters.Contains(voter.Id) || votersWhoVoted.Contains(voter.Id))
            {
                return false;
            }

            var selectedAnswer = activeRoundAnswers[answerIndex];
            if (selectedAnswer.Player.Id == voter.Id)
            {
                return false;
            }

            selectedAnswer.Votes++;
            votersWhoVoted.Add(voter.Id);
            return true;
        }

        public void BeginFinalVote(IReadOnlyList<FinalAnswer> finalAnswers, IReadOnlyList<PlayerData> players)
        {
            activeRoundAnswers = null;
            activeFinalAnswers = finalAnswers;
            eligibleVoters.Clear();
            votersWhoVoted.Clear();

            foreach (var player in players)
            {
                eligibleVoters.Add(player.Id);
            }
        }

        public bool CastFinalVote(PlayerData voter, int answerIndex)
        {
            if (activeFinalAnswers == null || voter == null || answerIndex < 0 || answerIndex >= activeFinalAnswers.Count)
            {
                return false;
            }

            if (!eligibleVoters.Contains(voter.Id) || votersWhoVoted.Contains(voter.Id))
            {
                return false;
            }

            var selectedAnswer = activeFinalAnswers[answerIndex];
            if (selectedAnswer.Player.Id == voter.Id)
            {
                return false;
            }

            selectedAnswer.Votes++;
            votersWhoVoted.Add(voter.Id);
            return true;
        }

        public bool HasVoted(PlayerData player)
        {
            return player != null && votersWhoVoted.Contains(player.Id);
        }

        public bool IsEligible(PlayerData player)
        {
            return player != null && eligibleVoters.Contains(player.Id);
        }

        public List<int> GetMissingVoterIds()
        {
            return eligibleVoters.Where(id => !votersWhoVoted.Contains(id)).ToList();
        }
    }
}
