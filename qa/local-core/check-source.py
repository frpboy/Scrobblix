import json, pathlib, sys
run, artifact = [json.loads(pathlib.Path(p).read_text()) for p in sys.argv[1:]]
assert run['id'] == 42626308433 and run['head_sha'] == '67422ae7bba6b7e3a38de5bcc0a395b6151db5d5'
assert run['status'] == 'completed' and run['conclusion'] == 'success' and run['run_attempt'] == 1
assert run['repository']['full_name'] == 'frpboy/Scrobblix' and not run['repository']['private']
assert artifact['id'] == 13678284303 and not artifact['expired']
assert artifact['size_in_bytes'] == 135357479
assert artifact['digest'] == 'sha256:4eee8fad91d38cb992db6b1eb81e6ec10b83c80d9542a37bae5ae3097334d16f'
assert artifact['workflow_run']['id'] == run['id'] and artifact['workflow_run']['head_sha'] == run['head_sha']
print('fixed source run/artifact identity verified')
