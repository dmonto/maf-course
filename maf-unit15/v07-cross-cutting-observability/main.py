"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

class ObservableEvidence:
    def __init__(self, inner, context):
        self.inner, self.context = inner, context

    def read(self, service):
        with self.context.timed('evidence-read'):
            result = self.inner.read(service)
        self.context.emit('evidence-observed', service=result['service'],
                          evidenceMode=result['mode'], requestId=result.get('requestId'))
        return result


def demonstrate(ctx):
    with stores(ctx.state) as (producer, consumer):
        with ctx.timed('assessment'):
            result = lab.assess(producer, ObservableEvidence(ctx.evidence(), ctx),
                                ctx.operation_id, ctx.service)
        event = json.loads(payload_for(producer, ctx.operation_id))
        ctx.emit('outbox-pending', eventId=event['eventId'])
        with ctx.timed('delivery'):
            deliveries = lab.dispatch(producer, consumer)
        state = inspect(producer, consumer)
        ctx.emit('business-state-inspected', inboxCount=state['inboxCount'],
                 pendingEvents=state['pendingEvents'], reviewItems=len(state['reviewItems']))
    return {'assessment': result, 'operationId': ctx.operation_id,
            'correlationId': event['correlationId'], 'eventId': event['eventId'],
            'deliveries': deliveries, 'state': state,
            'diagnosticsFile': str(ctx.state / 'events.jsonl'),
            'telemetryKind': 'structured local diagnostics; no OTLP exporter configured'}


if __name__ == '__main__':
    run_demo('v07', demonstrate)
