using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TableLaughs.EditorTools
{
    public static class TableLaughsProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/TableLaughs.unity";

        [MenuItem("Table Laughs/Create Playable Scene", priority = 100)]
        public static void CreatePlayableScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TableLaughs";

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.095f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var root = new GameObject("TableLaughsRoot");
            root.AddComponent<BoardInputBridge>();
            root.AddComponent<PlayerManager>();
            root.AddComponent<PromptManager>();
            root.AddComponent<RoundManager>();
            root.AddComponent<VoteManager>();
            root.AddComponent<ScoreManager>();
            root.AddComponent<UIManager>();
            root.AddComponent<SoundHooks>();
            root.AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene(ScenePath);
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"Table Laughs playable scene created at {ScenePath}");
        }

        public static void RunSmokeTest()
        {
            var root = new GameObject("TableLaughs Smoke Test");
            try
            {
                var players = root.AddComponent<PlayerManager>();
                var prompts = root.AddComponent<PromptManager>();
                var rounds = root.AddComponent<RoundManager>();
                var votes = root.AddComponent<VoteManager>();
                var scores = root.AddComponent<ScoreManager>();

                prompts.LoadPromptPack();
                players.JoinSeat(0);
                players.JoinSeat(2);
                players.JoinSeat(5);
                players.ResetScores();

                rounds.BeginRound(1, players.Players, prompts);
                if (rounds.CurrentMatchups.Count == 0 || rounds.CurrentAnswerSlots.Count == 0)
                {
                    throw new InvalidOperationException("Round 1 did not create matchups and answer slots.");
                }

                foreach (var slot in rounds.CurrentAnswerSlots)
                {
                    rounds.SubmitAnswer(slot, $"Smoke answer {slot.Player.Id}");
                }

                foreach (var matchup in rounds.CurrentMatchups)
                {
                    votes.BeginHeadToHeadVote(matchup, players.Players);
                    foreach (var player in players.Players)
                    {
                        if (!matchup.HasSubmitter(player.Id))
                        {
                            votes.CastHeadToHeadVote(player, 0);
                        }
                    }
                }

                scores.ApplyStandardRoundScores(rounds.CurrentMatchups, rounds.PointsPerVote, players.Players.Count);

                rounds.BeginRound(3, players.Players, prompts);
                foreach (var answer in rounds.FinalAnswers)
                {
                    rounds.SubmitFinalAnswer(answer, $"Final smoke {answer.Player.Id}");
                }

                votes.BeginFinalVote(rounds.FinalAnswers, players.Players);
                for (var voterIndex = 0; voterIndex < players.Players.Count; voterIndex++)
                {
                    var voter = players.Players[voterIndex];
                    var answerIndex = (voterIndex + 1) % rounds.FinalAnswers.Count;
                    votes.CastFinalVote(voter, answerIndex);
                }

                scores.ApplyFinalRoundScores(rounds.FinalAnswers, rounds.PointsPerVote, players.Players.Count);
                if (players.GetLeaderboard()[0].Score <= 0)
                {
                    throw new InvalidOperationException("Smoke test completed without awarding points.");
                }

                Debug.Log("Table Laughs smoke test passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void RunUiSmokeTest()
        {
            var root = new GameObject("TableLaughs UI Smoke Test");
            try
            {
                var bridge = root.AddComponent<BoardInputBridge>();
                var players = root.AddComponent<PlayerManager>();
                var prompts = root.AddComponent<PromptManager>();
                var rounds = root.AddComponent<RoundManager>();
                var votes = root.AddComponent<VoteManager>();
                var scores = root.AddComponent<ScoreManager>();
                var ui = root.AddComponent<UIManager>();

                prompts.LoadPromptPack();
                ui.Initialize(bridge, null);
                ui.ShowTitleScreen(() => { });

                players.JoinSeat(0);
                players.JoinSeat(2);
                players.JoinSeat(5);
                ui.ShowJoinScreen(players.Players, players.JoinSeat, (player, name) => players.RenamePlayer(player.Id, name),
                    player => players.CyclePlayerColor(player.Id), player => players.LeaveSeat(player.Id), () => { });

                rounds.BeginRound(1, players.Players, prompts);
                ui.ShowPromptEntry(1, players.Players, rounds.CurrentAnswerSlots, 75f,
                    rounds.SubmitAnswer, prompts.GetRandomFallbackAnswer);
                foreach (var slot in rounds.CurrentAnswerSlots)
                {
                    rounds.SubmitAnswer(slot, $"UI answer {slot.Player.Id}");
                }

                var matchup = rounds.CurrentMatchups[0];
                votes.BeginHeadToHeadVote(matchup, players.Players);
                ui.ShowHeadToHeadVoting(1, matchup, players.Players, 25f, votes.CastHeadToHeadVote);
                ui.ShowHeadToHeadResult(matchup);

                rounds.BeginRound(3, players.Players, prompts);
                ui.ShowFinalPromptEntry(players.Players, rounds.FinalAnswers, 75f,
                    rounds.SubmitFinalAnswer, prompts.GetRandomFallbackAnswer);
                foreach (var answer in rounds.FinalAnswers)
                {
                    rounds.SubmitFinalAnswer(answer, $"Final UI {answer.Player.Id}");
                }

                votes.BeginFinalVote(rounds.FinalAnswers, players.Players);
                ui.ShowFinalVoting(players.Players, rounds.FinalAnswers, 40f, votes.CastFinalVote);
                ui.ShowFinalResult(rounds.FinalAnswers);

                var summary = scores.ApplyFinalRoundScores(rounds.FinalAnswers, rounds.PointsPerVote,
                    players.Players.Count);
                ui.ShowScoreboard(3, players.GetLeaderboard(), summary, () => { });
                ui.ShowWinnerScreen(players.GetLeaderboard(), () => { });

                Debug.Log("Table Laughs UI smoke test passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                DestroyIfPresent("TableLaughsCanvas");
                DestroyIfPresent("EventSystem");
            }
        }

        public static void BuildAndroidApk()
        {
            if (!File.Exists(ScenePath))
            {
                CreatePlayableScene();
            }

            ConfigurePlayerSettings();
            var outputPath = Environment.GetEnvironmentVariable("TABLE_LAUGHS_APK");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = "/tmp/table-laughs/TableLaughs.apk";
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android build failed with {report.summary.totalErrors} error(s): {report.summary.result}");
            }

            Debug.Log($"Table Laughs Android APK built at {outputPath}");
        }

        private static void SetBuildScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Table Laughs";
            PlayerSettings.productName = "Table Laughs";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.tablelaughs.board");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tablelaughs.board");
        }

        private static void DestroyIfPresent(string objectName)
        {
            var gameObject = GameObject.Find(objectName);
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
