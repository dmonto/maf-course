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
    # The evidence port owns retrieval; the application owns the decision.
    with stores(ctx.state) as (producer, consumer):
        result = lab.assess(producer, ctx.evidence(), ctx.operation_id, ctx.service)
        state = inspect(producer, consumer)
    ctx.emit('assessment-committed', decision=result['decision'])
    return {'ownership': {'evidence': 'EvidencePort',
                          'assessment': 'Assessment application',
                          'review': 'Independent review consumer'},
            'assessment': result, 'state': state,
            'businessActionsExecuted': []}


if __name__ == '__main__':
    run_demo('v01', demonstrate)
