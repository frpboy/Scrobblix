#!/usr/bin/env python3
import os,re,subprocess

def git(*args):return subprocess.check_output(['git',*args],text=True).strip()
commit=os.environ.get('EXPECTED_COMMIT','')
assert re.fullmatch('[0-9a-f]{40}',commit) and git('rev-parse','HEAD')==commit
pins=re.findall(r'^Qualification-Tree: ([0-9a-f]{40})$',git('log','-1','--format=%B'),re.M)
assert len(pins)==1 and git('rev-parse','HEAD^{tree}')==pins[0]
assert not git('status','--porcelain')
for path,expected in {'src':'34ba5bd0ecc296c6d7e848312279327a9fa1e972','tests/Scrobblix.Domain.Tests':'0c4d00b54ab91cb37581b71bdd1c623f98a0e45c','tests/fixtures/scrobble-eligibility.tsv':'ab8dd294725fbb9f322e261b41912ddfad1eaa8e'}.items():
    assert git('rev-parse','HEAD:'+path)==expected
assert not os.environ.get('SCROBBLIX_SIGNING_STORE') and not os.environ.get('SCROBBLIX_SIGNING_PASSWORD_FILE')
print('REVIEWED_WINDOWS_TREE',pins[0],'DOMAIN_CONSOLE_INHERITED_FROM_42438756642_NOT_REPEATED')
