# Development and verification log

Last updated: 2026-09-17

## v0.6.0

- Translated all player-facing menus, HUD labels, instructions, network messages, course signs and crash guidance into English.
- Renamed the Windows player to `Roadmates.exe`.
- Added English repository documentation and contribution instructions.
- The Windows playtest package is distributed separately through Google Drive.

## Earlier verified milestones

- v0.5.1: speed-sensitive steering; Unity behavior checks passed 16/16; road and free-roam tests reported zero runtime errors.
- v0.5.0: restored mountain/tree materials and added physical mountain/tree collisions; checks passed 15/15.
- v0.4.0: free-roam world heading, off-road driving and player-controlled balance; network checks passed 25/25.
- v0.3.0: original Antigravity world, 900-meter route, manual drivetrain and 2–4 player host-authoritative TCP/LAN co-op.

## Known limits

- Remote play requires an accessible network or shared VPN.
- No relay, matchmaking, Steam integration or automatic host migration.
- Skill-focused physics without detailed tire slip or WheelCollider suspension.
- Keyboard input only in this version.
