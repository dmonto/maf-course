"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

from audit_consumer import AuditConsumer, deliver


def demonstrate(ctx):
    with stores(ctx.state) as (producer, consumer):
        lab.assess(producer, ctx.evidence(), ctx.operation_id, ctx.service)
        payload = payload_for(producer, ctx.operation_id)
        review_delivery = lab.dispatch(producer, consumer)
        review_state = inspect(producer, consumer)
        # Simulate an independent subscription being temporarily unavailable.
        audit = AuditConsumer(ctx.state / 'audit.db')
        try:
            before = audit.inspect()
            try:
                deliver(audit, payload, available=False)
            except ConnectionError as error:
                audit_failure = str(error)
            else:
                raise AssertionError('The audit outage was not observed')
            deliveries = [deliver(audit, payload) for _ in range(3)]
            after = audit.inspect()
        finally:
            audit.close()
    assert deliveries == ['applied', 'duplicate', 'duplicate']
    assert after['auditRecords'] == 1 and after['inboxCount'] == 1
    return {'reviewDelivery': review_delivery, 'reviewState': review_state,
            'duringAuditOutage': {'auditState': before, 'failure': audit_failure},
            'auditReplay': deliveries, 'auditState': after,
            'deliveryModel': 'two independent local subscriber adapters, no distributed broker'}


if __name__ == '__main__':
    run_demo('v10', demonstrate)
