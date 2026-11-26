"""Durable synthetic approval and repair-request state machine."""
import json
import sqlite3
from datetime import datetime, timezone


class TransitionRejected(ValueError):
    pass


class RepairProcess:
    def __init__(self, path):
        self.db = sqlite3.connect(path)
        self.db.row_factory = sqlite3.Row
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS processes(
                id TEXT PRIMARY KEY, service TEXT NOT NULL, state TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS commands(
                id TEXT PRIMARY KEY, payload TEXT NOT NULL, result TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS history(
                sequence INTEGER PRIMARY KEY AUTOINCREMENT, process_id TEXT NOT NULL,
                state TEXT NOT NULL, actor TEXT NOT NULL, occurred_at TEXT NOT NULL);
        ''')

    def open(self, process_id, service):
        if service not in ('vpn', 'email'):
            raise TransitionRejected('Unsupported synthetic service')
        existing = self.db.execute('SELECT * FROM processes WHERE id=?', (process_id,)).fetchone()
        if existing:
            if existing['service'] != service:
                raise TransitionRejected('Process identity conflict')
            return
        with self.db:
            self.db.execute('INSERT INTO processes VALUES (?,?,?)', (process_id, service, 'AwaitingApproval'))
            self._history(process_id, 'AwaitingApproval', 'application-fixture')

    def _history(self, process_id, state, actor):
        self.db.execute('INSERT INTO history(process_id,state,actor,occurred_at) VALUES (?,?,?,?)',
                        (process_id, state, actor, datetime.now(timezone.utc).isoformat()))

    def apply(self, process_id, command_id, action, actor):
        payload = json.dumps({'processId': process_id, 'action': action, 'actor': actor}, sort_keys=True)
        stored = self.db.execute('SELECT * FROM commands WHERE id=?', (command_id,)).fetchone()
        if stored:
            if stored['payload'] != payload:
                raise TransitionRejected('Command ID reused with different content')
            result = json.loads(stored['result'])
            return {**result, 'outcome': 'duplicate'}
        transitions = {
            ('approve', 'AwaitingApproval'): ('Authorized', 'reviewer-demo'),
            ('request-repair', 'Authorized'): ('RepairRequested', 'operator-demo'),
            ('verify-success', 'RepairRequested'): ('Verified', 'verifier-demo'),
            ('verify-failure', 'RepairRequested'): ('NeedsManualRecovery', 'verifier-demo')
        }
        with self.db:
            process = self.db.execute('SELECT * FROM processes WHERE id=?', (process_id,)).fetchone()
            if not process or (action, process['state']) not in transitions:
                raise TransitionRejected('The transition is not allowed from the current state')
            target, trusted_fixture_actor = transitions[(action, process['state'])]
            # These actors are fixed laboratory identities, not an authentication system.
            if actor != trusted_fixture_actor:
                raise TransitionRejected('The synthetic actor does not own this transition')
            changed = self.db.execute('UPDATE processes SET state=? WHERE id=? AND state=?',
                                      (target, process_id, process['state'])).rowcount
            if changed != 1:
                raise TransitionRejected('Concurrent transition; reload the process')
            result = {'outcome': 'applied', 'from': process['state'], 'to': target}
            self._history(process_id, target, actor)
            self.db.execute('INSERT INTO commands VALUES (?,?,?)',
                            (command_id, payload, json.dumps(result, sort_keys=True)))
        return result

    def inspect(self, process_id):
        row = self.db.execute('SELECT * FROM processes WHERE id=?', (process_id,)).fetchone()
        history = [dict(r) for r in self.db.execute(
            'SELECT state,actor,occurred_at FROM history WHERE process_id=? ORDER BY sequence', (process_id,))]
        return {'process': dict(row), 'history': history}

    def close(self):
        self.db.close()
