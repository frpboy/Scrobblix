#!/usr/bin/env python3
import os
from pathlib import Path
import re
import subprocess
import sys

def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()

def source():
    commit = os.environ.get('EXPECTED_COMMIT', '')
    assert re.fullmatch('[0-9a-f]{40}',commit) and git('rev-parse','HEAD')==commit, 'Event commit mismatch'
    message=git('log','-1','--format=%B')
    pins=re.findall(r'^Qualification-Tree: ([0-9a-f]{40})$',message,re.M)
    assert len(pins)==1, 'Exactly one final reviewed tree trailer required'
    expected=pins[0]
    assert re.fullmatch('[0-9a-f]{40}', expected), 'Expected full tree required'
    assert git('rev-parse', 'HEAD^{tree}') == expected, 'Unreviewed source tree'
    assert not git('status', '--porcelain'), 'Checkout modified'
    assert git('rev-parse', 'HEAD:android') == 'a3cb65af446ddd4b0fd3d4d0031f1f0d594c73cf'
    assert git('rev-parse', 'HEAD:tests/fixtures/scrobble-eligibility.tsv') == 'ab8dd294725fbb9f322e261b41912ddfad1eaa8e'
    assert not os.environ.get('SCROBBLIX_SIGNING_STORE') and not os.environ.get('SCROBBLIX_SIGNING_PASSWORD_FILE')
    print('REVIEWED_SOURCE_TREE', expected)

def phase(text, cls, method):
    lines=[line.strip() for line in text.replace('\r','').splitlines()]
    assert not re.search(r'shortMsg|FAILURES|INSTRUMENTATION_FAILED|Process crashed|\bignored\b|\bskipped\b|INSTRUMENTATION_RESULT: (?:error|failure)', text, re.I)
    assert lines.count('OK (1 test)') == 1
    assert [x for x in lines if x.startswith('INSTRUMENTATION_CODE:')] == ['INSTRUMENTATION_CODE: -1']
    blocks=[]; current=[]
    for line in lines:
        if line.startswith('INSTRUMENTATION_STATUS: '): current.append(line[len('INSTRUMENTATION_STATUS: '):])
        elif line.startswith('INSTRUMENTATION_STATUS_CODE:'):
            blocks.append((current,line));current=[]
    assert not current and len(blocks)==2
    for (fields,status), expected_status in zip(blocks,['INSTRUMENTATION_STATUS_CODE: 1','INSTRUMENTATION_STATUS_CODE: 0']):
        assert status==expected_status
        for key,value in [('class',cls),('test',method),('numtests','1'),('current','1')]:
            assert [f for f in fields if f.startswith(key+'=')] == [key+'='+value]
    assert lines.index('INSTRUMENTATION_STATUS_CODE: 1') < lines.index('INSTRUMENTATION_STATUS_CODE: 0') < lines.index('INSTRUMENTATION_CODE: -1')

def redact_phase(text):
    # Keep only fixed runner protocol and expected synthetic identities, never stack or free text.
    allowed=re.compile(r'(?:INSTRUMENTATION_STATUS: (?:class=dev\.scrobblix\.app\.(?:ProcessRecoveryTest|SignedUpdateRehearsalTest)|test=(?:prepareBeforeProcessDeath|verifyAfterProcessDeath|preserveSyntheticStateAcrossSignedReleaseUpdate)|(?:numtests|current)=[0-9]+)|INSTRUMENTATION_STATUS_CODE: -?[0-9]+|INSTRUMENTATION_CODE: -?[0-9]+|OK \(1 test\))')
    lines=[]
    for line in text.replace('\r','').splitlines():
        if allowed.fullmatch(line.strip()): lines.append(line.strip())
        elif re.search(r'shortMsg|FAILURES|INSTRUMENTATION_FAILED|Process crashed|\bignored\b|\bskipped\b|INSTRUMENTATION_RESULT: (?:error|failure)',line,re.I): lines.append('INSTRUMENTATION_FAILED: redacted_failure')
    return '\n'.join(lines)+'\n'

def evidence():
    import json
    root=Path('narrow-evidence')
    allowed={'process-prepare.txt','process-verify.txt','update-prepare.txt','update-verify.txt','inputs-online.sha256','inputs-offline.sha256','identity.txt','source.txt','manifest.json','overlay.txt','signer.json'}
    assert root.is_dir()
    assert {p.name for p in root.iterdir()} <= allowed
    assert all(p.is_file() and not p.is_symlink() and p.stat().st_size<=1024*1024 for p in root.iterdir())
    assert sum(p.stat().st_size for p in root.iterdir()) <= 10*1024*1024
    manifest=json.loads((root/'manifest.json').read_text())
    assert set(manifest)=={'source_tree','api','attempt','status','first_cause','cleanup','error_category'}
    assert re.fullmatch('[0-9a-f]{40}',manifest['source_tree'])
    assert manifest['api'] in ['26','36'] and manifest['attempt'].isdigit()
    assert manifest['status'] in ['pass','failed'] and re.fullmatch('[a-z0-9_-]{1,64}',manifest['first_cause'])
    assert manifest['error_category'] in ['none','cleanup_failure','bounded_command_nonzero']
    assert manifest['cleanup'] in ['no_guest','dead','failed']
    if manifest['status']=='pass':
        assert {p.name for p in root.iterdir()} == allowed
        assert manifest['cleanup']=='dead'
        signer=json.loads((root/'signer.json').read_text())
        assert signer['status']=='pass' and signer['error_category']=='none'
        assert set(signer['certificate_sha256'])=={'baseline','test','candidate'}
        values=list(signer['certificate_sha256'].values())
        assert len(set(values))==1 and all(re.fullmatch('[0-9a-f]{64}',v) for v in values)
        for name, cls, method in [('process-prepare','ProcessRecoveryTest','prepareBeforeProcessDeath'),('process-verify','ProcessRecoveryTest','verifyAfterProcessDeath'),('update-prepare','SignedUpdateRehearsalTest','preserveSyntheticStateAcrossSignedReleaseUpdate'),('update-verify','SignedUpdateRehearsalTest','preserveSyntheticStateAcrossSignedReleaseUpdate')]:
            phase((root/(name+'.txt')).read_text(), 'dev.scrobblix.app.'+cls, method)
        assert (root/'inputs-online.sha256').read_bytes() == (root/'inputs-offline.sha256').read_bytes()
        assert (root/'inputs-online.sha256').stat().st_size > 0

if __name__ == '__main__':
    {'source':source,'evidence':evidence}[sys.argv[1]]()
