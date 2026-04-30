# Table Laughs

Table Laughs is an original Board-native local party game prototype for 3 to 8 players. Players join from seats around the Board, write family-friendly joke answers, vote from their own edge panels, and play through three fast rounds.

## Run in Unity Editor

1. Open this folder in Unity `6000.4.2f1`.
2. Open `Assets/Scenes/TableLaughs.unity`.
3. Press Play.
4. Click `Start`, then tap at least three edge seats to join.

If the scene is missing, run `Table Laughs > Create Playable Scene` from Unity's menu. The editor setup utility creates the scene and puts it in Build Settings.

## Game Loop

1. Title screen.
2. Player join screen with 8 edge seats. Each joined player can edit their name and cycle color.
3. Rounds 1 and 2 give every player the same prompt. Players handwrite one answer on blank paper in their own seat-facing panels.
4. The center of the Board reveals the shared prompt and every answer. Players vote from their seat panels, except for their own answer.
5. Votes award 100 points in Round 1 and 200 points in Round 2, with a sweep bonus if an answer gets every possible vote.
6. Round 3 gives everyone the same final prompt. Players vote among all final answers, except their own.
7. The winner screen shows the leaderboard, confetti, and a `Play Again` button.

## Board Notes

- The UI is built around a landscape tabletop layout: edge panels face each side and the center is the shared reveal area.
- `BoardUIInputModule` is added at runtime through `BoardInputBridge`; in Editor, an `InputSystemUIInputModule` is added as a mouse fallback while keeping Board's required new Input System project setting.
- No phones, accounts, networking, backend, or physical pieces are required for v1.
- TODO comments mark where future Board SDK integrations can map `BoardInput.GetActiveContacts(...)` to richer seat routing, read Board profile names from `BoardSession.players`, or persist custom prompt packs with `BoardSaveGameManager`.

## Android Build

The editor utility includes `TableLaughs.EditorTools.TableLaughsProjectSetup.BuildAndroidApk`, which writes `/tmp/table-laughs/TableLaughs.apk` by default.

Board SDK builds require a Piece Set Model even though this v1 does not use physical pieces. Configure it in `Project Settings > Board > Input Settings` before building for hardware.

## Prompt Packs

Prompts live in:

`Assets/Resources/Prompts/table_laughs_prompts.json`

Add more entries to `prompts`, `finalPrompts`, or `randomAnswers`. Keep prompt copy original and safe by default.

## Main Scripts

- `GameManager`: high-level phase and coroutine flow.
- `PlayerManager`: local players, seats, names, colors, leaderboard.
- `PromptManager`: JSON prompt loading and random fallback answers.
- `RoundManager`: shared prompt creation, final prompt setup, answer submission.
- `VoteManager`: vote eligibility and vote capture.
- `ScoreManager`: points and sweep bonuses.
- `UIManager`: runtime tabletop UI, handwriting paper, voting panels, reveal screens.
- `HandwritingPaperInput`: per-player finger-drawn answer capture and replay.
