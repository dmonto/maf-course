# Integration with Slack

Prepare the exact notification and optionally deliver it after host review.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v03 -Demo
```

Expected: Default: prepared preview. With local -Send -Approve: sent with a fixture receipt. Repeating the operation ID: already-sent.

The significant implementation is in `Shared/NotificationFlow.cs and Shared/Connectors.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.

```powershell
./Scripts/Run-Example.ps1 -Example v03 -Demo -Send -Approve -Operation local-send-001
./Scripts/Run-Example.ps1 -Example v03 -Demo -Send -Approve -Operation local-send-001
```

The second run must not produce another POST. For a configured training Slack send, follow the live setup in the root README and omit -Approve.
