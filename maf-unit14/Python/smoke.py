import argparse
import concurrent.futures
import json
import math
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

EXPECTED = {
    "vpn": ("degraded", "Synthetic evidence: VPN connectivity is degraded."),
    "email": ("healthy", "Synthetic evidence: email is healthy."),
}


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", default="http://127.0.0.1:8080")
    parser.add_argument("--release", required=True)
    parser.add_argument("--mode", choices=("fixture", "azure"), default="fixture")
    parser.add_argument("--requests", type=int, default=4)
    parser.add_argument("--parallel", type=int, default=1)
    parser.add_argument("--timeout", type=int, default=120)
    parser.add_argument("--output", default="smoke-report.json")
    args = parser.parse_args()
    url = urllib.parse.urlsplit(args.base)
    local = url.hostname in {"127.0.0.1", "localhost", "::1"}
    if url.scheme not in {"http", "https"} or not url.hostname or url.username or url.password:
        parser.error("Use an HTTP(S) base URL without embedded credentials.")
    if url.path not in {"", "/"} or url.query or url.fragment:
        parser.error("Use an origin URL without a path, query or fragment.")
    if url.scheme == "http" and not local:
        parser.error("Remote targets require HTTPS.")
    if not 1 <= args.requests <= 20 or not 1 <= args.parallel <= 4:
        parser.error("Use 1..20 requests and 1..4 parallel workers.")
    if not 1 <= args.timeout <= 300:
        parser.error("Use a request timeout of 1..300 seconds.")

    base = args.base.rstrip("/")
    token = os.getenv("SMOKE_BEARER_TOKEN")
    rows = []
    report = {"release": args.release, "mode": args.mode, "passed": False,
              "requestTimeoutSeconds": args.timeout, "checks": {}, "results": rows}
    output = Path(args.output)
    opener = urllib.request.build_opener(NoRedirect())

    def request(path, payload=None, timeout=None):
        headers = {"Accept": "application/json"}
        if token:
            headers["Authorization"] = f"Bearer {token}"
        data = None
        if payload is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(base + path, data=data, headers=headers)
        started = time.perf_counter()
        try:
            with opener.open(req, timeout=timeout or args.timeout) as response:
                # A redirect to a login page is not a successful health response.
                if response.geturl() != base + path:
                    raise ValueError("Unexpected redirect")
                body = json.load(response)
                return response.status, body, (time.perf_counter() - started) * 1000
        except urllib.error.HTTPError as error:
            try:
                body = json.loads(error.read(65536))
            except (ValueError, UnicodeError):
                body = {}
            return error.code, body, (time.perf_counter() - started) * 1000

    def diagnostic_fields(body):
        # Copy only known operational fields, never arbitrary response bodies or messages.
        if not isinstance(body, dict):
            return {}
        result = {}
        allowed = {
            "error": {"request_cancelled", "agent_unavailable", "busy", "unknown_service"},
            "phase": {"identity", "model", "contract", "fixture"},
            "reason": {"client_disconnected", "application_stopping", "request_deadline"},
        }
        for key, values in allowed.items():
            if isinstance(body.get(key), str) and body[key] in values:
                result[key] = body[key]
        if isinstance(body.get("requestId"), str) and re.fullmatch(r"[a-f0-9]{32}", body["requestId"]):
            result["requestId"] = body["requestId"]
        if isinstance(body.get("failureType"), str) and re.fullmatch(r"[A-Za-z0-9.]{1,128}", body["failureType"]):
            result["failureType"] = body["failureType"]
        if type(body.get("dependencyStatus")) is int and 100 <= body["dependencyStatus"] <= 599:
            result["dependencyStatus"] = body["dependencyStatus"]
        if type(body.get("timeoutSeconds")) is int and 1 <= body["timeoutSeconds"] <= 180:
            result["timeoutSeconds"] = body["timeoutSeconds"]
        if type(body.get("elapsedMs")) in (int, float) and 0 <= body["elapsedMs"] <= 1_000_000:
            result["serverElapsedMs"] = body["elapsedMs"]
        return result

    def validate_metadata(body):
        if not isinstance(body, dict):
            raise ValueError("Response must be a JSON object")
        for key, expected in (("release", args.release), ("mode", args.mode),
                              ("promptVersion", "status-v1")):
            if body.get(key) != expected:
                raise ValueError(f"Unexpected {key}")
        if not isinstance(body.get("instance"), str) or not body["instance"]:
            raise ValueError("Missing instance identity")

    def check(index):
        service = "vpn" if index % 2 == 0 else "email"
        started = time.perf_counter()
        row = {"service": service, "httpStatus": None, "passed": False}
        try:
            code, body, elapsed = request("/api/status", {"service": service})
            row.update({"httpStatus": code, "elapsedMs": round(elapsed, 2)})
            if code == 200:
                validate_metadata(body)
                expected_status, expected_summary = EXPECTED[service]
                row["instance"] = body["instance"]
                row["passed"] = (body.get("service") == service
                                 and body.get("status") == expected_status
                                 and body.get("summary") == expected_summary
                                 and isinstance(body.get("requestId"), str)
                                 and bool(body["requestId"]))
            else:
                row.update(diagnostic_fields(body))
        except Exception as error:
            row.update({"failureType": type(error).__name__,
                        "elapsedMs": round((time.perf_counter() - started) * 1000, 2)})
        return row

    stage, path = "readiness", "/health/ready"
    try:
        deadline = time.monotonic() + 45
        while True:
            try:
                code, body, _ = request(path, timeout=min(args.timeout, 10))
                report["checks"][stage] = {"httpStatus": code, "passed": False}
                if code == 200:
                    validate_metadata(body)
                    if body.get("status") != "ok":
                        raise ValueError("Unexpected readiness status")
                    report["checks"][stage]["passed"] = True
                    break
                if code in {401, 403}:
                    raise ValueError("Probe identity cannot access the target")
            except (urllib.error.URLError, TimeoutError):
                pass
            if time.monotonic() >= deadline:
                raise ValueError("Readiness deadline exceeded")
            time.sleep(0.5)

        stage, path = "liveness", "/health/live"
        code, body, _ = request(path, timeout=min(args.timeout, 10))
        report["checks"][stage] = {"httpStatus": code, "passed": False}
        if code != 200 or body.get("status") != "ok":
            raise ValueError("Liveness check failed")
        validate_metadata(body)
        report["checks"][stage]["passed"] = True
        stage, path = "functional", "/api/status"
        with concurrent.futures.ThreadPoolExecutor(max_workers=args.parallel) as pool:
            # Stop after the first failed batch; do not issue four long retries after a timeout.
            for start in range(0, args.requests, args.parallel):
                batch = list(pool.map(check, range(start, min(start + args.parallel, args.requests))))
                rows.extend(batch)
                if not all(row["passed"] for row in batch):
                    break
        if not all(row["passed"] for row in rows):
            raise ValueError("Functional request failed")
        samples = sorted(row["elapsedMs"] for row in rows)
        report["p95Ms"] = samples[math.ceil(0.95 * len(samples)) - 1]
        report["instancesObserved"] = sorted({row["instance"] for row in rows})
        report["passed"] = True
    except Exception as error:
        # Persist failure class only; do not copy tokens or response bodies into reports.
        report["failureType"] = type(error).__name__
        report["failureStage"] = stage
        report["failurePath"] = path
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    label = "PASS" if report["passed"] else f"FAIL [{stage}]"
    print(f"{label}: {output}")
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
