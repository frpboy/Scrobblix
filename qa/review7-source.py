#!/usr/bin/env python3
"""Execution binding only. External review and full-tree comparison precede publication."""
import os,re,subprocess

def git(*args): return subprocess.check_output(['git',*args],text=True).strip()
commit=os.environ.get('EXPECTED_COMMIT','')
assert re.fullmatch('[0-9a-f]{40}',commit) and git('rev-parse','HEAD')==commit
pins=re.findall(r'^Qualification-Tree: ([0-9a-f]{40})$',git('log','-1','--format=%B'),re.M)
assert len(pins)==1 and git('rev-parse','HEAD^{tree}')==pins[0]
assert not git('status','--porcelain')
assert git('rev-parse','HEAD:android')=='61169e8e2ff5f12e8d9c9b241e26837711c9b5bd'
assert git('rev-parse','HEAD:tests/fixtures/scrobble-eligibility.tsv')=='ab8dd294725fbb9f322e261b41912ddfad1eaa8e'
assert not os.environ.get('SCROBBLIX_SIGNING_STORE') and not os.environ.get('SCROBBLIX_SIGNING_PASSWORD_FILE')
print('REVIEWED_REVIEW7_TREE',pins[0])
