# Validation record

The pack was validated with the .NET 10 SDK on Linux.

* All 12 projects compiled: eight examples, two exercise solutions, the shared library and the independent checks project. Build completed with zero warnings and zero errors.
* Locked restore passed for the complete solution.
* All eight example applications and both solutions ran successfully in synthetic demo mode, including execution from a project subdirectory with the common root `.env`.
* Independent checks covered cost arithmetic, null usage, model eligibility, task facts, invented recovery times, endpoint boundaries and dotenv process-variable precedence.
* The versioning demonstration correctly rejected its deliberately regressed candidate.
* Cost outputs reproduced USD 0.001 per call, USD 10 per 10,000 calls, USD 30 per 10,000 three-call cases and USD 7.20 for Exercise 2.
* Four .NET applications and the Python MAF reference were checked against a local compatible-API fixture. Six requests verified the `/v1/chat/completions` route, configured full model ID, output-token option and reported token usage. The local preparation check also read `/openai/loadedmodels`.

These protocol checks used simulated model responses. They did not invoke real Azure deployments or execute model weights on Foundry Local hardware. Windows-specific installation, Azure role assignments and model/device compatibility must be verified in the instructor environment. The PowerShell preparation scripts follow the linked current CLI documentation.

Run `Scripts/Test-Offline.ps1` after restore to repeat the independent checks and synthetic demonstrations. Run the normal examples with the assigned deployments and local model for live validation.
