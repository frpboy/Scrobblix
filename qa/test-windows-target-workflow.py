from pathlib import Path
import re,unittest
class WindowsRouteTest(unittest.TestCase):
 def test_exact_branch_and_caps(self):
  s=Path('.github/workflows/scrobblix-windows-target.yml').read_text()
  self.assertIn('branches: [qa/windows-target-20261108-once]',s)
  for token in ['workflow_dispatch:','pull_request:','tags:','workflow_call:']:self.assertNotIn(token,s)
  self.assertEqual(['45','10'],re.findall(r'timeout-minutes: (\d+)',s))
 def test_all_ci_steps_preserved_except_qualified_console(self):
  a=Path('.github/workflows/ci.yml').read_text();b=Path('.github/workflows/scrobblix-windows-target.yml').read_text()
  names=lambda s:re.findall(r'^      - name: (.+)$',s,re.M)
  self.assertEqual([n for n in names(a) if n!='Run domain and infrastructure assertions'],[n for n in names(b) if n!='Bind reviewed source and unchanged Windows console provenance'])
  self.assertNotIn('dotnet run --project tests/Scrobblix.Domain.Tests',b)
  for token in ['test-artwork-ui.ps1','test-portable-package.ps1','SyntheticLegacyProfile','RequirePrivateRuntime','needs: build-and-test']:self.assertIn(token,b)
 def test_source_pins_and_retention(self):
  s=Path('.github/workflows/scrobblix-windows-target.yml').read_text()
  self.assertIn('EXPECTED_COMMIT: ${{ github.sha }}',s);self.assertNotIn('retention-days: 14',s);self.assertNotIn('retention-days: 7',s)
  self.assertIn('qa/windows-target-source.py',s)
  uploads=re.split(r'(?m)^      - ',s)
  uploads=[part for part in uploads if 'uses: actions/upload-artifact@' in part]
  self.assertEqual(6,len(uploads))
  for upload in uploads:self.assertRegex(upload,r'(?m)^          retention-days: 1$')
if __name__=='__main__':unittest.main()
