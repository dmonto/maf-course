# v07: Pipeline integration

A portability gate accepts the host and rejects a deliberately wrong release.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

The real fixture HTTP host passes `candidate.json`. The same host fails `rejected-release.json` when the expected release is deliberately changed. The enclosing example returns success only when both outcomes are correct.

`Pipelines/unit14-container.yml` contains the portability workflow. It is not installed automatically. A green portability report remains fixture evidence and must not be labeled as Azure identity, behavioral evaluation or production security evidence.
