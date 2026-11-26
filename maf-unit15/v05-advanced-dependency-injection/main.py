"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

class ScopedEvidence:
    def __init__(self, inner):
        self.inner = inner
        self.scope_id = uuid.uuid4().hex
        self.calls = 0

    def read(self, service):
        self.calls += 1
        return self.inner.read(service)


def demonstrate(ctx):
    # The composition root selects the implementation outside the use case.
    scope_a = ScopedEvidence(ctx.evidence())
    scope_b = ScopedEvidence(ctx.evidence())
    with stores(ctx.state) as (producer, consumer):
        first = lab.assess(producer, scope_a, ctx.operation_id + '-a', ctx.service)
        retried = lab.assess(producer, scope_a, ctx.operation_id + '-a', ctx.service)
        second = lab.assess(producer, scope_b, ctx.operation_id + '-b', ctx.service)
        state = inspect(producer, consumer)
    assert scope_a.calls == scope_b.calls == 1
    assert first == retried and scope_a.scope_id != scope_b.scope_id
    return {'scopeA': {'id': scope_a.scope_id, 'adapterCalls': scope_a.calls},
            'scopeB': {'id': scope_b.scope_id, 'adapterCalls': scope_b.calls},
            'retryReusedAcceptedResult': first == retried,
            'assessments': [first, second], 'state': state}


if __name__ == '__main__':
    run_demo('v05', demonstrate)
