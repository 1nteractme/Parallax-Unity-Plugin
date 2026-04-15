# 1nteractme Parallax Plugin

Embedded UPM plugin for Unity UI and 2D background layers.

## Included Components

- `BackgroundParallaxController`
- `ParallaxImageBackgroundController`
- `Parallax Studio` editor window (`Tools/Parallax Studio`)

## Features

- Layer-based parallax with per-layer speed.
- Auto-scroll mode (works even when camera is static).
- Infinite cycle support on X, Y, or both axes.
- UI mode support (`RectTransform` + `Image`).
- Background sprite sets with random/manual selection.
- Seamless tiling for `Image` layers.
- Editor window for creating/configuring controllers without manual list editing.

## Folder

- `Packages/com.1nteractme.parallax/Runtime`

## Install In Another Project

1. Copy folder `Packages/com.1nteractme.parallax` into target project `Packages`.
2. Open Unity and wait for package import.
3. Components appear in Add Component menu:
- `BackgroundParallaxController`
- `ParallaxImageBackgroundController`
4. Open setup window:
- `Tools -> Parallax Studio`

## Export As .unitypackage

1. In Unity Project view select `Packages/com.1nteractme.parallax`.
2. `Assets -> Export Package...`
3. Keep dependencies if needed and export.
