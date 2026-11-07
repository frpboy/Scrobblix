"""Isolated hosted-CI synthetic fixture only. Never opens an existing owner profile."""
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import sys

if os.environ.get('GITHUB_ACTIONS') != 'true' or os.environ.get('RUNNER_OS') != 'Windows':
    raise SystemExit('This synthetic fixture runs only on Windows GitHub Actions')
root = Path(os.environ['LOCALAPPDATA']) / 'Scrobblix'
marker = root / 'synthetic-ci-fixture.marker'
expected = 'scrobblix-known-lf-legacy-fixture-v1'
snapshot = Path(os.environ['RUNNER_TEMP']) / 'scrobblix-legacy-snapshot.json'
tables = ['schema_migrations', 'plays', 'accounts', 'provider_actions', 'loved_tracks']

def rows(connection):
    return {t: connection.execute(f'SELECT * FROM {t} ORDER BY 1').fetchall() for t in tables}

if sys.argv[1] == 'prepare':
    if root.exists():
        raise SystemExit('Refusing to seed over any existing profile')
    root.mkdir()
    marker.write_text(expected)
    with sqlite3.connect(root / 'scrobblix.db') as db:
        migrations = sorted(Path('src/Scrobblix.Infrastructure/Storage/Migrations').glob('*.sql'))
        if len(migrations) != 6:
            raise SystemExit('Unexpected migration fixture shape')
        for file in migrations:
            sql = file.read_bytes().decode('utf-8').replace('\r\n', '\n')
            db.executescript(sql)
            db.execute('INSERT INTO schema_migrations VALUES(?,?,?)',
                       (file.stem, hashlib.sha256(sql.encode()).hexdigest(), '2026-01-01T00:00:00Z'))
        db.execute("INSERT INTO plays(play_id,started_utc,artist,title,confirmed_played_ms,local_status,created_utc,updated_utc) VALUES('synthetic-play','2026-01-01T00:00:00Z','Synthetic Artist','Synthetic Track',42000,0,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z')")
        # Disconnected synthetic account, no stored credential, never a real provider call.
        db.execute("INSERT INTO accounts(account_id,provider,remote_username,credential_resource,is_connected,created_utc,updated_utc) VALUES('synthetic-account','lastfm','synthetic-user','synthetic-resource',0,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z')")
        db.execute("INSERT INTO provider_actions(action_id,play_id,account_id,provider,action_type,state,idempotency_key,attempt_count,due_utc,created_utc,updated_utc) VALUES('synthetic-action','synthetic-play','synthetic-account','lastfm',0,0,'synthetic-key',2,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z')")
        db.execute("INSERT INTO loved_tracks(track_key,source_play_id,artist,title,is_loved,current_action_id,created_utc,updated_utc) VALUES('synthetic-track','synthetic-play','Synthetic Artist','Synthetic Track',1,'synthetic-action','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z')")
        snapshot.write_text(json.dumps(rows(db), sort_keys=True))
    print('Prepared exact known-LF migration profile with synthetic history/account/queue/loved rows')
elif sys.argv[1] == 'verify':
    if marker.read_text() != expected:
        raise SystemExit('Synthetic marker missing')
    with sqlite3.connect(f'file:{(root / "scrobblix.db").as_posix()}?mode=ro', uri=True) as db:
        if json.dumps(rows(db), sort_keys=True) != snapshot.read_text():
            raise SystemExit('Legacy migration/history/account/queue/loved rows changed')
    print('PASS exact delivered ZIP opens known-LF legacy profile with every fixture row/checksum unchanged')
else:
    raise SystemExit('Expected prepare or verify')
