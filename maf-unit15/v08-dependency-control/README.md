# v08 — Dependency control

Complete C# entry point: `Program.cs`. Shared implementation: `../Shared`. Run from the Unit 15 root after building `Maf.Unit15.slnx`:

```powershell
.\Scripts\Run-Example.ps1 -Example v08 -Mode fixture -NoBuild
```

Direct .NET alternative:

```powershell
dotnet run --project .\v08-dependency-control\Maf.Unit15.V08.csproj --no-build -- --mode fixture
```

Expected default result: No provider references in the compiled core. The explicit healthy email fixture creates no review item and calls no model.

The root `.env` is shared by all projects. Every default invocation uses a fresh state directory. The final `RESULT` line includes `stateDirectory`; inspect `result.json`, `events.jsonl` and the retained SQLite files there. Use `-Mode http` with the bundled host, or `-Mode azure` with the assigned Azure identity and deployment. Version v08 always uses its explicit healthy fixture.

All repair and audit scenarios are synthetic. See the root README for transport, state reuse and verification details.
