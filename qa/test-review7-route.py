from pathlib import Path
import ast,re,unittest,subprocess,tempfile,os
class Review7Route(unittest.TestCase):
 def test_four_jobs_exact_branch_and_caps(self):
  s=Path('.github/workflows/scrobblix-review7-qualification.yml').read_text()
  self.assertIn('branches: [qa/review7-android-20261108-once]',s)
  self.assertEqual(['25','55'],re.findall(r'timeout-minutes: (\d+)',s))
  self.assertEqual(2,s.count('api: [26, 36]'))
  for x in ['workflow_dispatch:', 'pull_request:', 'tags:', 'workflow_call:', 'windows-latest', 'self-hosted']:self.assertNotIn(x,s)
  self.assertEqual(2,s.count('retention-days: 1'))
 def test_exact_inventory_and_gated_exclusion(self):
  tree=ast.parse(Path('qa/review7-inventory.py').read_text());assignment=next(x for x in tree.body if isinstance(x,ast.Assign)and any(isinstance(t,ast.Name)and t.id=='expected'for t in x.targets));expected=ast.literal_eval(assignment.value)
  self.assertEqual({'core':84,'platform':15,'app':56},{k:len(v)for k,v in expected.items()})
  for k,v in expected.items():self.assertEqual(len(v),len(set(v)))
  self.assertIn('dev.scrobblix.platform.AndroidStorageTest#walDriverReturnsExecutingStatementCountsAndPreservesTransactions',expected['platform'])
  s=Path('.github/workflows/scrobblix-review7-qualification.yml').read_text()
  for x in ['notClass=dev.scrobblix.app.LiveTokenStartQualificationTest,dev.scrobblix.app.ProcessRecoveryTest,dev.scrobblix.app.SignedUpdateRehearsalTest','--offline','id -u','ip6tables','runtime-manifest.json','inputs-online.sha256','setsid emulator','-TERM -- "-$emulator_pid"','-KILL -- "-$emulator_pid"','EXPECTED_COMMIT: ${{ github.sha }}']:self.assertIn(x,s)
 def test_only_ten_targeted_label_pixels_pulled(self):
  s=Path('.github/workflows/scrobblix-review7-qualification.yml').read_text()
  pulls=re.search(r'for name in (reason-label-[^;]+); do',s).group(1).split()
  expected=[f'reason-label-{tab}-{i}'for tab in ['queue','history']for i in range(5)]
  self.assertEqual(expected,pulls)
  test=Path('android/app/src/androidTest/kotlin/dev/scrobblix/app/AuthorityUiStateTest.kt').read_text()
  for x in ['"Queue" to 1f,"History" to 2f','assertEquals(before,store.sql.query','capture("reason-label-${tab.lowercase()}-$index",expected)']:self.assertIn(x,test)
 def test_source_binding_and_no_keys(self):
  s=Path('qa/review7-source.py').read_text()
  self.assertIn("git('rev-parse','HEAD^{tree}')==pins[0]",s)
  self.assertIn("git('rev-parse','HEAD:android')=='",s)
  self.assertNotIn('PENDING',s)
  a=Path('qa/narrow-target-contract.py').read_text()
  self.assertNotIn('PENDING',a)
  self.assertIn('SCROBBLIX_SIGNING_STORE',s)
 def test_all_workflow_shell_blocks_parse(self):
  # YAML block scripts are checked without evaluating GitHub expressions.
  s=Path('.github/workflows/scrobblix-review7-qualification.yml').read_text()
  for script in re.findall(r'^        run: \|\n((?:          .*\n|\n)+)',s,re.M):
   shell='\n'.join(x[10:]for x in script.splitlines());shell=re.sub(r'\$\{\{.*?\}\}', '26',shell)
   r=subprocess.run(['bash','-n'],input=shell,text=True,capture_output=True);self.assertEqual(0,r.returncode,r.stderr)
if __name__=='__main__':unittest.main()
