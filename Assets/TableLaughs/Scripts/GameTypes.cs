using System;
using System.Collections.Generic;
using UnityEngine;

namespace TableLaughs
{
    public enum GamePhase
    {
        Title,
        Join,
        PromptEntry,
        Voting,
        Scoring,
        Winner
    }

    [Serializable]
    public sealed class PlayerData
    {
        public int Id;
        public int SeatIndex;
        public string DisplayName;
        public Color Color;
        public int Score;

        public PlayerData(int id, int seatIndex, string displayName, Color color)
        {
            Id = id;
            SeatIndex = seatIndex;
            DisplayName = displayName;
            Color = color;
            Score = 0;
        }
    }

    [Serializable]
    public sealed class PromptEntry
    {
        public string id;
        public string text;
        public string type;
    }

    [Serializable]
    public sealed class PromptPackData
    {
        public string packName;
        public PromptEntry[] prompts;
        public PromptEntry[] finalPrompts;
        public string[] randomAnswers;
    }

    public sealed class Matchup
    {
        public PromptEntry Prompt;
        public PlayerData PlayerA;
        public PlayerData PlayerB;
        public string AnswerA = string.Empty;
        public string AnswerB = string.Empty;
        public HandwritingAnswer HandwritingA = HandwritingAnswer.Blank();
        public HandwritingAnswer HandwritingB = HandwritingAnswer.Blank();
        public int VotesA;
        public int VotesB;

        public IReadOnlyList<int> SubmitterIds => new[] { PlayerA.Id, PlayerB.Id };

        public bool HasSubmitter(int playerId)
        {
            return PlayerA.Id == playerId || PlayerB.Id == playerId;
        }

        public string GetAnswer(int answerIndex)
        {
            return answerIndex == 0 ? AnswerA : AnswerB;
        }

        public HandwritingAnswer GetHandwritingAnswer(int answerIndex)
        {
            return answerIndex == 0 ? HandwritingA : HandwritingB;
        }

        public PlayerData GetAnswerPlayer(int answerIndex)
        {
            return answerIndex == 0 ? PlayerA : PlayerB;
        }

        public void AddVote(int answerIndex)
        {
            if (answerIndex == 0)
            {
                VotesA++;
            }
            else
            {
                VotesB++;
            }
        }
    }

    public sealed class AnswerSlot
    {
        public Matchup Matchup;
        public PlayerData Player;
        public bool IsFirstAnswer;
        public string Answer = string.Empty;
        public HandwritingAnswer Handwriting = HandwritingAnswer.Blank();
        public bool Submitted;

        public PromptEntry Prompt => Matchup.Prompt;
    }

    public sealed class FinalAnswer
    {
        public PromptEntry Prompt;
        public PlayerData Player;
        public string Answer = string.Empty;
        public HandwritingAnswer Handwriting = HandwritingAnswer.Blank();
        public bool Submitted;
        public int Votes;
    }

    [Serializable]
    public sealed class HandwritingStroke
    {
        public readonly List<Vector2> Points = new List<Vector2>();

        public HandwritingStroke Clone()
        {
            var clone = new HandwritingStroke();
            clone.Points.AddRange(Points);
            return clone;
        }
    }

    [Serializable]
    public sealed class HandwritingAnswer
    {
        public string Text = string.Empty;
        public readonly List<HandwritingStroke> Strokes = new List<HandwritingStroke>();

        public bool HasInk
        {
            get
            {
                for (var i = 0; i < Strokes.Count; i++)
                {
                    if (Strokes[i].Points.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static HandwritingAnswer Blank()
        {
            return new HandwritingAnswer();
        }

        public static HandwritingAnswer FromText(string text)
        {
            return new HandwritingAnswer
            {
                Text = text ?? string.Empty
            };
        }

        public HandwritingAnswer Clone()
        {
            var clone = new HandwritingAnswer
            {
                Text = Text ?? string.Empty
            };

            for (var i = 0; i < Strokes.Count; i++)
            {
                clone.Strokes.Add(Strokes[i].Clone());
            }

            return clone;
        }
    }

    public sealed class RoundScoreSummary
    {
        public readonly List<string> Lines = new List<string>();
    }
}
