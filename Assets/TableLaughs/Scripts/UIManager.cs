using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TableLaughs
{
    public sealed class UIManager : MonoBehaviour
    {
        private static readonly Vector2[] SeatPositions =
        {
            new Vector2(-560f, -430f),
            new Vector2(560f, -430f),
            new Vector2(825f, 0f),
            new Vector2(560f, 430f),
            new Vector2(-560f, 430f),
            new Vector2(-825f, 0f)
        };

        private static readonly float[] SeatRotations = { 0f, 0f, 90f, 180f, 180f, -90f };

        private readonly Color backgroundColor = new Color(0.055f, 0.075f, 0.095f);
        private readonly Color panelColor = new Color(0.12f, 0.15f, 0.18f, 0.96f);
        private readonly Color softPanelColor = new Color(0.18f, 0.22f, 0.26f, 0.96f);
        private readonly Color textColor = new Color(0.94f, 0.96f, 0.95f);
        private readonly Color mutedTextColor = new Color(0.68f, 0.73f, 0.75f);
        private readonly Color accentColor = new Color(0.98f, 0.78f, 0.22f);
        private readonly Color secondAccentColor = new Color(0.24f, 0.78f, 0.88f);
        private readonly Color paperColor = new Color(0.98f, 0.96f, 0.89f);
        private readonly Color inkColor = new Color(0.045f, 0.055f, 0.065f);

        private Canvas canvas;
        private RectTransform canvasRect;
        private GameObject currentScreen;
        private GameObject keyboardOverlay;
        private Text timerText;
        private Font defaultFont;
        private BoardInputBridge boardInputBridge;
        private SoundHooks soundHooks;

        public void Initialize(BoardInputBridge inputBridge, SoundHooks hooks)
        {
            boardInputBridge = inputBridge;
            soundHooks = hooks;
            EnsureCamera();
            EnsureCanvas();
            EnsureEventSystem();
        }

        public void ShowTitleScreen(Action onStart)
        {
            var screen = CreateScreen("Title Screen");

            CreateText(screen.transform, "Table Laughs", 86, accentColor, TextAnchor.MiddleCenter, FontStyle.Bold,
                new Vector2(0f, 120f), new Vector2(1100f, 130f));
            CreateText(screen.transform,
                $"A tabletop comedy prompt game for {PlayerManager.MinPlayers}-{PlayerManager.MaxPlayers} players", 34, textColor,
                TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0f, 40f), new Vector2(1100f, 70f));

            CreateButton(screen.transform, "Start", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                onStart?.Invoke();
            }, accentColor, new Vector2(0f, -90f), new Vector2(340f, 100f), 34);

            CreateText(screen.transform, "No phones. No accounts. Just tap in around the table.", 26, mutedTextColor,
                TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0f, -210f), new Vector2(1100f, 60f));
        }

        public void ShowJoinScreen(
            IReadOnlyList<PlayerData> players,
            Func<int, PlayerData> onSeatTapped,
            Action<PlayerData, string> onNameChanged,
            Action<PlayerData> onColorCycle,
            Action<PlayerData> onLeave,
            Action onStart)
        {
            var screen = CreateScreen("Join Screen");

            CreateText(screen.transform, "Tap a seat to join", 54, accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 120f), new Vector2(900f, 80f));

            var readyText = players.Count >= PlayerManager.MinPlayers
                ? "Ready when the table is"
                : $"{PlayerManager.MinPlayers - players.Count} more player(s) needed";
            CreateText(screen.transform, readyText, 30, textColor, TextAnchor.MiddleCenter, FontStyle.Normal,
                new Vector2(0f, 55f), new Vector2(900f, 60f));

            var startButton = CreateButton(screen.transform, "Begin Game", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                onStart?.Invoke();
            }, secondAccentColor, new Vector2(0f, -65f), new Vector2(360f, 92f), 30);
            startButton.Button.interactable = players.Count >= PlayerManager.MinPlayers;

            CreateText(screen.transform, $"{players.Count}/{PlayerManager.MaxPlayers}", 34, mutedTextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, -160f), new Vector2(300f, 60f));

            for (var seatIndex = 0; seatIndex < PlayerManager.MaxPlayers; seatIndex++)
            {
                var player = players.FirstOrDefault(candidate => candidate.SeatIndex == seatIndex);
                CreateJoinSeat(screen.transform, seatIndex, player, onSeatTapped, onNameChanged, onColorCycle, onLeave);
            }
        }

        public void ShowPromptEntry(
            int roundNumber,
            IReadOnlyList<PlayerData> players,
            IReadOnlyList<AnswerSlot> answerSlots,
            float timeLimit,
            Action<AnswerSlot, HandwritingAnswer> onSubmit,
            Func<string> randomAnswerProvider)
        {
            var screen = CreateScreen("Prompt Entry");
            CreateRoundHeader(screen.transform, $"Round {roundNumber}", "Write on your paper", timeLimit);

            foreach (var player in players)
            {
                var playerSlots = answerSlots.Where(slot => slot.Player.Id == player.Id).ToList();
                if (playerSlots.Count == 0)
                {
                    continue;
                }

                CreatePromptSeatPanel(screen.transform, player, playerSlots, onSubmit, randomAnswerProvider);
            }
        }

        public void ShowFinalPromptEntry(
            IReadOnlyList<PlayerData> players,
            IReadOnlyList<FinalAnswer> finalAnswers,
            float timeLimit,
            Action<FinalAnswer, HandwritingAnswer> onSubmit,
            Func<string> randomAnswerProvider)
        {
            var screen = CreateScreen("Final Prompt Entry");
            CreateRoundHeader(screen.transform, "Final Round", "Write on your paper", timeLimit);

            foreach (var player in players)
            {
                var finalAnswer = finalAnswers.First(answer => answer.Player.Id == player.Id);
                CreateFinalPromptSeatPanel(screen.transform, player, finalAnswer, onSubmit, randomAnswerProvider);
            }
        }

        public void ShowRoundVoting(
            int roundNumber,
            IReadOnlyList<PlayerData> players,
            IReadOnlyList<AnswerSlot> answers,
            float timeLimit,
            Func<PlayerData, int, bool> onVote)
        {
            var screen = CreateScreen("Round Vote");
            CreateRoundHeader(screen.transform, $"Round {roundNumber}", "Choose a favorite that is not your own", timeLimit);

            if (answers.Count > 0)
            {
                CreateText(screen.transform, answers[0].Prompt.text, 36, textColor, TextAnchor.MiddleCenter,
                    FontStyle.Bold, new Vector2(0f, 270f), new Vector2(1240f, 90f));
            }

            var answerGrid = CreatePanel(screen.transform, "Round Answer Grid", new Vector2(0f, 10f),
                new Vector2(1280f, 480f), new Color(0f, 0f, 0f, 0f), 0f);
            var grid = answerGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 140f);
            grid.spacing = new Vector2(18f, 18f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (var i = 0; i < answers.Count; i++)
            {
                var answer = answers[i];
                var card = CreateLayoutPanel(answerGrid.transform, $"Round Answer {i + 1}",
                    Color.Lerp(answer.Player.Color, Color.black, 0.18f));
                CreateText(card.transform, (i + 1).ToString(), 24, Color.black, TextAnchor.MiddleCenter,
                    FontStyle.Bold, new Vector2(-118f, 42f), new Vector2(40f, 40f), accentColor);
                CreateAnswerPreview(card.transform, answer.Answer, answer.Handwriting, new Vector2(22f, 0f),
                    new Vector2(230f, 96f), 7f);
            }

            foreach (var player in players)
            {
                CreateRoundVotePanel(screen.transform, player, answers, onVote);
            }

            soundHooks?.Play(SfxCue.Reveal);
        }

        public void ShowFinalVoting(
            IReadOnlyList<PlayerData> players,
            IReadOnlyList<FinalAnswer> finalAnswers,
            float timeLimit,
            Func<PlayerData, int, bool> onVote)
        {
            var screen = CreateScreen("Final Vote");
            CreateRoundHeader(screen.transform, "Final Vote", "Choose a favorite that is not your own", timeLimit);

            CreateText(screen.transform, finalAnswers[0].Prompt.text, 36, textColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 270f), new Vector2(1240f, 90f));

            var answerGrid = CreatePanel(screen.transform, "Final Answer Grid", new Vector2(0f, 10f),
                new Vector2(1280f, 480f), new Color(0f, 0f, 0f, 0f), 0f);
            var grid = answerGrid.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 140f);
            grid.spacing = new Vector2(18f, 18f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (var i = 0; i < finalAnswers.Count; i++)
            {
                var answer = finalAnswers[i];
                var card = CreateLayoutPanel(answerGrid.transform, $"Final Answer {i + 1}",
                    Color.Lerp(answer.Player.Color, Color.black, 0.18f));
                CreateText(card.transform, (i + 1).ToString(), 24, Color.black, TextAnchor.MiddleCenter,
                    FontStyle.Bold, new Vector2(-118f, 42f), new Vector2(40f, 40f), accentColor);
                CreateAnswerPreview(card.transform, answer.Answer, answer.Handwriting, new Vector2(22f, 0f), new Vector2(230f, 96f), 7f);
            }

            foreach (var player in players)
            {
                CreateFinalVotePanel(screen.transform, player, finalAnswers, onVote);
            }

            soundHooks?.Play(SfxCue.Reveal);
        }

        public void ShowRoundResult(int roundNumber, IReadOnlyList<AnswerSlot> answers)
        {
            var screen = CreateScreen("Round Result");
            CreateText(screen.transform, $"Round {roundNumber} votes", 52, accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 310f), new Vector2(800f, 80f));

            var sorted = answers.OrderByDescending(answer => answer.Votes).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var answer = sorted[i];
                var row = CreatePanel(screen.transform, $"Round Result {i}", new Vector2(0f, 210f - i * 78f),
                    new Vector2(1120f, 64f), Color.Lerp(answer.Player.Color, Color.black, 0.35f), 0f);
                CreateText(row.transform, answer.Player.DisplayName, 23, Color.white,
                    TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(-390f, 0f), new Vector2(250f, 54f));
                CreateAnswerPreview(row.transform, answer.Answer, answer.Handwriting, new Vector2(20f, 0f),
                    new Vector2(480f, 48f), 5f);
                CreateText(row.transform, $"{answer.Votes} vote(s)", 25, Color.white, TextAnchor.MiddleRight,
                    FontStyle.Bold, new Vector2(420f, 0f), new Vector2(230f, 54f));
            }

            soundHooks?.Play(SfxCue.Score);
        }

        public void ShowFinalResult(IReadOnlyList<FinalAnswer> finalAnswers)
        {
            var screen = CreateScreen("Final Result");
            CreateText(screen.transform, "Final votes", 52, accentColor, TextAnchor.MiddleCenter, FontStyle.Bold,
                new Vector2(0f, 310f), new Vector2(800f, 80f));

            var sorted = finalAnswers.OrderByDescending(answer => answer.Votes).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var answer = sorted[i];
                var row = CreatePanel(screen.transform, $"Final Result {i}", new Vector2(0f, 210f - i * 78f),
                    new Vector2(1120f, 64f), Color.Lerp(answer.Player.Color, Color.black, 0.35f), 0f);
                CreateText(row.transform, answer.Player.DisplayName, 23, Color.white,
                    TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(-390f, 0f), new Vector2(250f, 54f));
                CreateAnswerPreview(row.transform, answer.Answer, answer.Handwriting, new Vector2(20f, 0f),
                    new Vector2(480f, 48f), 5f);
                CreateText(row.transform, $"{answer.Votes} vote(s)", 25, Color.white, TextAnchor.MiddleRight,
                    FontStyle.Bold, new Vector2(420f, 0f), new Vector2(230f, 54f));
            }

            soundHooks?.Play(SfxCue.Score);
        }

        public void ShowScoreboard(
            int roundNumber,
            IReadOnlyList<PlayerData> leaderboard,
            RoundScoreSummary scoreSummary,
            Action onContinue)
        {
            var screen = CreateScreen("Scoreboard");
            CreateText(screen.transform, $"Scores after round {roundNumber}", 52, accentColor,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 315f), new Vector2(1000f, 80f));

            for (var i = 0; i < leaderboard.Count; i++)
            {
                var player = leaderboard[i];
                var row = CreatePanel(screen.transform, $"Leaderboard {i}", new Vector2(0f, 220f - i * 72f),
                    new Vector2(980f, 58f), Color.Lerp(player.Color, Color.black, 0.30f), 0f);
                CreateText(row.transform, $"{i + 1}. {player.DisplayName}", 26, Color.white, TextAnchor.MiddleLeft,
                    FontStyle.Bold, new Vector2(-220f, 0f), new Vector2(470f, 52f));
                CreateText(row.transform, player.Score.ToString(), 28, Color.white, TextAnchor.MiddleRight,
                    FontStyle.Bold, new Vector2(340f, 0f), new Vector2(220f, 52f));
            }

            var summaryText = scoreSummary.Lines.Count == 0
                ? "No points this time. The table remains mysterious."
                : string.Join("   |   ", scoreSummary.Lines.Take(3));
            CreateText(screen.transform, summaryText, 23, mutedTextColor, TextAnchor.MiddleCenter, FontStyle.Normal,
                new Vector2(0f, -315f), new Vector2(1450f, 56f));

            CreateButton(screen.transform, "Next Round", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                onContinue?.Invoke();
            }, secondAccentColor, new Vector2(0f, -230f), new Vector2(300f, 80f), 28);
        }

        public void ShowWinnerScreen(IReadOnlyList<PlayerData> leaderboard, Action onPlayAgain)
        {
            var screen = CreateScreen("Winner Screen");
            var winner = leaderboard[0];

            CreateText(screen.transform, "Table Laughs Champion", 58, accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 295f), new Vector2(1150f, 85f));
            CreateText(screen.transform, winner.DisplayName, 76, winner.Color, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 205f), new Vector2(1100f, 95f));

            for (var i = 0; i < leaderboard.Count; i++)
            {
                var player = leaderboard[i];
                var row = CreatePanel(screen.transform, $"Winner Row {i}", new Vector2(0f, 105f - i * 58f),
                    new Vector2(900f, 48f), Color.Lerp(player.Color, Color.black, 0.34f), 0f);
                CreateText(row.transform, $"{i + 1}. {player.DisplayName}", 23, Color.white, TextAnchor.MiddleLeft,
                    FontStyle.Bold, new Vector2(-190f, 0f), new Vector2(430f, 44f));
                CreateText(row.transform, player.Score.ToString(), 24, Color.white, TextAnchor.MiddleRight,
                    FontStyle.Bold, new Vector2(300f, 0f), new Vector2(190f, 44f));
            }

            CreateButton(screen.transform, "Play Again", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                onPlayAgain?.Invoke();
            }, accentColor, new Vector2(0f, -330f), new Vector2(320f, 86f), 30);

            if (Application.isPlaying)
            {
                StartCoroutine(ConfettiBurst(screen.transform));
            }
            soundHooks?.Play(SfxCue.Win);
        }

        public void SetTimer(float secondsRemaining)
        {
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining)).ToString();
            }
        }

        private void CreateJoinSeat(
            Transform parent,
            int seatIndex,
            PlayerData player,
            Func<int, PlayerData> onSeatTapped,
            Action<PlayerData, string> onNameChanged,
            Action<PlayerData> onColorCycle,
            Action<PlayerData> onLeave)
        {
            var color = player == null ? softPanelColor : Color.Lerp(player.Color, Color.black, 0.22f);
            var panel = CreateSeatPanel(parent, $"Seat {seatIndex + 1}", seatIndex, new Vector2(390f, 172f), color);

            if (player == null)
            {
                CreateText(panel.transform, "Tap to join", 27, textColor, TextAnchor.MiddleCenter, FontStyle.Bold,
                    Vector2.zero, new Vector2(340f, 80f));
                var joinButton = panel.AddComponent<Button>();
                joinButton.transition = Selectable.Transition.ColorTint;
                joinButton.onClick.AddListener(() =>
                {
                    soundHooks?.Play(SfxCue.Tap);
                    onSeatTapped?.Invoke(seatIndex);
                });
                return;
            }

            CreateText(panel.transform, $"Seat {seatIndex + 1}", 18, mutedTextColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 58f), new Vector2(240f, 28f));

            ButtonBundle nameButton = null;
            nameButton = CreateButton(panel.transform, player.DisplayName, () =>
            {
                ShowKeyboard("Name", player.DisplayName, 16, SeatRotations[seatIndex], value =>
                {
                    onNameChanged?.Invoke(player, value);
                    nameButton.Label.text = string.IsNullOrWhiteSpace(value) ? $"Player {player.Id}" : value;
                });
            }, Color.Lerp(player.Color, Color.white, 0.18f), new Vector2(0f, 14f), new Vector2(310f, 54f), 20);

            CreateButton(panel.transform, "Color", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                onColorCycle?.Invoke(player);
            }, secondAccentColor, new Vector2(-78f, -48f), new Vector2(142f, 44f), 18);

            CreateButton(panel.transform, "Leave", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                onLeave?.Invoke(player);
            }, new Color(0.84f, 0.22f, 0.26f), new Vector2(78f, -48f), new Vector2(142f, 44f), 18);
        }

        private void CreatePromptSeatPanel(
            Transform parent,
            PlayerData player,
            List<AnswerSlot> slots,
            Action<AnswerSlot, HandwritingAnswer> onSubmit,
            Func<string> randomAnswerProvider)
        {
            var panel = CreateSeatPanel(parent, $"Prompt Panel {player.Id}", player.SeatIndex,
                new Vector2(430f, 245f), Color.Lerp(player.Color, Color.black, 0.30f));
            var state = new PromptPanelState
            {
                Player = player,
                Slots = slots,
                CurrentIndex = NextOpenIndex(slots)
            };

            CreateText(panel.transform, player.DisplayName, 20, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 99f), new Vector2(360f, 30f));
            state.ProgressLabel = CreateText(panel.transform, string.Empty, 17, mutedTextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 72f), new Vector2(360f, 26f));
            state.PromptLabel = CreateText(panel.transform, string.Empty, 20, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 24f), new Vector2(370f, 76f));

            state.PaperInput = CreateHandwritingPaper(panel.transform, $"Paper {player.Id}",
                new Vector2(0f, -42f), new Vector2(360f, 78f), true, null, value =>
            {
                state.DraftAnswer = value;
                var hasInk = value != null && value.HasInk;
                if (state.SubmitButton != null)
                {
                    state.SubmitButton.interactable = hasInk;
                }

                if (state.ClearButton != null)
                {
                    state.ClearButton.interactable = hasInk;
                }
            });

            var clearButton = CreateButton(panel.transform, "Clear", () =>
            {
                state.PaperInput.Clear();
                soundHooks?.Play(SfxCue.Tap);
            }, paperColor, new Vector2(-93f, -101f), new Vector2(170f, 38f), 16);
            state.ClearButton = clearButton.Button;

            var submitButton = CreateButton(panel.transform, "Submit", () =>
            {
                var activeSlot = state.ActiveSlot;
                if (activeSlot == null)
                {
                    return;
                }

                var answer = state.PaperInput != null ? state.PaperInput.GetAnswer() : state.DraftAnswer.Clone();
                if (!answer.HasInk && string.IsNullOrWhiteSpace(answer.Text))
                {
                    answer = HandwritingAnswer.FromText(randomAnswerProvider());
                }

                onSubmit?.Invoke(activeSlot, answer);
                state.CurrentIndex = NextOpenIndex(slots);
                state.DraftAnswer = HandwritingAnswer.Blank();
                RefreshPromptState(state);
                soundHooks?.Play(SfxCue.Tap);
            }, accentColor, new Vector2(93f, -101f), new Vector2(170f, 38f), 16);
            state.SubmitButton = submitButton.Button;

            RefreshPromptState(state);
        }

        private void CreateFinalPromptSeatPanel(
            Transform parent,
            PlayerData player,
            FinalAnswer finalAnswer,
            Action<FinalAnswer, HandwritingAnswer> onSubmit,
            Func<string> randomAnswerProvider)
        {
            var panel = CreateSeatPanel(parent, $"Final Prompt Panel {player.Id}", player.SeatIndex,
                new Vector2(430f, 245f), Color.Lerp(player.Color, Color.black, 0.30f));

            var draft = HandwritingAnswer.Blank();
            CreateText(panel.transform, player.DisplayName, 20, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 99f), new Vector2(360f, 30f));
            CreateText(panel.transform, finalAnswer.Prompt.text, 20, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 34f), new Vector2(370f, 100f));

            Button submitButton = null;
            Button clearButton = null;
            var paperInput = CreateHandwritingPaper(panel.transform, $"Final Paper {player.Id}",
                new Vector2(0f, -42f), new Vector2(360f, 78f), true, null, value =>
            {
                draft = value ?? HandwritingAnswer.Blank();
                var hasInk = draft.HasInk;
                if (submitButton != null)
                {
                    submitButton.interactable = hasInk;
                }

                if (clearButton != null)
                {
                    clearButton.interactable = hasInk;
                }
            });

            var clear = CreateButton(panel.transform, "Clear", () =>
            {
                paperInput.Clear();
                soundHooks?.Play(SfxCue.Tap);
            }, paperColor, new Vector2(-93f, -101f), new Vector2(170f, 38f), 16);
            clearButton = clear.Button;

            var submit = CreateButton(panel.transform, "Submit", () =>
            {
                if (finalAnswer.Submitted)
                {
                    return;
                }

                var answer = paperInput != null ? paperInput.GetAnswer() : draft.Clone();
                if (!answer.HasInk && string.IsNullOrWhiteSpace(answer.Text))
                {
                    answer = HandwritingAnswer.FromText(randomAnswerProvider());
                }

                onSubmit?.Invoke(finalAnswer, answer);
                paperInput.SetInputEnabled(false);
                clearButton.interactable = false;
                submitButton.interactable = false;
                soundHooks?.Play(SfxCue.Tap);
            }, accentColor, new Vector2(93f, -101f), new Vector2(170f, 38f), 16);
            submitButton = submit.Button;
            submitButton.interactable = false;
            clearButton.interactable = false;
        }

        private void CreateRoundVotePanel(
            Transform parent,
            PlayerData player,
            IReadOnlyList<AnswerSlot> answers,
            Func<PlayerData, int, bool> onVote)
        {
            var panel = CreateSeatPanel(parent, $"Round Vote Panel {player.Id}", player.SeatIndex,
                new Vector2(410f, 178f), Color.Lerp(player.Color, Color.black, 0.35f));
            CreateText(panel.transform, player.DisplayName, 19, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 64f), new Vector2(330f, 30f));
            var status = CreateText(panel.transform, "Vote", 17, mutedTextColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 39f), new Vector2(330f, 24f));

            var gridObject = CreatePanel(panel.transform, "Vote Number Grid", new Vector2(0f, -25f),
                new Vector2(330f, 105f), new Color(0f, 0f, 0f, 0f), 0f);
            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(70f, 42f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var buttons = new List<Button>();
            for (var i = 0; i < answers.Count; i++)
            {
                var answerIndex = i;
                var isOwnAnswer = answers[i].Player.Id == player.Id;
                var button = CreateLayoutButton(gridObject.transform, (i + 1).ToString(), () =>
                {
                    if (onVote(player, answerIndex))
                    {
                        status.text = "Voted";
                        foreach (var choiceButton in buttons)
                        {
                            choiceButton.interactable = false;
                        }

                        soundHooks?.Play(SfxCue.Vote);
                    }
                }, isOwnAnswer ? mutedTextColor : accentColor, 22);
                button.Button.interactable = !isOwnAnswer;
                buttons.Add(button.Button);
            }
        }

        private void CreateFinalVotePanel(
            Transform parent,
            PlayerData player,
            IReadOnlyList<FinalAnswer> finalAnswers,
            Func<PlayerData, int, bool> onVote)
        {
            var panel = CreateSeatPanel(parent, $"Final Vote Panel {player.Id}", player.SeatIndex,
                new Vector2(410f, 178f), Color.Lerp(player.Color, Color.black, 0.35f));
            CreateText(panel.transform, player.DisplayName, 19, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 64f), new Vector2(330f, 30f));
            var status = CreateText(panel.transform, "Vote", 17, mutedTextColor, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 39f), new Vector2(330f, 24f));

            var gridObject = CreatePanel(panel.transform, "Vote Number Grid", new Vector2(0f, -25f),
                new Vector2(330f, 105f), new Color(0f, 0f, 0f, 0f), 0f);
            var grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(70f, 42f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var buttons = new List<Button>();
            for (var i = 0; i < finalAnswers.Count; i++)
            {
                var answerIndex = i;
                var button = CreateLayoutButton(gridObject.transform, (i + 1).ToString(), () =>
                {
                    if (onVote(player, answerIndex))
                    {
                        status.text = "Voted";
                        foreach (var choiceButton in buttons)
                        {
                            choiceButton.interactable = false;
                        }

                        soundHooks?.Play(SfxCue.Vote);
                    }
                }, finalAnswers[i].Player.Id == player.Id ? mutedTextColor : accentColor, 22);
                button.Button.interactable = finalAnswers[i].Player.Id != player.Id;
                buttons.Add(button.Button);
            }
        }

        private void RefreshPromptState(PromptPanelState state)
        {
            if (state.CurrentIndex >= state.Slots.Count)
            {
                state.ProgressLabel.text = "All set";
                state.PromptLabel.text = "Ready for voting";
                state.PaperInput.SetAnswer(HandwritingAnswer.Blank());
                state.PaperInput.SetInputEnabled(false);
                state.SubmitButton.interactable = false;
                state.ClearButton.interactable = false;
                return;
            }

            var slot = state.ActiveSlot;
            state.DraftAnswer = HandwritingAnswer.Blank();
            state.ProgressLabel.text = state.Slots.Count == 1
                ? "Prompt"
                : $"Prompt {state.CurrentIndex + 1}/{state.Slots.Count}";
            state.PromptLabel.text = slot.Prompt.text;
            state.PaperInput.SetAnswer(HandwritingAnswer.Blank());
            state.PaperInput.SetInputEnabled(true);
            state.SubmitButton.interactable = false;
            state.ClearButton.interactable = false;
        }

        private static int NextOpenIndex(IReadOnlyList<AnswerSlot> slots)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (!slots[i].Submitted)
                {
                    return i;
                }
            }

            return slots.Count;
        }

        private void CreateRoundHeader(Transform parent, string leftText, string centerText, float timeLimit)
        {
            CreateText(parent, leftText, 32, accentColor, TextAnchor.MiddleLeft, FontStyle.Bold,
                new Vector2(-760f, 500f), new Vector2(360f, 54f));
            CreateText(parent, centerText, 34, textColor, TextAnchor.MiddleCenter, FontStyle.Bold,
                new Vector2(0f, 500f), new Vector2(800f, 54f));

            var timerPanel = CreatePanel(parent, "Timer", new Vector2(800f, 500f), new Vector2(120f, 64f),
                Color.Lerp(accentColor, Color.black, 0.20f), 0f);
            timerText = CreateText(timerPanel.transform, Mathf.CeilToInt(timeLimit).ToString(), 34, Color.black,
                TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(112f, 58f));
        }

        private void CreateAnswerPreview(
            Transform parent,
            string fallbackText,
            HandwritingAnswer handwriting,
            Vector2 position,
            Vector2 size,
            float strokeThickness)
        {
            if (handwriting != null && handwriting.HasInk)
            {
                CreateHandwritingPaper(parent, "Handwritten Answer", position, size, false, handwriting, null, strokeThickness);
                return;
            }

            CreateText(parent, fallbackText, 31, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, position, size);
        }

        private HandwritingPaperInput CreateHandwritingPaper(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            bool interactive,
            HandwritingAnswer initialAnswer,
            Action<HandwritingAnswer> onChanged,
            float strokeThickness = 7f)
        {
            var paper = CreatePanel(parent, name, position, size, paperColor, 0f);
            var image = paper.GetComponent<Image>();
            image.raycastTarget = interactive;

            var outline = paper.AddComponent<Outline>();
            outline.effectColor = new Color(0.44f, 0.37f, 0.26f, 0.34f);
            outline.effectDistance = new Vector2(2f, -2f);

            var input = paper.AddComponent<HandwritingPaperInput>();
            input.Initialize(inkColor, strokeThickness, onChanged);
            input.SetAnswer(initialAnswer ?? HandwritingAnswer.Blank());
            input.SetInputEnabled(interactive);
            return input;
        }

        private void ShowKeyboard(string title, string initialValue, int maxLength, float rotation, Action<string> onChanged)
        {
            if (keyboardOverlay != null)
            {
                DestroyUiObject(keyboardOverlay);
            }

            var value = initialValue ?? string.Empty;
            keyboardOverlay = new GameObject("Table Laughs Keyboard", typeof(RectTransform), typeof(Image));
            keyboardOverlay.transform.SetParent(canvas.transform, false);
            var keyboardRect = keyboardOverlay.GetComponent<RectTransform>();
            keyboardRect.anchorMin = new Vector2(0.5f, 0.5f);
            keyboardRect.anchorMax = new Vector2(0.5f, 0.5f);
            keyboardRect.anchoredPosition = Vector2.zero;
            keyboardRect.sizeDelta = new Vector2(840f, 500f);
            keyboardRect.localEulerAngles = new Vector3(0f, 0f, rotation);
            keyboardOverlay.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.055f, 0.985f);

            CreateText(keyboardOverlay.transform, title, 28, accentColor, TextAnchor.MiddleCenter, FontStyle.Bold,
                new Vector2(0f, 214f), new Vector2(720f, 48f));
            var display = CreateText(keyboardOverlay.transform, string.Empty, 26, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold, new Vector2(0f, 158f), new Vector2(720f, 54f), softPanelColor);

            void UpdateDisplay()
            {
                display.text = string.IsNullOrEmpty(value) ? " " : value;
                onChanged?.Invoke(value);
            }

            var rows = new[] { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex];
                var rowWidth = row.Length * 64f;
                var startX = -rowWidth * 0.5f + 32f;
                for (var keyIndex = 0; keyIndex < row.Length; keyIndex++)
                {
                    var character = row[keyIndex].ToString();
                    CreateButton(keyboardOverlay.transform, character, () =>
                    {
                        if (value.Length < maxLength)
                        {
                            value += character;
                            UpdateDisplay();
                        }
                    }, Color.Lerp(secondAccentColor, Color.white, 0.08f),
                        new Vector2(startX + keyIndex * 64f, 82f - rowIndex * 64f), new Vector2(56f, 52f), 21);
                }
            }

            CreateButton(keyboardOverlay.transform, "Space", () =>
            {
                if (value.Length < maxLength)
                {
                    value += " ";
                    UpdateDisplay();
                }
            }, softPanelColor, new Vector2(-205f, -145f), new Vector2(210f, 54f), 20);

            CreateButton(keyboardOverlay.transform, "Back", () =>
            {
                if (value.Length > 0)
                {
                    value = value.Substring(0, value.Length - 1);
                    UpdateDisplay();
                }
            }, softPanelColor, new Vector2(35f, -145f), new Vector2(150f, 54f), 20);

            CreateButton(keyboardOverlay.transform, "Done", () =>
            {
                soundHooks?.Play(SfxCue.Tap);
                DestroyUiObject(keyboardOverlay);
                keyboardOverlay = null;
            }, accentColor, new Vector2(245f, -145f), new Vector2(170f, 54f), 20);

            UpdateDisplay();
        }

        private IEnumerator ConfettiBurst(Transform parent)
        {
            for (var i = 0; i < 72; i++)
            {
                var piece = new GameObject("Confetti", typeof(RectTransform), typeof(Image), typeof(ConfettiPiece));
                piece.transform.SetParent(parent, false);
                var rect = piece.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(0f, 90f));
                rect.sizeDelta = new Vector2(UnityEngine.Random.Range(10f, 22f), UnityEngine.Random.Range(10f, 22f));
                piece.GetComponent<Image>().color = Color.HSVToRGB(UnityEngine.Random.value, 0.72f, 1f);
                piece.GetComponent<ConfettiPiece>().Initialize(new Vector2(
                    UnityEngine.Random.Range(-460f, 460f),
                    UnityEngine.Random.Range(180f, 620f)));
                yield return new WaitForSeconds(0.008f);
            }
        }

        private GameObject CreateScreen(string name)
        {
            if (currentScreen != null)
            {
                DestroyUiObject(currentScreen);
            }

            if (keyboardOverlay != null)
            {
                DestroyUiObject(keyboardOverlay);
                keyboardOverlay = null;
            }

            timerText = null;
            currentScreen = new GameObject(name, typeof(RectTransform), typeof(Image));
            currentScreen.transform.SetParent(canvas.transform, false);
            var rect = currentScreen.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            currentScreen.GetComponent<Image>().color = backgroundColor;
            return currentScreen;
        }

        private GameObject CreateSeatPanel(Transform parent, string name, int seatIndex, Vector2 size, Color color)
        {
            var panel = CreatePanel(parent, name, SeatPositions[seatIndex], size, color, SeatRotations[seatIndex]);
            return panel;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color, float rotation)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private GameObject CreateLayoutPanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private Text CreateText(
            Transform parent,
            string value,
            int fontSize,
            Color color,
            TextAnchor alignment,
            FontStyle style,
            Vector2 position,
            Vector2 size,
            Color? background = null)
        {
            var container = new GameObject("Text", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            GameObject textObject = container;
            if (background.HasValue)
            {
                var image = container.AddComponent<Image>();
                image.color = background.Value;

                textObject = new GameObject("Text Label", typeof(RectTransform));
                textObject.transform.SetParent(container.transform, false);
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, fontSize - 10);
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private ButtonBundle CreateButton(
            Transform parent,
            string label,
            Action onClick,
            Color color,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var buttonObject = new GameObject($"Button {label}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(buttonObject.transform, label, fontSize, Color.black, TextAnchor.MiddleCenter,
                FontStyle.Bold, Vector2.zero, new Vector2(size.x - 16f, size.y - 10f));

            return new ButtonBundle(buttonObject, button, text);
        }

        private ButtonBundle CreateLayoutButton(Transform parent, string label, Action onClick, Color color, int fontSize)
        {
            var buttonObject = new GameObject($"Button {label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(buttonObject.transform, label, fontSize, Color.black, TextAnchor.MiddleCenter,
                FontStyle.Bold, Vector2.zero, new Vector2(64f, 38f));
            return new ButtonBundle(buttonObject, button, text);
        }

        private void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        private void DestroyUiObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void EnsureCanvas()
        {
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("TableLaughsCanvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            canvasRect = canvas.GetComponent<RectTransform>();
        }

        private void EnsureEventSystem()
        {
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            boardInputBridge?.EnsureBoardUiInputModule(eventSystem);
        }

        private Font DefaultFont
        {
            get
            {
                if (defaultFont != null)
                {
                    return defaultFont;
                }

                defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (defaultFont == null)
                {
                    defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                return defaultFont;
            }
        }

        private sealed class PromptPanelState
        {
            public PlayerData Player;
            public List<AnswerSlot> Slots;
            public int CurrentIndex;
            public HandwritingAnswer DraftAnswer = HandwritingAnswer.Blank();
            public Text ProgressLabel;
            public Text PromptLabel;
            public HandwritingPaperInput PaperInput;
            public Button SubmitButton;
            public Button ClearButton;

            public AnswerSlot ActiveSlot => CurrentIndex < Slots.Count ? Slots[CurrentIndex] : null;
        }

        private sealed class ButtonBundle
        {
            public readonly GameObject GameObject;
            public readonly Button Button;
            public readonly Text Label;

            public ButtonBundle(GameObject gameObject, Button button, Text label)
            {
                GameObject = gameObject;
                Button = button;
                Label = label;
            }
        }
    }
}
