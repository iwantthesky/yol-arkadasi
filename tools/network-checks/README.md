# Standalone LAN transport checks

Requires the .NET 10 SDK. Run from the motor-coop folder:

```powershell
dotnet run --project tools/network-checks/NetworkChecks.csproj
```

The project links the actual Unity `CoopSession.cs` and `CoopRoleChecks.cs` source files through relative paths. It exercises real TCP sockets on `127.0.0.1:49773`; this port must be available. A successful run prints `SUCCESS: 25 transport checks plus static role checks` and exits with code 0. No external NuGet packages are used.

The harness covers 2-, 3-, and 4-player joining, role allocation and aggregation, pulse consumption, host-only reset, input clamping, stale input, snapshots, room capacity rejection, disconnects, role reassignment, rejoining during play, host restart, duplicate sequence rejection, oversized packets, flooding, and normal joining after an abusive peer is disconnected.

**Scope:** `UnityEngine`, `MotorInput`, and `BikeSnapshot` are standalone stubs. `JsonUtility` is emulated with `System.Text.Json`. The sockets and transport implementation are genuine, but this harness does not validate Unity's serializer, Unity lifecycle scheduling, presentation, physics, or real Unity player instances. The separate Unity build and multi-instance smoke checks provide that evidence.

The recorded run is in `../../qa/network-harness.txt`.
