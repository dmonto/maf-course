"""Runnable example for the corresponding Unit 15 syllabus section."""
from pathlib import Path
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from shared import architecture_lab as lab
from shared.runtime import run_demo, stores, inspect, payload_for, save_json

from repair_process import RepairProcess, TransitionRejected


def demonstrate(ctx):
    # Every transition below concerns synthetic state; no repair API exists here.
    with stores(ctx.state) as (producer, consumer):
        assessment = lab.assess(producer, ctx.evidence(), ctx.operation_id, ctx.service)
        lab.dispatch(producer, consumer)
        handoff = inspect(producer, consumer)
    if assessment['decision'] == 'no_action':
        return {'assessment': assessment, 'handoff': handoff,
                'process': 'not-required', 'externalRepairExecuted': False}
    workflow = RepairProcess(ctx.state / 'repair-process.db')
    try:
        successful = ctx.operation_id + '-success'
        workflow.open(successful, ctx.service)
        try:
            workflow.apply(successful, 'premature-request', 'request-repair', 'operator-demo')
        except TransitionRejected:
            premature = 'blocked-before-approval'
        else:
            raise AssertionError('Repair request bypassed approval')
        workflow.apply(successful, 'approval-1', 'approve', 'reviewer-demo')
        workflow.apply(successful, 'request-1', 'request-repair', 'operator-demo')
        retry = workflow.apply(successful, 'request-1', 'request-repair', 'operator-demo')
        workflow.apply(successful, 'verify-1', 'verify-success', 'verifier-demo')
        failed = ctx.operation_id + '-manual-recovery'
        workflow.open(failed, ctx.service)
        workflow.apply(failed, 'approval-2', 'approve', 'reviewer-demo')
        workflow.apply(failed, 'request-2', 'request-repair', 'operator-demo')
        workflow.apply(failed, 'verify-2', 'verify-failure', 'verifier-demo')
        return {'assessment': assessment, 'handoff': handoff,
                'prematureRequest': premature, 'duplicateCommand': retry,
                'successful': workflow.inspect(successful),
                'manualRecovery': workflow.inspect(failed),
                'externalRepairExecuted': False}
    finally:
        workflow.close()


if __name__ == '__main__':
    run_demo('v09', demonstrate)
