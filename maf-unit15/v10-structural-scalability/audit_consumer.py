"""Independent audit-owned store; it never queries the review database."""
import sqlite3
from shared.contracts import validate_event


class AuditConsumer:
    def __init__(self, path):
        self.db = sqlite3.connect(path)
        self.db.row_factory = sqlite3.Row
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS inbox(event_id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS audit_records(
                event_id TEXT PRIMARY KEY, operation_id TEXT NOT NULL, service TEXT NOT NULL);
        ''')

    def consume(self, payload):
        event = validate_event(payload)
        with self.db:
            inserted = self.db.execute('INSERT OR IGNORE INTO inbox VALUES (?,?)',
                                       (event['eventId'], payload)).rowcount
            if not inserted:
                previous = self.db.execute('SELECT payload FROM inbox WHERE event_id=?',
                                           (event['eventId'],)).fetchone()['payload']
                if previous != payload:
                    raise ValueError('Conflicting audit event identity')
                return 'duplicate'
            self.db.execute('INSERT INTO audit_records VALUES (?,?,?)',
                            (event['eventId'], event['correlationId'], event['data']['service']))
        return 'applied'

    def inspect(self):
        return {'inboxCount': self.db.execute('SELECT count(*) FROM inbox').fetchone()[0],
                'auditRecords': self.db.execute('SELECT count(*) FROM audit_records').fetchone()[0]}

    def close(self):
        self.db.close()


def deliver(consumer, payload, available=True):
    if not available:
        raise ConnectionError('Synthetic audit subscription unavailable')
    return consumer.consume(payload)
