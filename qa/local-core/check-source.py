import json, pathlib, sys
run, artifact = [json.loads(pathlib.Path(p).read_text()) for p in sys.argv[1:]]
assert run['id'] == 42523442072 and run['head_sha'] == 'da37da0caa8edf5653a9b02483a4e37d739179cf'
assert run['status'] == 'completed' and run['conclusion'] == 'success' and run['run_attempt'] == 1
assert run['repository']['full_name'] == 'frpboy/Scrobblix' and not run['repository']['private']
assert artifact['id'] == 13633913382 and not artifact['expired']
assert artifact['size_in_bytes'] == 135360417
assert artifact['digest'] == 'sha256:c9407c3f7c26ad5a3a39dbdaa85ccb3655e49cb3ccc0e5f9d4004301ed82ea0b'
assert artifact['workflow_run']['id'] == run['id'] and artifact['workflow_run']['head_sha'] == run['head_sha']
print('fixed source run/artifact identity verified')
