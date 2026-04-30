using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TableLaughs
{
    public sealed class RoundManager : MonoBehaviour
    {
        private readonly List<Matchup> currentMatchups = new List<Matchup>();
        private readonly List<AnswerSlot> currentAnswerSlots = new List<AnswerSlot>();
        private readonly List<FinalAnswer> finalAnswers = new List<FinalAnswer>();

        public int CurrentRound { get; private set; } = 1;
        public bool IsFinalRound => CurrentRound == 3;
        public IReadOnlyList<Matchup> CurrentMatchups => currentMatchups;
        public IReadOnlyList<AnswerSlot> CurrentAnswerSlots => currentAnswerSlots;
        public IReadOnlyList<FinalAnswer> FinalAnswers => finalAnswers;
        public PromptEntry CurrentFinalPrompt { get; private set; }

        public int PointsPerVote
        {
            get
            {
                if (CurrentRound == 1)
                {
                    return 100;
                }

                return CurrentRound == 2 ? 200 : 300;
            }
        }

        public void BeginRound(int roundNumber, IReadOnlyList<PlayerData> players, PromptManager promptManager)
        {
            CurrentRound = Mathf.Clamp(roundNumber, 1, 3);
            currentMatchups.Clear();
            currentAnswerSlots.Clear();
            finalAnswers.Clear();
            CurrentFinalPrompt = null;

            if (IsFinalRound)
            {
                BuildFinalRound(players, promptManager);
            }
            else
            {
                BuildStandardRound(players, promptManager);
            }
        }

        public bool ArePromptTasksComplete()
        {
            if (IsFinalRound)
            {
                return finalAnswers.All(answer => answer.Submitted);
            }

            return currentAnswerSlots.All(slot => slot.Submitted);
        }

        public void SubmitAnswer(AnswerSlot slot, string answer)
        {
            if (slot == null || slot.Submitted)
            {
                return;
            }

            slot.Answer = CleanAnswer(answer);
            slot.Submitted = true;

            if (slot.IsFirstAnswer)
            {
                slot.Matchup.AnswerA = slot.Answer;
            }
            else
            {
                slot.Matchup.AnswerB = slot.Answer;
            }
        }

        public void SubmitFinalAnswer(FinalAnswer finalAnswer, string answer)
        {
            if (finalAnswer == null || finalAnswer.Submitted)
            {
                return;
            }

            finalAnswer.Answer = CleanAnswer(answer);
            finalAnswer.Submitted = true;
        }

        public void FillMissingAnswers(PromptManager promptManager)
        {
            if (IsFinalRound)
            {
                foreach (var finalAnswer in finalAnswers.Where(answer => !answer.Submitted))
                {
                    SubmitFinalAnswer(finalAnswer, promptManager.GetRandomFallbackAnswer());
                }

                return;
            }

            foreach (var slot in currentAnswerSlots.Where(slot => !slot.Submitted))
            {
                SubmitAnswer(slot, promptManager.GetRandomFallbackAnswer());
            }
        }

        private void BuildStandardRound(IReadOnlyList<PlayerData> players, PromptManager promptManager)
        {
            var shuffledPlayers = players.ToList();
            Shuffle(shuffledPlayers);

            for (var i = 0; i < shuffledPlayers.Count; i += 2)
            {
                var playerA = shuffledPlayers[i];
                var playerB = i + 1 < shuffledPlayers.Count ? shuffledPlayers[i + 1] : shuffledPlayers[0];
                var matchup = new Matchup
                {
                    Prompt = promptManager.DrawStandardPrompt(),
                    PlayerA = playerA,
                    PlayerB = playerB
                };

                currentMatchups.Add(matchup);
                currentAnswerSlots.Add(new AnswerSlot
                {
                    Matchup = matchup,
                    Player = playerA,
                    IsFirstAnswer = true
                });
                currentAnswerSlots.Add(new AnswerSlot
                {
                    Matchup = matchup,
                    Player = playerB,
                    IsFirstAnswer = false
                });
            }
        }

        private void BuildFinalRound(IReadOnlyList<PlayerData> players, PromptManager promptManager)
        {
            CurrentFinalPrompt = promptManager.DrawFinalPrompt();
            foreach (var player in players)
            {
                finalAnswers.Add(new FinalAnswer
                {
                    Prompt = CurrentFinalPrompt,
                    Player = player
                });
            }
        }

        private static string CleanAnswer(string answer)
        {
            var cleaned = string.IsNullOrWhiteSpace(answer) ? "A tiny parade of waffles" : answer.Trim();
            return cleaned.Length > 80 ? cleaned.Substring(0, 80) : cleaned;
        }

        private void Shuffle<T>(IList<T> values)
        {
            var random = new System.Random(unchecked(Environment.TickCount + CurrentRound * 7919));
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }
    }
}
