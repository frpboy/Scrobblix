"""Exact source equivalence and current main gate; read-only GitHub API."""
from pathlib import Path
import subprocess,json
root=Path(__file__).resolve().parents[2];m=json.loads((Path(__file__).parent/'artifacts.json').read_text())
assert m['canonical_source']=='5c54607c57b07d2e5e7f3dc6db46d09f36c71291'
assert m['canonical_tree']=='473e00685aee316306eef2cddf0ed505dfaa34c8'
def git(*args):return subprocess.check_output(['git','-C',str(root),*args],text=True).strip()
assert git('rev-parse',m['canonical_source']+'^{tree}')==m['canonical_tree']
assert git('rev-parse','HEAD^')==m['canonical_source']
changed=git('diff','--name-only',m['canonical_source'],'HEAD').splitlines()
assert changed and all(x.startswith('qa/alpha2-update/')or x=='.github/workflows/android-signed-update-rehearsal.yml'for x in changed)
# No checkout differences outside exact QA/workflow allowlist. Closure checks all permitted files.
assert not git('diff',m['canonical_source'],'HEAD','--','.',':(exclude)qa/alpha2-update/**',':(exclude).github/workflows/android-signed-update-rehearsal.yml')
current=json.loads(subprocess.check_output(['gh','api','repos/frpboy/Scrobblix/commits/main'],text=True))
assert current['sha']==m['canonical_source']and current['commit']['tree']['sha']==m['canonical_tree']
print('PASS exact canonical source/tree/direct lineage and production-source equivalence')
