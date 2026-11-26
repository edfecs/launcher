# Fortnite Booster PRO 2.0

A Windows 11 x64 Fortnite desktop launcher with a custom dark library interface.

## Features
- Fortnite-only library and a dedicated game details page.
- Install button opens the official Epic Games Store Fortnite page.
- Play button launches the configured Fortnite executable, or lets the user locate it.
- Local player profiles: create, rename, switch and delete profiles. Each profile stores its own Fortnite path, Epic display nickname, launch options and performance preset.
- Profiles are stored in `%LOCALAPPDATA%\\FortniteBoosterPRO\\profiles.json`.
- Live Fortnite Battle Royale news artwork is refreshed from the public Fortnite-API news endpoint and cached locally. The API is community-run and is not affiliated with Epic Games.
- Safe Windows Game Mode / Game DVR settings in the BOOST panel. No service disabling or overclocking.

## Account note
Booster profiles are local app profiles, not Epic Games authentication sessions. The app never asks for or stores Epic passwords.

## Build
GitHub Actions builds a self-contained Windows x64 executable on every push to `main`.
Open **Actions**, choose the latest successful **Build Windows EXE** run, and download the `FortniteBoosterPRO-Windows-x64` artifact.
