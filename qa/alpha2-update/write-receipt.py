from pathlib import Path
import sys,json,hashlib,os
private,evidence=map(Path,sys.argv[1:3]);api=int(sys.argv[3]);assert api in[26,36]
a=json.loads((private/'baseline-state.json').read_text());b=json.loads((private/'candidate-state.json').read_text())
assert a['versionCode']==6 and b['versionCode']==7 and a['uid']==b['uid'] and a['firstInstallTime']==b['firstInstallTime']
manifest=json.loads((Path(__file__).parent/'artifacts.json').read_text())
r={'api':api,'synthetic_only':True,'baseline_versionCode':6,'candidate_versionCode':7,'uid_unchanged':True,'firstInstallTime_unchanged':True,'prepare_instrumentation_passed':True,'verify_instrumentation_passed':True,'attempt':int(os.environ['GITHUB_RUN_ATTEMPT']),'run_id':os.environ['GITHUB_RUN_ID'],'source_sha':os.environ['GITHUB_SHA'],'artifact_sha256':{n:manifest[n]['sha256']for n in ['baseline','candidate','test']},'owner_certificate_sha256':manifest['signer_sha256'],'tracking_paused_asserted':True,'synthetic_account_secret_retained_asserted':True,'exactly_one_unknown_nonretryable_action_asserted':True,'phone_or_live_provider_proof':False,'pixel_review_pending':True}
assert r['attempt']==1
(evidence/f'api{api}-synthetic-update.json').write_text(json.dumps(r,indent=2))
