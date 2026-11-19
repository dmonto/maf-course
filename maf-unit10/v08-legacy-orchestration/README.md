# Orchestration with legacy systems

Translate a legacy health contract into the same typed service evidence and message format.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v08 -Demo
```

Expected: VPN01 with health D maps to service vpn with status degraded; SQL still returns 2 open tickets.

The significant implementation is in `Shared/LegacyAdapter.cs and Program.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.

## Legacy endpoint contract

The assigned API root exposes `GET legacy/status/{service}` with a bearer credential attached by the host. Its JSON fields are `serviceCode`, `tenant`, `health` and `observedUtc`. Service mappings are VPN01/vpn, ERP01/erp and PAY01/payroll. Health mappings are H/healthy, D/degraded and U/unavailable. The timestamp uses the round-trip ISO 8601 format with an explicit UTC offset. Unsupported codes/flags, tenant mismatches and stale observations fail validation.

The packaged loopback fixture exposes this route. The maintenance-freeze endpoint and rule belong exclusively to Exercise 2.
