from pathlib import Path
import runpy
import unittest
c=runpy.run_path('qa/signer-output-contract.py');parse=c['parse'];receipt=c['receipt'];Error=c['ContractError']
a=Path('qa/fixtures/apksigner36-test5-public.txt').read_text();b=Path('qa/fixtures/apksigner37-test5-public.txt').read_text()
class SignerContractTest(unittest.TestCase):
 def test_actual36and37same_apk(self): self.assertEqual(parse(a),parse(b))
 def test_expected_actual_public_cert(self): self.assertEqual('9ab2a3edbb25ab9a3c921dfabef8a95d63761c3367e72e19026adb43851e7285',parse(b))
 def test_empty_rejected(self):
  for text in ['', 'Verifies\nNumber of signers: 1\n']:
   with self.assertRaises(Error):parse(text)
 def test_multiple_signers_rejected(self):
  with self.assertRaises(Error):parse(a.replace('Number of signers: 1','Number of signers: 2'))
 def test_malformed_digest_rejected(self):
  for bad in ['','a'*63,'a'*65,'z'*64]:
   with self.assertRaises(Error):parse(a.replace(parse(a),bad))
 def test_duplicate_entry_rejected(self):
  with self.assertRaises(Error):parse(a+'Signer #1: certificate SHA-256 digest: '+parse(a)+'\n')
 def test_distinct_scheme_certs_rejected(self):
  with self.assertRaises(Error):parse(b+'V3 Signer: certificate SHA-256 digest: '+'a'*64+'\n')
 def test_same_scheme_cert_allowed(self):self.assertEqual(parse(b),parse(b+'V3 Signer: certificate SHA-256 digest: '+parse(b)+'\n'))
 def test_mismatched_artifact_rejected(self):
  with self.assertRaises(Error):receipt({'baseline':a,'test':b,'candidate':b.replace(parse(b),'a'*64)},'0.9')
 def test_empty_process_substitution_cannot_pass(self):
  with self.assertRaises(Error):receipt({'baseline':'','test':'','candidate':''},'0.9')
 def test_receipt_has_only_public_values(self):
  result=receipt({'baseline':a,'test':b,'candidate':b},'0.9');self.assertEqual(set(result),{'status','error_category','tool_version','certificate_sha256'})
if __name__=='__main__':unittest.main()
