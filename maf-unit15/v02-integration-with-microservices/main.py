"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

class RequestEvidence:
    def __init__(self, inner):
        self.inner = inner
        self.response = None

    def read(self, service):
        self.response = self.inner.read(service)
        return self.response


def demonstrate(ctx):
    port = RequestEvidence(ctx.evidence())
    with stores(ctx.state) as (producer, consumer):
        result = lab.assess(producer, port, ctx.operation_id, ctx.service)
        state = inspect(producer, consumer)
    return {'adapter': type(port.inner).__name__,
            'transport': 'HTTP' if ctx.mode == 'http' else 'in-process fixture',
            'evidenceResponse': port.response,
            'assessment': result, 'state': state}


if __name__ == '__main__':
    run_demo('v02', demonstrate)
