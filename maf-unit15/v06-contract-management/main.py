"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

def demonstrate(ctx):
    from shared.contracts import validate_event
    with stores(ctx.state) as (producer, consumer):
        lab.assess(producer, ctx.evidence(), ctx.operation_id, ctx.service)
        payload = payload_for(producer, ctx.operation_id)
        event = validate_event(payload)
        lab.dispatch(producer, consumer)
        outcomes = []
        for label, mutate in [
            ('unsupported-schema', lambda e: e.update(schemaVersion=2)),
            ('correlation-mismatch', lambda e: e.update(correlationId='another-operation')),
            ('unsupported-mode', lambda e: e['data'].update(evidence_mode='unknown'))
        ]:
            changed = json.loads(payload)
            mutate(changed)
            try:
                validate_event(json.dumps(changed))
            except ValueError:
                outcomes.append({'case': label, 'outcome': 'rejected-before-write'})
            else:
                raise AssertionError('An invalid event was accepted')
        changed = json.loads(payload)
        changed['data']['evidence_mode'] = 'azure' if changed['data']['evidence_mode'] == 'fixture' else 'fixture'
        validate_event(json.dumps(changed))
        try:
            lab.consume(consumer, json.dumps(changed, sort_keys=True))
        except ValueError:
            outcomes.append({'case': 'same-id-different-payload', 'outcome': 'rejected'})
        else:
            raise AssertionError('Conflicting event identity was accepted')
        final = inspect(producer, consumer)
        assert final['inboxCount'] == 1
    save_json(ctx.state / 'accepted-contract.json', event)
    return {'accepted': event, 'checks': outcomes, 'state': final}


if __name__ == '__main__':
    run_demo('v06', demonstrate)
