"""Run process-level acceptance checks without any model or Azure dependency."""
import json
import os
import sqlite3
import subprocess
import sys
import uuid
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'manifest.json').read_text())
projects = {v['id']: v['project'] for v in manifest['examples'] + manifest['exercises']}
prefix = 'verify-' + uuid.uuid4().hex[:8]
dotnet = os.environ.get('DOTNET_COMMAND', 'dotnet')
results = []

def run(id, *args, code=0, stage=None):
    process = subprocess.run([dotnet, 'run', '--no-build', '--project', str(root / projects[id]),
        '--', *args, '--demo'], text=True, capture_output=True, timeout=30, cwd=root)
    text = process.stdout + process.stderr
    assert process.returncode == code, (id, args, process.returncode, text)
    entries = [json.loads(line) for line in process.stdout.splitlines() if line.startswith('{')]
    outputs = [json.loads(line[7:]) for line in process.stdout.splitlines() if line.startswith('RESULT ')]
    if stage:
        assert len(outputs) == 1 and outputs[0]['stage'] == stage, text
    if code:
        assert not outputs, text
    results.append({'project': id, 'arguments': list(args), 'exitCode': code,
        'stage': outputs[0]['stage'] if outputs else None})
    return entries, outputs, text

def completed(entries):
    return [e['executor'] for e in entries if e.get('type') == 'ExecutorCompletedEvent']

for id in ['v01','v02','v05','v07','v09']:
    run(id, 'run', 'vpn', '--state', prefix, stage='information-ready')
run('v02', 'run', 'payroll', '--decision', 'yes', '--state', prefix, stage='escalation-approved')
e, _, t = run('v03', 'run', 'payroll', '--fail', '--state', prefix, code=1)
assert [x['attempt'] for x in e if x.get('kind') == 'status-attempt'] == [1,2,3]
assert not any(x.get('kind') == 'review' for x in e)
assert 'draft' not in completed(e)
run('v04', 'run', 'payroll', '--decision', 'no', '--state', prefix, stage='escalation-rejected')
e, _, _ = run('v05', 'run', 'unknown', '--state', prefix, code=1)
assert not any(x.get('kind') == 'status-attempt' for x in e)
e, _, _ = run('v06', 'run', 'payroll', '--decision', 'yes', '--state', prefix, stage='escalation-approved')
assert any(x.get('kind') == 'order-verified' for x in e)
a, _, _ = run('v07', 'run', 'vpn', '--state', prefix, '--status-delay-ms', '300', '--guide-delay-ms', '500', stage='information-ready')
b, _, _ = run('v07', 'run', 'vpn', '--state', prefix, '--serial', '--status-delay-ms', '300', '--guide-delay-ms', '500', stage='information-ready')
parallel = next(x['elapsedMs'] for x in a if x.get('kind') == 'evidence-timing')
serial = next(x['elapsedMs'] for x in b if x.get('kind') == 'evidence-timing')
# Timing is recorded as an observation, not a flaky machine-speed assertion.
run('v08', 'pause', 'payroll', '--state', prefix)
bookmark = root / 'data/v08' / prefix / 'pending.json'
assert bookmark.exists()
e, _, _ = run('v08', 'resume', '--decision', 'yes', '--state', prefix, stage='escalation-approved')
assert not bookmark.exists()
assert not {'intake','evidence','draft'}.intersection(completed(e))
run('ex01', 'run', 'vpn', '--state', prefix, stage='information-ready')
run('ex01', 'run', 'payroll', '--decision', 'yes', '--state', prefix, stage='escalation-approved')
e, _, _ = run('ex01', 'run', 'payroll', '--stale', '--state', prefix, code=1)
assert 'evidence-quality-gate' not in completed(e) and 'draft' not in completed(e)
assert not any(x.get('kind') == 'review' for x in e)
reject = prefix + '-reject'
run('ex02', 'run', 'payroll', '--decision', 'no', '--state', reject, stage='escalation-rejected')
with sqlite3.connect(root / 'data/ex02' / reject / 'escalations.db') as db:
    assert db.execute('SELECT COUNT(*) FROM escalations').fetchone()[0] == 0
recovery = prefix + '-recovery'
e, _, _ = run('ex02', 'run', 'payroll', '--decision', 'yes', '--fail-after-save', '--state', recovery, code=1)
receipt = next(x for x in e if x.get('kind') == 'operation-receipt')
assert receipt['count'] == 1
with sqlite3.connect(root / 'data/ex02' / recovery / 'escalations.db') as db:
    saved = db.execute('SELECT escalation_id FROM escalations').fetchone()[0]
e, out, _ = run('ex02', 'resume', '--state', recovery, stage='escalation-created')
assert out[0]['escalationId'] == saved and out[0]['reused']
assert not any(x.get('kind') == 'review' for x in e)
_, out, _ = run('ex02', 'run', 'payroll', '--decision', 'yes', '--state', recovery, stage='escalation-created')
assert out[0]['escalationId'] == saved and out[0]['reused']
with sqlite3.connect(root / 'data/ex02' / recovery / 'escalations.db') as db:
    assert db.execute('SELECT COUNT(*) FROM escalations').fetchone()[0] == 1
report = {'checks': len(results), 'result': 'passed', 'parallelEvidenceMs': parallel,
    'serialEvidenceMs': serial, 'liveInference': 'not-run', 'cases': results}
report_path = root / 'Verification/last-run.json'
report_path.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({k: v for k, v in report.items() if k != 'cases'}))
