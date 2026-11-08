#!/usr/bin/env python3
from pathlib import Path
import runpy
import unittest
import subprocess
import tempfile
import os
phase=runpy.run_path(str(Path(__file__).with_name('narrow-target-contract.py')))['phase']
GOOD=Path('qa/fixtures/process-prepare-api26.txt').read_text()
CLS='dev.scrobblix.app.ProcessRecoveryTest'
METHOD='prepareBeforeProcessDeath'
class ContractTest(unittest.TestCase):
    def test_exact_phase(self): phase(GOOD,CLS,METHOD)
    def test_wrong_class(self):
        with self.assertRaises(AssertionError): phase(GOOD,'other.Test',METHOD)
    def test_wrong_method(self):
        with self.assertRaises(AssertionError): phase(GOOD,CLS,'other')
    def test_nonzero_or_missing_count(self):
        for value in ['OK (0 tests)','OK (2 tests)','']:
            with self.assertRaises(AssertionError): phase(GOOD.replace('OK (1 test)',value),CLS,METHOD)
    def test_skip_fail_crash_rejected(self):
        for marker in ['skipped','ignored','FAILURES','INSTRUMENTATION_FAILED','Process crashed','INSTRUMENTATION_STATUS_CODE: -3']:
            with self.assertRaises(AssertionError): phase(GOOD+marker,CLS,METHOD)
    def test_duplicate_result_rejected(self):
        with self.assertRaises(AssertionError): phase(GOOD+GOOD,CLS,METHOD)
    def test_substring_and_extra_identity_rejected(self):
        for text in [GOOD.replace(METHOD,METHOD+'NotApproved'), GOOD.replace('INSTRUMENTATION_STATUS_CODE: 0','INSTRUMENTATION_STATUS: test=unapproved\nINSTRUMENTATION_STATUS_CODE: 0'), GOOD+'INSTRUMENTATION_STATUS: class=other.Test\n', GOOD.replace('numtests=1','numtests=2')]:
            with self.assertRaises(AssertionError): phase(text,CLS,METHOD)
    def test_bad_terminal_and_short_message(self):
        for text in [GOOD.replace('INSTRUMENTATION_CODE: -1','INSTRUMENTATION_CODE: 0'),GOOD+'INSTRUMENTATION_RESULT: shortMsg=Runner failed\n',GOOD.replace('STATUS_CODE: 1','STATUS_CODE: 0')]:
            with self.assertRaises(AssertionError): phase(text,CLS,METHOD)
    def test_runner_guards(self):
        s=Path('scripts/android-narrow-phases.sh').read_text()
        for token in ['--offline','inputs-online.sha256','id -u','ip6tables','install -r','cleanup_guest','boot_fenced disposable-update','signedUpdateRehearsal disposable-isolated-only']:
            self.assertIn(token,s)
        self.assertLess(s.index('cleanup_guest\n# Distinct'),s.index('boot_fenced disposable-update'))
        self.assertNotIn('adb uninstall',s);self.assertNotIn('pm clear',s)
    def test_owned_group_cleanup_kills_descendant_and_removes_private(self):
        source=Path('scripts/android-narrow-phases.sh').read_text()
        functions=source[source.index('cleanup_guest()'):source.index('trap cleanup EXIT')]
        with tempfile.TemporaryDirectory() as directory:
            command = """set -euo pipefail
private=$(mktemp -d)
mkdir narrow-evidence
cleanup_state=no_guest
cause=synthetic_negative_control
""" + functions + """
trap cleanup EXIT
setsid bash -c 'trap "" TERM; sleep 60 & wait' &
emulator_pid=$!
sleep 0.1
cleanup_guest
[[ "$cleanup_state" = dead ]]
printf "%s" "$private" > private-path.txt
"""
            result=subprocess.run(['bash','-c',command],cwd=directory,env=dict(os.environ,EXPECTED_TREE='a'*40,API='26'),capture_output=True,text=True,timeout=8)
            self.assertEqual(0,result.returncode,result.stderr)
            self.assertFalse(Path(directory,'private-path.txt').read_text() and Path(Path(directory,'private-path.txt').read_text()).exists())
            self.assertIn('"cleanup": "dead"',Path(directory,'narrow-evidence/manifest.json').read_text())
    def test_privacy_filter_drops_paths_credentials_and_stack(self):
        contract=runpy.run_path('qa/narrow-target-contract.py')
        safe=contract['redact_phase'](GOOD+'\n/home/owner/private/test.p12\npassword=private-canary\napi_key=another-canary\nstack: secret\n')
        phase(safe,CLS,METHOD)
        for value in ['private-canary','another-canary','/home/','stack:','test.p12']: self.assertNotIn(value,safe)
        with self.assertRaises(AssertionError): phase(contract['redact_phase'](GOOD+'INSTRUMENTATION_RESULT: shortMsg=password=private-canary'),CLS,METHOD)
    def test_cleanup_scan_error_is_not_dead(self):
        source=Path('scripts/android-narrow-phases.sh').read_text()
        self.assertIn('if ! processes=$(ps -eo pgid=,stat=); then cleanup_state=failed; return 1; fi',source)
        self.assertLess(source.index('rm -f "$private/test.p12"'),source.index('  cleanup_guest\n  local guest_result'))
        self.assertIn('timeout --kill-after=1s 2s rm -rf',source)
    def test_exact_one_branch_push_only(self):
        s=Path('.github/workflows/scrobblix-narrow-target.yml').read_text()
        self.assertNotIn('workflow_dispatch:',s);self.assertNotIn('pull_request:',s);self.assertIn('branches: [qa/narrow-target-20261108-once]',s);self.assertNotIn('tags:',s)
        self.assertIn('EXPECTED_COMMIT: ${{ github.sha }}',s)
if __name__=='__main__': unittest.main()
