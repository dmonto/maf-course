"""Run the built projects and check their process contracts using only the standard library."""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    options = parser.parse_args()
    if not options.dotnet:
        parser.error("Install the .NET 10 SDK or supply --dotnet PATH.")
    sdk = str(Path(options.dotnet).resolve())
    root = Path(__file__).resolve().parents[1]
    manifest = json.loads((root / "manifest.json").read_text())
    entries = {e["id"]: e for e in manifest["examples"] }
    environment = dict(os.environ, NETWORK_DELAY_MS="5", SERVICE_DELAY_MS="10")
    records = []

    def run(identifier, arguments, code, stage=None, extra_env=None):
        entry = entries[identifier]
        project = root / entry["project"]
        assembly = project.parent / "bin" / "Debug" / "net10.0" / (project.stem + ".dll")
        if not assembly.exists():
            raise AssertionError("Build the solution before verification: " + str(project))
        with tempfile.TemporaryDirectory() as cwd:
            process = subprocess.run([sdk, str(assembly), *arguments], cwd=cwd,
                                     env=dict(environment, **(extra_env or {})),
                                     text=True, capture_output=True, timeout=45)
        assert process.returncode == code, (identifier, arguments, process.returncode, process.stderr)
        result = json.loads(process.stdout) if process.stdout.strip() else None
        events = [json.loads(line) for line in process.stderr.splitlines() if line.strip()]
        if stage:
            assert result and result["Stage"] == stage, (identifier, result)
        records.append({"project": identifier, "arguments": arguments,
                        "exitCode": code, "stage": result["Stage"] if result else None, "passed": True})
        return result, events

    expected = {
        "v01": "information-ready", "v02": "delegation-complete", "v03": "needs-review",
        "v04": "communication-verified", "v05": "role-boundaries-verified",
        "v06": "isolation-verified", "v07": "escalation-proposal", "v08": "scalability-observed",
        "v09": "patterns-compared", "v10": "checks-passed", "ex01": "scoped-delegation-verified",
        "ex02": "replay-handling-verified",
    }
    for identifier, stage in expected.items():
        result, events = run(identifier, ["--demo"], 2 if identifier == "v03" else 0, stage)
        data = result["Data"]
        if identifier == "v03":
            assert data["Brief"] is None and all(e["Event"] != "synthesis-start" for e in events)
        elif identifier == "v04":
            assert len(data["Exchanges"]) == 2
            assert all(e["RejectedForgedBinding"] == "assignment-mismatch" for e in data["Exchanges"])
        elif identifier == "v06":
            outcomes = data["Outcomes"]
            assert len({o["RunId"] for o in outcomes}) == 2
            assert all(r["Task"]["RunId"] == o["RunId"] for o in outcomes for r in o["Reports"])
        elif identifier == "v07":
            assert len(data["Reports"]) == 2 and len(data["Brief"]["EvidenceIds"]) == 2
            assert {r["Task"]["Evidence"]["Subject"]: r["Task"]["Evidence"]["Status"]
                    for r in data["Reports"]} == {"vpn": "healthy", "payroll": "degraded"}
        elif identifier == "v10":
            assert len(data["Checks"]) == 5 and all(c["Passed"] for c in data["Checks"])
        elif identifier == "ex01":
            assert len(data["Delegations"]) == 3
            assert "FAKE-TOKEN-NOT-A-CREDENTIAL" not in json.dumps(result)
            assert "EMP-TEST-001" not in json.dumps(result)
        elif identifier == "ex02":
            assert data["SynthesisInvocations"] == 1
            assert data["CurrentOutcome"]["Brief"] is None and data["AcceptedReportNotOverwritten"]

    for scenario, stage in [("vpn", "information-ready"), ("payroll", "escalation-proposal")]:
        run("v07", [scenario, "--demo"], 0, stage)
    for arguments in [["combined", "--demo", "--fail-network"], ["payroll", "--demo", "--conflict"]]:
        result, events = run("v07", arguments, 2, "needs-review")
        assert result["Data"]["Brief"] is None
        assert all(e["Event"] != "synthesis-start" for e in events)
    run("v07", ["combined", "--demo", "--serial"], 0, "escalation-proposal")
    result, events = run("v08", ["--demo", "--cases", "3"], 0, "scalability-observed")
    assert result["Data"]["Cases"] == 3 and result["Data"]["SpecialistInvocations"] == 6
    assert result["Data"]["SynthesisInvocations"] == 3
    result, _ = run("ex01", ["--demo", "--deny-identity"], 2, "needs-review")
    assert result["Data"]["IdentityEvidenceReads"] == 0 and result["Data"]["Delegations"] == []

    for arguments in [["unknown", "--demo"], ["--demo", "--live"],
                      ["payroll", "--demo", "--fail-network"], ["--demo", "--unknown"]]:
        result, events = run("v07", arguments, 1)
        assert result is None and all(e["Event"] != "specialist-start" for e in events)
    result, events = run("v07", ["--live"], 1, extra_env={
        "AZURE_OPENAI_ENDPOINT": "https://YOUR-RESOURCE.openai.azure.com/",
        "AZURE_OPENAI_DEPLOYMENT_NAME": "YOUR-ASSIGNED-DEPLOYMENT"})
    assert result is None and all(e["Event"] != "specialist-start" for e in events)
    assert list(root.glob("*/.env")) == [] and (root / ".env").exists()

    core = root / "Verification/bin/Debug/net10.0/Unit08.Checks.dll"
    process = subprocess.run([sdk, str(core)], text=True, capture_output=True, timeout=30)
    assert process.returncode == 0, process.stderr
    internal = json.loads(process.stdout)
    assert internal["Stage"] == "checks-passed"
    report = {"verifiedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "sdk": subprocess.check_output([sdk, "--version"], text=True).strip(),
              "liveAzureTested": False, "processCases": records, "internalChecks": internal}
    (root / "Verification/last-run.json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"PASS: {len(records)} process cases and {internal['Checks']} contract/concurrency checks.")


if __name__ == "__main__":
    main()
