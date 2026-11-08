"""Fail closed to full CI. Only nonempty Markdown-only changes may use docs CI.
Uses immutable event commit identities and git objects, never a truncated API file list.
"""
import json,os,pathlib,re,subprocess

def docs_paths(raw):
    try:names=raw.decode('utf-8',errors='strict').split('\0')
    except UnicodeDecodeError:return False
    if not names or names[-1]!='':return False
    names=names[:-1]
    return bool(names) and len(names)<=10000 and all(
        n=='README.md' or (n.startswith('docs/') and n.endswith('.md') and
        not any(p in ('','..','.')for p in n.split('/')) and
        all(32<=ord(c)<127 for c in n) and '\\'not in n)
        for n in names)

def allowed_diff(git,base,head):
    raw=git('diff','--no-renames','--name-only','-z',base,head,'--')
    if len(raw)>1048576 or not docs_paths(raw):return False
    changed=set(raw.decode().split('\0')[:-1])
    tree=git('ls-tree','-r','-z',head,'--','README.md','docs/')
    if len(tree)>4194304:return False
    for row in tree.split(b'\0'):
        if not row:continue
        meta,name=row.split(b'\t',1)
        if name.decode()in changed and not meta.startswith(b'100644 blob '):return False
    return True

def classify(event_name,event,sha,git):
    if not re.fullmatch('[0-9a-f]{40}',sha or ''):return False
    if git('rev-parse','HEAD').decode().strip()!=sha:return False
    if event_name=='pull_request':
        pr=event.get('pull_request',{})
        if pr.get('base',{}).get('ref')!='main':return False
        base=pr.get('base',{}).get('sha','');head=pr.get('head',{}).get('sha','')
        if not all(re.fullmatch('[0-9a-f]{40}',x or '')for x in (base,head)):return False
        # Synthetic merge checkout must include both authenticated event commits.
        if git('merge-base','--is-ancestor',head,sha)!=b'':return False
        if git('merge-base','--is-ancestor',base,sha)!=b'':return False
        merge=git('merge-base',base,head).decode().strip()
        return allowed_diff(git,merge,head)
    if event_name=='push':
        if event.get('ref')not in ('refs/heads/main','refs/heads/feat/private-beta-readiness'):return False
        if event.get('created')or event.get('deleted')or event.get('forced'):return False
        base=event.get('before','');head=event.get('after','')
        if head!=sha or not re.fullmatch('[0-9a-f]{40}',base or '')or base=='0'*40:return False
        git('merge-base','--is-ancestor',base,head)
        return allowed_diff(git,base,head)
    return False

def main():
    result=False
    try:
        def git(*args):return subprocess.check_output(['git',*args],stderr=subprocess.DEVNULL,timeout=15)
        event=json.loads(pathlib.Path(os.environ['GITHUB_EVENT_PATH']).read_text())
        result=classify(os.environ.get('GITHUB_EVENT_NAME'),event,os.environ.get('GITHUB_SHA'),git)
    except Exception:result=False
    with open(os.environ['GITHUB_OUTPUT'],'a')as f:f.write('docs_only='+str(result).lower()+'\n')
    print('CI route: '+('Markdown validation only'if result else 'full build and test'))
if __name__=='__main__':main()
