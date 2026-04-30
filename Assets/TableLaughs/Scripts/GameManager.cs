using System.Collections;
using UnityEngine;

namespace TableLaughs
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private float promptEntrySeconds = 75f;
        [SerializeField] private float voteSeconds = 25f;
        [SerializeField] private float revealSeconds = 2.1f;

        private PlayerManager playerManager;
        private PromptManager promptManager;
        private RoundManager roundManager;
        private VoteManager voteManager;
        private ScoreManager scoreManager;
        private UIManager uiManager;
        private SoundHooks soundHooks;
        private BoardInputBridge boardInputBridge;
        private Coroutine gameLoop;
        private GamePhase phase;

        public GamePhase CurrentPhase => phase;

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            promptManager = GetComponent<PromptManager>();
            roundManager = GetComponent<RoundManager>();
            voteManager = GetComponent<VoteManager>();
            scoreManager = GetComponent<ScoreManager>();
            uiManager = GetComponent<UIManager>();
            soundHooks = GetComponent<SoundHooks>();
            boardInputBridge = GetComponent<BoardInputBridge>();

            promptManager.LoadPromptPack();
            boardInputBridge.NoteFutureBoardIntegrations();
            uiManager.Initialize(boardInputBridge, soundHooks);
        }

        private void Start()
        {
            ShowTitle();
        }

        public void ShowTitle()
        {
            StopActiveLoop();
            phase = GamePhase.Title;
            uiManager.ShowTitleScreen(BeginJoin);
        }

        public void BeginJoin()
        {
            StopActiveLoop();
            phase = GamePhase.Join;
            playerManager.ClearPlayers();
            RefreshJoinScreen();
        }

        private void RefreshJoinScreen()
        {
            uiManager.ShowJoinScreen(
                playerManager.Players,
                HandleSeatTapped,
                HandlePlayerNameChanged,
                HandlePlayerColorCycle,
                HandlePlayerLeave,
                StartGame);
        }

        private PlayerData HandleSeatTapped(int seatIndex)
        {
            var player = playerManager.JoinSeat(seatIndex);
            RefreshJoinScreen();
            return player;
        }

        private void HandlePlayerNameChanged(PlayerData player, string value)
        {
            playerManager.RenamePlayer(player.Id, value);
        }

        private void HandlePlayerColorCycle(PlayerData player)
        {
            playerManager.CyclePlayerColor(player.Id);
            RefreshJoinScreen();
        }

        private void HandlePlayerLeave(PlayerData player)
        {
            playerManager.LeaveSeat(player.Id);
            RefreshJoinScreen();
        }

        private void StartGame()
        {
            if (!playerManager.CanStartGame)
            {
                return;
            }

            StopActiveLoop();
            playerManager.ResetScores();
            promptManager.ResetForGame();
            gameLoop = StartCoroutine(RunGameLoop());
        }

        private IEnumerator RunGameLoop()
        {
            for (var round = 1; round <= 3; round++)
            {
                roundManager.BeginRound(round, playerManager.Players, promptManager);

                yield return RunPromptEntryPhase();

                if (roundManager.IsFinalRound)
                {
                    yield return RunFinalVotingPhase();
                }
                else
                {
                    yield return RunHeadToHeadVotingPhase();
                }

                var scoreSummary = roundManager.IsFinalRound
                    ? scoreManager.ApplyFinalRoundScores(roundManager.FinalAnswers, roundManager.PointsPerVote,
                        playerManager.Players.Count)
                    : scoreManager.ApplyStandardRoundScores(roundManager.CurrentMatchups, roundManager.PointsPerVote,
                        playerManager.Players.Count);

                if (!roundManager.IsFinalRound)
                {
                    var continueRequested = false;
                    phase = GamePhase.Scoring;
                    uiManager.ShowScoreboard(round, playerManager.GetLeaderboard(), scoreSummary,
                        () => continueRequested = true);

                    while (!continueRequested)
                    {
                        yield return null;
                    }
                }
            }

            phase = GamePhase.Winner;
            uiManager.ShowWinnerScreen(playerManager.GetLeaderboard(), BeginJoin);
            gameLoop = null;
        }

        private IEnumerator RunPromptEntryPhase()
        {
            phase = GamePhase.PromptEntry;
            if (roundManager.IsFinalRound)
            {
                uiManager.ShowFinalPromptEntry(
                    playerManager.Players,
                    roundManager.FinalAnswers,
                    promptEntrySeconds,
                    roundManager.SubmitFinalAnswer,
                    promptManager.GetRandomFallbackAnswer);
            }
            else
            {
                uiManager.ShowPromptEntry(
                    roundManager.CurrentRound,
                    playerManager.Players,
                    roundManager.CurrentAnswerSlots,
                    promptEntrySeconds,
                    roundManager.SubmitAnswer,
                    promptManager.GetRandomFallbackAnswer);
            }

            var secondsRemaining = promptEntrySeconds;
            while (secondsRemaining > 0f && !roundManager.ArePromptTasksComplete())
            {
                uiManager.SetTimer(secondsRemaining);
                secondsRemaining -= Time.deltaTime;
                yield return null;
            }

            roundManager.FillMissingAnswers(promptManager);
            uiManager.SetTimer(0f);
            yield return new WaitForSeconds(0.25f);
        }

        private IEnumerator RunHeadToHeadVotingPhase()
        {
            phase = GamePhase.Voting;
            foreach (var matchup in roundManager.CurrentMatchups)
            {
                voteManager.BeginHeadToHeadVote(matchup, playerManager.Players);
                uiManager.ShowHeadToHeadVoting(
                    roundManager.CurrentRound,
                    matchup,
                    playerManager.Players,
                    voteSeconds,
                    voteManager.CastHeadToHeadVote);

                yield return WaitForVotesOrTimer();

                uiManager.ShowHeadToHeadResult(matchup);
                yield return new WaitForSeconds(revealSeconds);
            }
        }

        private IEnumerator RunFinalVotingPhase()
        {
            phase = GamePhase.Voting;
            voteManager.BeginFinalVote(roundManager.FinalAnswers, playerManager.Players);
            uiManager.ShowFinalVoting(
                playerManager.Players,
                roundManager.FinalAnswers,
                voteSeconds + 15f,
                voteManager.CastFinalVote);

            yield return WaitForVotesOrTimer(voteSeconds + 15f);

            uiManager.ShowFinalResult(roundManager.FinalAnswers);
            yield return new WaitForSeconds(revealSeconds + 0.8f);
        }

        private IEnumerator WaitForVotesOrTimer()
        {
            yield return WaitForVotesOrTimer(voteSeconds);
        }

        private IEnumerator WaitForVotesOrTimer(float seconds)
        {
            var secondsRemaining = seconds;
            while (secondsRemaining > 0f && !voteManager.AllEligibleVotesSubmitted)
            {
                uiManager.SetTimer(secondsRemaining);
                secondsRemaining -= Time.deltaTime;
                yield return null;
            }

            uiManager.SetTimer(0f);
            yield return new WaitForSeconds(0.2f);
        }

        private void StopActiveLoop()
        {
            if (gameLoop != null)
            {
                StopCoroutine(gameLoop);
                gameLoop = null;
            }
        }
    }
}
