# Optional Python MAF connection reference

This is the same connection task as the .NET probe, using `Agent` and `OpenAIChatCompletionClient` from the Python MAF SDK. The root `.env` is loaded automatically. It supports `azure` and `foundry-local` and uses an explicitly configured OpenAI-compatible SDK client for each path.

Run from the ZIP root:

```powershell
py -3.13 -m venv .venv-python
if ($LASTEXITCODE -ne 0) { throw 'Python environment creation failed.' }
.\.venv-python\Scripts\python.exe -m pip install -r requirements-python.txt
if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed.' }

# Start and prepare Foundry Local first, using the shared .env settings.
& ./Scripts/Prepare-FoundryLocal.ps1
$env:MAF_MODEL_PROVIDER = 'foundry-local'
.\.venv-python\Scripts\python.exe ./v05-endpoint-management/Python/provider_probe.py
if ($LASTEXITCODE -ne 0) { throw 'Python local probe failed.' }

& ./Scripts/Login-Azure.ps1
$env:MAF_MODEL_PROVIDER = 'azure'
.\.venv-python\Scripts\python.exe ./v05-endpoint-management/Python/provider_probe.py
if ($LASTEXITCODE -ne 0) { throw 'Python Azure probe failed.' }
```

The local key `notneeded` is an SDK compatibility placeholder. Azure uses an Azure CLI bearer-token provider. Secrets are not printed. `elapsedMs` measures the Python agent run; compare timing boundaries before comparing it with another implementation. There is no Python `--demo` switch. Capture a resolved Python dependency list with `pip freeze` for a reproducible instructor environment.
