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
        after_assessment = inspect(producer, consumer)
        try:
            lab.dispatch(producer, consumer, crash_after_delivery=True)
        except RuntimeError as error:
            if str(error) != 'Injected crash after consumer commit':
                raise
            ctx.emit('expected-delivery-failure', failureType=type(error).__name__)
        else:
            raise AssertionError('The injected failure did not occur')
        after_failure = inspect(producer, consumer)
    # Reopen both stores to demonstrate recovery across connection lifetimes.
    with stores(ctx.state) as (producer, consumer):
        recovery = lab.dispatch(producer, consumer)
        final = inspect(producer, consumer)
        assert recovery[0]['outcome'] == 'duplicate'
        assert after_failure['pendingEvents'] == 1 and final['pendingEvents'] == 0
        assert len(final['reviewItems']) == (1 if assessment['decision'] == 'review_required' else 0)
    return {'afterAssessment': after_assessment, 'afterFailure': after_failure,
            'recovery': recovery, 'final': final}


if __name__ == '__main__':
    run_demo('v04', demonstrate)
