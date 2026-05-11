# Table Laughs Agent Guide

## Scope

These instructions apply to the whole repository.

## Active App

- The active Board Web SDK game lives in `table-laughs/web`.
- The Android wrapper that should be installed on Board devices lives in `table-laughs/android`.
- Treat `board-websdk/` as the vendor SDK bundle and reference harness unless the user explicitly asks to modify it.

## Device Verification

- When an Android APK is built for this project and an attached device is available, always install the built APK on the device before reporting completion.
- Build the web app first with `cd table-laughs/web && npm run build`.
- Build the Android wrapper with `cd table-laughs/android && ./gradlew assembleDebug`.
- Install the debug APK with `adb install -r table-laughs/android/app/build/outputs/apk/debug/app-debug.apk`.
- If the default Java runtime is missing, use Android Studio's bundled JDK by setting `JAVA_HOME=/Applications/Android Studio.app/Contents/jbr/Contents/Home` for the Gradle command.
