import json
import re
import sys
from datetime import datetime
from pathlib import Path

EXPECTED = {
    "vpn-allowed": "degraded",
    "payroll-denied": "forbidden",
    "sensitive-allowed": "healthy",
    "classification-denied": "forbidden",
    "tenant-denied": "forbidden",
    "spoofed-role": "forbidden",
    "unknown-denied": "forbidden",
}
AUDIT_FIELDS = {
    "timestamp", "requestId", "subject", "tenant", "operation",
    "resource", "decision", "reason", "policyVersion",
}
EMAIL = re.compile(r"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(root):
    audit_text = (root / "audit.jsonl").read_text(encoding="utf-8-sig")
    result_text = (root / "results.json").read_text(encoding="utf-8-sig")
    require(not EMAIL.search(audit_text + result_text), "Contact detail found in artifacts")
    events = [json.loads(line) for line in audit_text.splitlines() if line.strip()]
    result = json.loads(result_text)
    require(result["policyVersion"] == "security-v1", "Unexpected result policy")
    require(result["mode"] in {"fixture", "azure"}, "Unexpected execution mode")
    cases = result["outcomes"]
    ids = [case["caseId"] for case in cases]
    require(len(ids) == len(set(ids)), "Duplicate case IDs")
    require(set(EXPECTED).issubset(ids), "Required negative or positive case missing")
    request_ids = [case["requestId"] for case in cases]
    require(len(request_ids) == len(set(request_ids)), "Shared request between cases")
    require(bool(events), "Audit trail missing")

    for event in events:
        require(set(event) == AUDIT_FIELDS, "Unexpected audit fields or missing metadata")
        require(event["requestId"] in request_ids, "Uncorrelated audit event")
        require(event["operation"] == "service.read", "Unexpected operation")
        require(event["policyVersion"] == "security-v1", "Unexpected audit policy")
        require(event["decision"] in {"allow", "deny"}, "Invalid security decision")
        require((event["decision"] == "allow") == (event["reason"] == "allowed"),
                "Decision and reason disagree")
        datetime.fromisoformat(event["timestamp"].replace("Z", "+00:00"))

    for case in cases:
        expected = EXPECTED.get(case["caseId"], case["expectedStatus"])
        require(case["expectedStatus"] == expected, "Case expectation changed")
        require(case["passed"] is True and case["failure"] is None,
                "Application security or response contract failed")
        require(case["observedStatus"] == expected, "Observed outcome disagrees")
        matching = [event for event in events if event["requestId"] == case["requestId"]]
        require(bool(matching), "Decision absent for a request")
        required_decision = "deny" if expected == "forbidden" else "allow"
        require(all(event["decision"] == required_decision for event in matching),
                "Unauthorized read or inconsistent decision")
    print(f"SECURITY GATE PASS: {len(cases)} cases, {len(events)} audit events, "
          f"mode={result['mode']}")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2, "Usage: python security-gate.py <artifact-directory>")
        validate(Path(sys.argv[1]))
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"SECURITY GATE FAIL: {error}", file=sys.stderr)
        sys.exit(1)
