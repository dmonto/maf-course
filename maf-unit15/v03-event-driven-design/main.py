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
    with stores(ctx.state) as (producer, consumer):
        assessment = lab.assess(producer, ctx.evidence(), ctx.operation_id, ctx.service)
        payload = payload_for(producer, ctx.operation_id)
        committed = inspect(producer, consumer)
        first_delivery = lab.dispatch(producer, consumer)
        replay = lab.consume(consumer, payload)
        final = inspect(producer, consumer)
    event = json.loads(payload)
    save_json(ctx.state / 'AssessmentRecorded.v1.json', event)
    assert replay == 'duplicate'
    return {'assessment': assessment, 'event': event,
            'afterCommit': committed, 'delivery': first_delivery,
            'replay': replay, 'final': final}


if __name__ == '__main__':
    run_demo('v03', demonstrate)
