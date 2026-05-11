# Table Laughs

This directory is the standalone Board Web SDK version of Table Laughs.

## Layout

- `web/` - Vite + TypeScript game code.
- `android/` - Android WebView wrapper that installs as `com.tablelaughs.board`.
- `vendor/` - Board Web SDK npm tarball used by the web app.

## Build And Install

```bash
cd web
npm install
npm run build

cd ../android
./gradlew assembleDebug
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

The generic SDK sample remains in `../board-websdk/`; Table Laughs should be built from this directory.
