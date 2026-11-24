# Validation record

The pack was validated with the .NET 10 SDK on Linux.

* Locked restore passed for the complete solution.
* All eight example applications and both solutions ran successfully in synthetic demo mode, including execution from a project subdirectory with the common root `.env`.
* Independent checks covered cost arithmetic, null usage, model eligibility, task facts, invented recovery times, endpoint boundaries and dotenv process-variable precedence.
* The versioning demonstration correctly rejected its deliberately regressed candidate.
* Four .NET applications and the Python MAF reference were checked against a local compatible-API fixture. Six requests verified the `/v1/chat/completions` route, configured full model ID, output-token option and reported token usage. The local preparation check also read `/openai/loadedmodels`.

Run `Scripts/Test-Offline.ps1` after restore to repeat the independent checks and synthetic demonstrations. Run the normal examples with the assigned deployments and local model for live validation.
