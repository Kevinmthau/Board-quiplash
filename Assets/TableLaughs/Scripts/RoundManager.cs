using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TableLaughs
{
    public sealed class RoundManager : MonoBehaviour
    {
        private readonly List<AnswerSlot> currentAnswerSlots = new List<AnswerSlot>();
        private readonly List<FinalAnswer> finalAnswers = new List<FinalAnswer>();

        public int CurrentRound { get; private set; } = 1;
        public bool IsFinalRound => CurrentRound == 3;
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
            SubmitAnswer(slot, HandwritingAnswer.FromText(answer));
        }

        public void SubmitAnswer(AnswerSlot slot, HandwritingAnswer answer)
        {
            if (slot == null || slot.Submitted)
            {
                return;
            }

            var submittedAnswer = CleanHandwritingAnswer(answer, "Handwritten answer");
            slot.Answer = submittedAnswer.Text;
            slot.Handwriting = submittedAnswer;
            slot.Submitted = true;
        }

        public void SubmitFinalAnswer(FinalAnswer finalAnswer, string answer)
        {
            SubmitFinalAnswer(finalAnswer, HandwritingAnswer.FromText(answer));
        }

        public void SubmitFinalAnswer(FinalAnswer finalAnswer, HandwritingAnswer answer)
        {
            if (finalAnswer == null || finalAnswer.Submitted)
            {
                return;
            }

            var submittedAnswer = CleanHandwritingAnswer(answer, "Handwritten answer");
            finalAnswer.Answer = submittedAnswer.Text;
            finalAnswer.Handwriting = submittedAnswer;
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
            var sharedPrompt = promptManager.DrawStandardPrompt();
            foreach (var player in players)
            {
                currentAnswerSlots.Add(new AnswerSlot
                {
                    Prompt = sharedPrompt,
                    Player = player
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

        private static HandwritingAnswer CleanHandwritingAnswer(HandwritingAnswer answer, string inkFallback)
        {
            var cleanedAnswer = answer?.Clone() ?? HandwritingAnswer.Blank();
            cleanedAnswer.Text = CleanAnswer(cleanedAnswer.Text, cleanedAnswer.HasInk ? inkFallback : "A tiny parade of waffles");
            return cleanedAnswer;
        }

        private static string CleanAnswer(string answer, string blankFallback)
        {
            var cleaned = string.IsNullOrWhiteSpace(answer) ? blankFallback : answer.Trim();
            return cleaned.Length > 80 ? cleaned.Substring(0, 80) : cleaned;
        }
    }
}
