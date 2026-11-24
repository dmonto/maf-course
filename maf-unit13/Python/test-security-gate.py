"""Reject mutations of real .NET-generated evidence; no Azure connection is needed."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def check(report):
    gate = Path(__file__).with_name('security-gate.py')
    def run(path):
        return subprocess.run([sys.executable, str(gate), str(path)], capture_output=True, text=True)
    baseline = run(report)
    if baseline.returncode != 0:
        raise RuntimeError('The baseline evidence must pass before mutation testing.')
    with tempfile.TemporaryDirectory(prefix='unit13-gate-') as temp:
        for name in ['missing-case', 'unauthorized-read', 'sensitive-field', 'policy-change', 'missing-audit']:
            copy = Path(temp) / name
            shutil.copytree(report, copy)
            result = json.loads((copy / 'results.json').read_text(encoding='utf-8-sig'))
            events = [json.loads(line) for line in (copy / 'audit.jsonl').read_text(encoding='utf-8-sig').splitlines()]
            if name == 'missing-case':
                result['outcomes'] = [r for r in result['outcomes'] if r['caseId'] != 'tenant-denied']
            elif name == 'unauthorized-read':
                case = next(r for r in result['outcomes'] if r['caseId'] == 'payroll-denied')
                event = next(e for e in events if e['requestId'] == case['requestId'])
                event['decision'], event['reason'] = 'allow', 'allowed'
            elif name == 'sensitive-field':
                events[0]['prompt'] = 'owner@example.invalid'
            elif name == 'policy-change':
                events[0]['policyVersion'] = 'unreviewed'
            else:
                events = []
            (copy / 'results.json').write_text(json.dumps(result), encoding='utf-8')
            (copy / 'audit.jsonl').write_text('\n'.join(json.dumps(e) for e in events), encoding='utf-8')
            rejection = run(copy)
            if rejection.returncode != 1:
                raise RuntimeError('Gate did not reject ' + name)
            print('PASS reject ' + name)
    print('PYTHON GATE MUTATION CHECKS PASSED')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('Usage: python test-security-gate.py <valid-artifact-directory>')
    check(Path(sys.argv[1]))
