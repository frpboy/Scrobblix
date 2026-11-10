"""Fixed signed APK only. No owner signing secret, credentials or account data."""
import hashlib,json,os,pathlib,urllib.request,urllib.parse
BASE='https://api.github.com/repos/frpboy/Scrobblix'
TAG='v0.1.0-android-alpha.1'
NAME='Scrobblix-Android-v0.1.0-alpha.1.apk'
SHA='8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc'
class SafeRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,req,fp,code,msg,headers,url):
        new=super().redirect_request(req,fp,code,msg,headers,url)
        if new and urllib.parse.urlparse(req.full_url).netloc!=urllib.parse.urlparse(url).netloc:
            new.remove_header('Authorization')
        return new
opener=urllib.request.build_opener(SafeRedirect)
def request(path,accept='application/vnd.github+json'):
    req=urllib.request.Request(BASE+path,headers={'Authorization':'Bearer '+os.environ['GH_TOKEN'],'Accept':accept,'User-Agent':'Scrobblix-alpha-qualification'})
    return opener.open(req,timeout=60)
with request('/releases?per_page=100') as response: releases=json.load(response)
matches=[r for r in releases if r['tag_name']==TAG]
assert len(matches)==1
r=matches[0]
assert r['draft'] is True and r['prerelease'] is True
assert r['target_commitish']=='acf3d1bfc7c395bedff3a854b05e1b6be870b50e'
assets=[a for a in r['assets'] if a['name']==NAME]
assert len(assets)==1
asset=assets[0]
assert asset['state']=='uploaded' and asset['size']==8632540
assert asset.get('digest')=='sha256:'+SHA
path=pathlib.Path(os.environ['RUNNER_TEMP'])/'alpha1-exact.apk'
assert not path.exists()
with request('/releases/assets/'+str(asset['id']),'application/octet-stream') as response,path.open('xb') as dest:
    import shutil
    shutil.copyfileobj(response,dest)
assert path.stat().st_size==8632540
assert hashlib.sha256(path.read_bytes()).hexdigest()==SHA
print('exact owner-signed APK retrieved; no signing secret supplied')
