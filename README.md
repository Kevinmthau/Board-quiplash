# Table Laughs

Table Laughs is now implemented as a Board Web SDK local party game for 3 to 6 players. Players join from seats around the table, draw answers on per-seat paper panels, vote from their own seat controls, and play through three rounds with sweep bonuses and a final winner screen.

## Project Layout

The active Board version now lives under `table-laughs/`:

- `table-laughs/web/` contains the Vite + TypeScript game.
- `table-laughs/android/` contains the Android wrapper that installs as its own app: `com.tablelaughs.board`.
- `table-laughs/vendor/` contains the Board Web SDK npm tarball used by this game.

`board-websdk/` remains as the vendor SDK bundle and generic sample harness. It should not be used as the app identity for Table Laughs.

## Run The Board Web Version

```bash
cd table-laughs/web
npm install
npm run dev
```

The browser preview is playable for layout and game-loop work. Board APIs are guarded behind `Board.isOnDevice`, so the same bundle can run off-device and inside the Board WebView.

## Build For Board Or Harness

```bash
cd table-laughs/web
npm run build
cd ../android
./gradlew assembleDebug
```

The Android wrapper expects the built output at `table-laughs/web/dist` and copies it into the APK assets. Java 17+ is required for the Android build.

## Game Loop

1. Title screen.
2. Player join screen with 6 local seats and optional Board profile import on device.
3. Rounds 1 and 2 use a shared prompt. Each player draws one answer on their own paper panel.
4. The center table reveals all answers. Players vote from their own seat panel, except for their own answer.
5. Votes award 100 points in Round 1 and 200 points in Round 2, with a sweep bonus if an answer gets every possible vote.
6. Round 3 uses a final prompt worth 300 points per vote.
7. The winner screen shows the leaderboard and saves a compact snapshot through `Board.save` on device.

## Board Web SDK Notes

- `Board.isOnDevice` gates all bridge-backed API calls.
- `Board.session` is used for Board profile names and guest add/remove where available.
- `Board.input.subscribe` reads touch and piece contacts for live device status.
- `Board.pause.setContext` configures restart, save-and-quit, and audio sliders.
- `Board.save` creates or updates a small game snapshot on save-and-quit and at the winner screen.

## Prompt Packs

Prompts are served by the web app from:

`board-websdk/example/public/prompts/table_laughs_prompts.json`

The original Unity prompt pack remains at `Assets/Resources/Prompts/table_laughs_prompts.json` for reference.

## Legacy Unity Project

The Unity implementation is still present in `Assets/`, `Packages/`, and `ProjectSettings/`, but the active version is the Board Web SDK app under `board-websdk/example`.
