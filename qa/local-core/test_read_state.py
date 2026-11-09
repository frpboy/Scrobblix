import importlib.util
import pathlib
import sqlite3
import tempfile
import unittest
spec = importlib.util.spec_from_file_location('reader', pathlib.Path(__file__).with_name('read-state.py'))
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)

class Checks(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory(prefix='synthetic-reader-unit-')
        self.path = pathlib.Path(self.t.name)/'mock.db'
        self.db = sqlite3.connect(self.path)
        self.db.executescript('''CREATE TABLE accounts (n INT); CREATE TABLE provider_actions (n INT);
          CREATE TABLE plays (artist TEXT,title TEXT,started_utc TEXT,duration_ms INT,
          confirmed_played_ms INT,eligibility_threshold_ms INT,eligible_utc TEXT);
          CREATE TABLE playback_observations (artist TEXT,title TEXT,source_name TEXT,
          playback_state INT,observed_utc TEXT,sequence_number INT);''')
        self.db.commit()
    def tearDown(self):
        self.db.close(); self.t.cleanup()
    def fixture(self):
        self.db.execute('INSERT INTO plays VALUES (?,?,?,?,?,?,?)',
          (r.ARTIST,r.TITLE,'synthetic',90000,46000,45000,'synthetic'))
        self.db.executemany('INSERT INTO playback_observations VALUES (?,?,?,?,?,?)',
          [(r.ARTIST,r.TITLE,'gsmtc',1,'synthetic',n) for n in (1,2)])
        self.db.commit()
    def reject(self,stage='eligible',previous=None):
        with self.assertRaises(AssertionError): r.check(r.state(self.path),stage,previous)
    def test_baseline(self): r.check(r.state(self.path),'baseline')
    def test_eligible(self): self.fixture(); r.check(r.state(self.path),'eligible')
    def test_no_account(self):
        self.fixture(); self.db.execute('INSERT INTO accounts VALUES(1)'); self.db.commit(); self.reject()
    def test_no_provider_action(self):
        self.fixture(); self.db.execute('INSERT INTO provider_actions VALUES(1)'); self.db.commit(); self.reject()
    def test_other_source(self):
        self.fixture(); self.db.execute("UPDATE playback_observations SET source_name='browser'"); self.db.commit(); self.reject()
    def test_other_media(self):
        self.fixture(); self.db.execute("UPDATE playback_observations SET artist='other'"); self.db.commit(); self.reject()
    def test_duration(self):
        self.fixture(); self.db.execute('UPDATE plays SET duration_ms=30000'); self.db.commit(); self.reject()
    def test_insufficient_time(self):
        self.fixture(); self.db.execute('UPDATE plays SET confirmed_played_ms=44999'); self.db.commit(); self.reject()
    def test_two_canonical_plays(self):
        self.fixture(); self.db.execute('INSERT INTO plays SELECT * FROM plays'); self.db.commit(); self.reject()
    def test_pause(self):
        self.fixture(); self.reject('paused')
        self.db.execute('INSERT INTO playback_observations VALUES (?,?,?,?,?,?)',
                        (r.ARTIST,r.TITLE,'gsmtc',2,'synthetic',3)); self.db.commit()
        before=r.state(self.path); r.check(before,'paused')
        self.reject('stable',before)
        self.db.execute('INSERT INTO playback_observations VALUES (?,?,?,?,?,?)',
                        (r.ARTIST,r.TITLE,'gsmtc',2,'synthetic',4)); self.db.commit()
        r.check(r.state(self.path),'stable',before)
        self.db.execute('UPDATE plays SET confirmed_played_ms=confirmed_played_ms+1'); self.db.commit()
        self.reject('stable',before)
    def test_readonly(self):
        uri=self.path.resolve().as_uri()+'?mode=ro'
        with sqlite3.connect(uri,uri=True) as db:
            with self.assertRaises(sqlite3.OperationalError): db.execute('INSERT INTO accounts VALUES(1)')

if __name__=='__main__': unittest.main()
