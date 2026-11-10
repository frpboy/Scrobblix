"""Synthetic CI-only additions to the existing known-LF profile preservation fixture."""
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
if os.environ.get('GITHUB_ACTIONS') != 'true' or os.environ.get('RUNNER_OS') != 'Windows':
    raise SystemExit('Hosted Windows CI only')
subprocess.run([sys.executable, 'scripts/test-legacy-profile.py', 'prepare'], check=True)
root = Path(os.environ['LOCALAPPDATA']) / 'Scrobblix'
with sqlite3.connect(root / 'scrobblix.db') as db:
    # Two ambiguous outcomes must remain held through install, upgrade, launch and uninstall.
    for n in range(2):
        db.execute("INSERT INTO provider_actions(action_id,play_id,account_id,provider,action_type,state,idempotency_key,attempt_count,due_utc,created_utc,updated_utc) VALUES(?, 'synthetic-play','synthetic-account','lastfm',0,8,?,1,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z')", (f'unknown-{n}', f'unknown-key-{n}'))
    tables = ['schema_migrations','plays','accounts','provider_actions','loved_tracks']
    snapshot = {t: db.execute(f'SELECT * FROM {t} ORDER BY 1').fetchall() for t in tables}
    (Path(os.environ['RUNNER_TEMP']) / 'scrobblix-legacy-snapshot.json').write_text(json.dumps(snapshot, sort_keys=True))
print('Prepared disconnected synthetic profile including two held Unknown outcomes')
