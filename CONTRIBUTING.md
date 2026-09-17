# Contributing to Roadmates
## Setup

1. Clone the private repository.
2. Open the `Unity` folder with Unity Hub.
3. Use Unity `6000.3.18f1`.
4. Open `Assets/Scenes/YolArkadasi.unity`.

Unity recreates `Library`, `Logs`, `UserSettings` and build folders locally. Do not commit them.

## Branch workflow

- `main` contains verified playable releases.
- `dev` is the integration branch for the next release.
- Create `feature/short-topic` or `fix/short-topic` branches from `dev`.
- Open pull requests into `dev`. Release pull requests go from `dev` to `main`.
- Do not push directly to `main`.

## Before requesting review

- Commit Unity assets together with their `.meta` files.
- Coordinate scene edits before starting large changes that may conflict.
- For gameplay changes, test launch, falling/recovery and free-roam driving.
- For networking changes, run `dotnet run --project tools/network-checks/NetworkChecks.csproj --configuration Release`.
- Before a release, run **Roadmates → 02 Test and Build Windows** in Unity.
- Record every third-party asset and its license in `docs/ASSETS.md`.

Builds, videos, logs, backups and Unity-generated folders stay outside Git. Playtest builds are distributed through the shared Google Drive package.
