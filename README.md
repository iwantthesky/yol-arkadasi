# Roadmates
Roadmates is a 2–4 player co-op motorcycle game built with Unity 6000.3.18f1 for Windows. Every rider controls part of the same motorcycle: throttle and brake, clutch and gears, steering, or balance. With fewer than four players, roles are combined automatically.

The current v0.6.0 build includes a 900-meter mountain route and a fully rideable open map. The bike can leave the asphalt, turn around, climb gentle mountain slopes, collide with trees and steep terrain, and continue exploring after reaching the finish.

## Play

Download the Windows ZIP from the shared Google Drive link in the latest project announcement. Extract the whole ZIP, then double-click `Roadmates.exe`. Keep the EXE and its accompanying folders together.

For co-op play, all players must run the same version. One player creates a 2–4 player room and shares the displayed IP address. Other players join from the same LAN or a configured shared VPN. The game uses TCP port `47777`.

## Controls

| Role | Controls |
|---|---|
| Throttle / brake | W / S |
| Steering | A / D |
| Disengage clutch | Hold Space |
| Shift down / up | Q / E while the clutch is disengaged |
| Ignition | I while in neutral or holding the clutch |
| Shift weight left / right | Left / Right Arrow |
| Recover at the last checkpoint | R, host only |
| Guide / camera / audio | H / C / M |
| Pause / menu | Escape |

To launch: hold Space, press E for first gear, hold W, then release Space gradually. The motorcycle has no automatic balance assist; the balance rider must react to steering, terrain and wind.

## Develop

Open the `Unity` folder in Unity `6000.3.18f1`. The main scene is `Assets/Scenes/YolArkadasi.unity`. Use **Roadmates → 02 Test and Build Windows** to run checks, regenerate the scene and create the Windows build.

- `main`: verified playable releases.
- `dev`: integration branch for the next version.
- New work: `feature/short-topic` or `fix/short-topic`.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the team workflow and [docs/ASSETS.md](docs/ASSETS.md) for asset provenance.

## Current limitations

- LAN/direct-IP prototype; no Steam invites, relay, matchmaking or host migration.
- Keyboard controls only.
- Skill-focused motorcycle physics rather than a full tire and suspension simulator.
- The original Blender microtextures are not baked into the Unity model.
