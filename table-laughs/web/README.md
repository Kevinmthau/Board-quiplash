# Table Laughs - Board Web

This is the Board Web SDK version of Table Laughs. It replaces the Unity runtime with a Vite + TypeScript web game that runs in a normal browser for layout work and inside the Table Laughs Android WebView wrapper for Board SDK APIs.

## Run Locally

```bash
npm install
npm run dev
```

The browser preview runs with `Board.isOnDevice === false`, so Board bridge calls stay guarded while the full local game loop remains playable.

## Build

```bash
npm run build
```

`vite.config.ts` keeps `base: "./"` so `dist/` can be loaded by the Android wrapper from app assets.

## Android APK

From `table-laughs/`:

```bash
cd web
npm run build
cd ../android
./gradlew assembleDebug
```

The Android wrapper copies `web/dist` into the APK. Java 17+ is required for this step.

## Board SDK Usage

- `Board.isOnDevice` gates all bridge-backed calls.
- `Board.session` imports Board profile names and can add/remove guests from the join screen.
- `Board.input.subscribe` tracks touch and piece contact counts in the device bar.
- `Board.pause.setContext` configures restart, save-and-quit, and audio sliders.
- `Board.save` writes a compact game snapshot on winner and save-and-quit.

Prompts are served from `public/prompts/table_laughs_prompts.json`, copied from the original Unity prompt pack.
