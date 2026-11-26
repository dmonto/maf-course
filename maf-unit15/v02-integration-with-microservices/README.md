# v02 — Integration with microservices

Complete C# entry point: `Program.cs`. Shared implementation: `../Shared`. Run from the Unit 15 root after building `Maf.Unit15.slnx`:

```powershell
.\Scripts\Run-Example.ps1 -Example v02 -Mode fixture -NoBuild
```

Direct .NET alternative:

```powershell
dotnet run --project .\v02-integration-with-microservices\Maf.Unit15.V02.csproj --no-build -- --mode fixture
```

Expected default result: The selected adapter, evidence provenance and accepted assessment. HTTP mode exposes the actual host response.

The root `.env` is shared by all projects. Every default invocation uses a fresh state directory. The final `RESULT` line includes `stateDirectory`; inspect `result.json`, `events.jsonl` and the retained SQLite files there. Use `-Mode http` with the bundled host, or `-Mode azure` with the assigned Azure identity and deployment. Version v08 always uses its explicit healthy fixture.

All repair and audit scenarios are synthetic. See the root README for transport, state reuse and verification details.
