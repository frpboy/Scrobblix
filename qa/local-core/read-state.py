"""Bounded readonly assertions over a fresh synthetic profile. No row dumps."""
import argparse
import json
import pathlib
import sqlite3
import sys

ARTIST = 'Scrobblix synthetic local-core artist'
TITLE = 'Scrobblix synthetic local-core 90s'

def state(path):
    uri = pathlib.Path(path).resolve().as_uri() + '?mode=ro'
    with sqlite3.connect(uri, uri=True, timeout=1.0) as db:
        db.execute('PRAGMA query_only=ON')
        db.execute('BEGIN')
        # Counts only, never account identifiers/credentials/config/history contents.
        accounts = db.execute('SELECT count(*) FROM accounts').fetchone()[0]
        actions = db.execute('SELECT count(*) FROM provider_actions').fetchone()[0]
        plays = db.execute('SELECT count(*) FROM plays').fetchone()[0]
        obs = db.execute('SELECT count(*) FROM playback_observations').fetchone()[0]
        unexpected = db.execute('''SELECT count(*) FROM playback_observations
            WHERE artist IS NOT ? OR title IS NOT ? OR source_name != 'gsmtc' ''',
            (ARTIST, TITLE)).fetchone()[0]
        matching = db.execute('''SELECT duration_ms, confirmed_played_ms,
            eligibility_threshold_ms, eligible_utc IS NOT NULL
            FROM plays WHERE artist=? AND title=? ORDER BY started_utc DESC LIMIT 2''',
            (ARTIST, TITLE)).fetchall()
        latest = db.execute('''SELECT playback_state FROM playback_observations
            WHERE artist=? AND title=? AND source_name='gsmtc'
            ORDER BY observed_utc DESC, sequence_number DESC LIMIT 1''',
            (ARTIST, TITLE)).fetchone()
        paused = db.execute('''SELECT count(*) FROM playback_observations
            WHERE artist=? AND title=? AND source_name='gsmtc' AND playback_state=2''',
            (ARTIST, TITLE)).fetchone()[0]
        playing = db.execute('''SELECT count(*) FROM playback_observations
            WHERE artist=? AND title=? AND source_name='gsmtc' AND playback_state=1''',
            (ARTIST, TITLE)).fetchone()[0]
        # Count predicate covers unknown media, not arbitrary string exports.
        out = {'accounts':accounts, 'actions':actions, 'plays':plays,
               'observations':obs, 'unexpected_observations':unexpected,
               'synthetic_play_rows':len(matching), 'playing_observations':playing,
               'paused_observations':paused, 'latest_state':latest[0] if latest else None}
        if len(matching) == 1:
            out.update(zip(('duration_ms','confirmed_ms','threshold_ms','eligible'),matching[0]))
        return out

def check(s, stage, previous=None):
    assert s['accounts'] == 0 and s['actions'] == 0, 'unexpected_account_or_action'
    assert s['unexpected_observations'] == 0, 'unexpected_media_source'
    if stage == 'baseline':
        assert s['plays'] == 0 and s['observations'] == 0, 'nonempty_baseline'
        return
    assert s['plays'] == 1 and s['synthetic_play_rows'] == 1, 'canonical_play_count'
    assert s['duration_ms'] == 90000 and s['threshold_ms'] == 45000, 'duration_threshold'
    assert s['playing_observations'] >= 2, 'playing_not_observed'
    assert s['eligible'] == 1 and 45000 <= s['confirmed_ms'] <= 80000, 'eligibility_not_reached'
    if stage in ('paused', 'stable'):
        assert s['paused_observations'] >= 1 and s['latest_state'] == 2, 'pause_not_observed'
    if stage == 'stable':
        assert previous is not None, 'previous_state_required'
        # Once persisted Paused is observed, confirmed time must stop exactly.
        assert s['confirmed_ms'] == previous['confirmed_ms'], 'paused_time_advanced'
        assert s['paused_observations'] > previous['paused_observations'], 'no_fresh_pause_observation'

if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--db', required=True)
    p.add_argument('--stage', choices=('baseline','eligible','paused','stable'), required=True)
    p.add_argument('--previous')
    p.add_argument('--output', required=True)
    a = p.parse_args()
    try:
        s = state(a.db)
        previous = json.loads(pathlib.Path(a.previous).read_text()) if a.previous else None
        check(s, a.stage, previous)
        pathlib.Path(a.output).write_text(json.dumps(s, sort_keys=True, indent=2) + '\n')
    except (AssertionError, sqlite3.Error, OSError, ValueError) as e:
        # No paths, raw exception bodies, rows, DB, logs or config are emitted.
        print('assertion_not_satisfied' if isinstance(e, AssertionError) else 'readonly_probe_unavailable')
        sys.exit(1)
