#!/usr/bin/env python3
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).with_name('android-rehearsal-version.py')
spec = importlib.util.spec_from_file_location('version_helper', SCRIPT)
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)

class RehearsalVersionTest(unittest.TestCase):
    def test_current_source_two_becomes_three_not_one(self):
        source = 'versionName = "unchanged"\n        versionCode = 2\nother = 2\n'
        after, old, new = helper.prepare(source)
        self.assertEqual((2, 3), (old, new))
        self.assertEqual(source.replace('versionCode = 2', 'versionCode = 3'), after)
    def test_historical_one_and_future_values(self):
        for value in [1, 5, 17, 2099999999]:
            with self.subTest(value=value):
                _, old, new = helper.prepare(f'versionCode = {value}\n')
                self.assertEqual((value, value + 1), (old, new))
    def test_missing_literal_rejected(self):
        for source in ['', 'versionCode = providers.gradleProperty("version")\n', 'versionCode = -1\n']:
            with self.subTest(source=source):
                with self.assertRaises(ValueError): helper.prepare(source)
    def test_duplicate_assignments_rejected(self):
        with self.assertRaises(ValueError): helper.prepare('versionCode = 1\nversionCode = 2\n')
    def test_zero_and_android_maximum_rejected(self):
        for value in [0, 2100000000, 9999999999]:
            with self.assertRaises(ValueError): helper.prepare(f'versionCode = {value}\n')
    def test_comment_not_assignment(self):
        after, old, new = helper.prepare('// versionCode = 1\n\tversionCode = 5 \n')
        self.assertEqual((5, 6), (old, new));self.assertTrue(after.startswith('// versionCode = 1'))
    def test_cli_only_build_identity_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'build.gradle.kts'
            source = 'versionCode = 2\napplicationId = "synthetic.package"\n'
            path.write_text(source)
            result = subprocess.run([sys.executable, str(SCRIPT), str(path)], text=True, capture_output=True, check=True)
            self.assertEqual('2\t3\n', result.stdout)
            self.assertEqual(source.replace('versionCode = 2', 'versionCode = 3'), path.read_text())
    def test_rehearsal_uses_actual_versions_and_never_downgrades(self):
        source = SCRIPT.with_name('android-signed-update-rehearsal.sh').read_text()
        self.assertIn('python3 scripts/android-rehearsal-version.py android/app/build.gradle.kts', source)
        self.assertIn("versionCode='$baseline_version'", source)
        self.assertIn("versionCode='$candidate_version'", source)
        self.assertIn('adb install -r "$private/candidate.apk"', source)
        self.assertNotIn('adb uninstall', source)
        self.assertNotIn('adb install -r -d', source)
        self.assertLess(source.index('cp android/app/build/outputs/apk/debug/app-debug.apk'), source.index('versions=$(python3'))
        self.assertLess(source.index('versions=$(python3'), source.index(':app:assembleRelease'))
    def test_fixed_reviewed_two_source_fixture_is_supported(self):
        # Fixed source snapshot from reviewed b33199; deliberately not a live repository-source check.
        source = SCRIPT.with_name('current-build.gradle.kts.txt').read_text()
        _, baseline, candidate = helper.prepare(source)
        self.assertEqual((2, 3), (baseline, candidate))
    def test_cli_invalid_source_no_write(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'build.gradle.kts';source = 'versionCode = 2100000000\n';path.write_text(source)
            result = subprocess.run([sys.executable, str(SCRIPT), str(path)], text=True, capture_output=True)
            self.assertNotEqual(0, result.returncode);self.assertEqual(source, path.read_text());self.assertEqual('', result.stdout)

if __name__ == '__main__': unittest.main()
