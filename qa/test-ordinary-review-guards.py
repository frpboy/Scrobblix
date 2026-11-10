from pathlib import Path
import importlib.util,subprocess,tempfile,unittest,json
class OrdinaryReviewGuards(unittest.TestCase):
 def test_concurrency_cancel_only_superseded_pr(self):
  s=Path('.github/workflows/android-kotlin.yml').read_text()
  self.assertIn("group: android-kotlin-${{ github.event_name }}-${{ github.event.pull_request.number || github.ref }}",s)
  self.assertIn("cancel-in-progress: ${{ github.event_name == 'pull_request' }}",s)
 def test_ordinary_signer_command_nonempty_three_artifacts(self):
  s=Path('scripts/android-signed-update-rehearsal.sh').read_text()
  self.assertNotIn('diff <(grep',s)
  lines=[x for x in s.splitlines()if x.startswith('python3 qa/signer-output-contract.py ')]
  self.assertEqual(1,len(lines))
  fixture=Path('qa/fixtures/apksigner37-default-sign-public.txt').read_text()
  spec=importlib.util.spec_from_file_location('signer', 'qa/signer-output-contract.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
  cert=m.parse(fixture)
  with tempfile.TemporaryDirectory()as d:
   p=Path(d)
   for scenario,expect in [('matching',True),('empty',False),('mismatch',False),('unknown',False)]:
    for name in ['baseline','test','candidate']:
     value=fixture
     if name=='candidate':
      if scenario=='empty':value=''
      if scenario=='mismatch':value=fixture.replace(cert,'f'*64 if cert!='f'*64 else 'e'*64)
      if scenario=='unknown':value=fixture.replace('V3.0 Signer:', 'Unknown Signer:')
     (p/(name+'.signature')).write_text(value)
    result=subprocess.run(['bash','-c','set -euo pipefail; private=$1; build_tools=/sdk/build-tools/37.0.0; '+lines[0],'guard',d],capture_output=True,text=True)
    self.assertEqual(expect,result.returncode==0,(scenario,result.stderr))
    receipt=json.loads((p/'signer.json').read_text());self.assertEqual('pass'if expect else 'failed',receipt['status'])
 def test_sql_count_not_separate_read_connection(self):
  s=Path('android/platform/src/main/kotlin/dev/scrobblix/platform/AndroidQueueSqlDriver.kt').read_text()
  self.assertIn('return statement.executeUpdateDelete()',s)
  self.assertNotIn('SELECT changes()',s)
if __name__=='__main__':unittest.main()
