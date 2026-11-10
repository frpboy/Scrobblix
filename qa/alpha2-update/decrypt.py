"""LOCAL SOURCE PROPOSAL ONLY. Exact signed APKs, never owner signing key."""
from pathlib import Path
import hashlib,os,subprocess,tempfile,json

def decrypt(source,target,entry,phrase):
    assert not target.exists() and 40<=len(phrase)<=128
    assert source.stat().st_size==entry['cipher_bytes']<10_000_000
    assert hashlib.sha256(source.read_bytes()).hexdigest()==entry['cipher_sha256']
    env=dict(os.environ);env.pop('SCROBBLIX_ALPHA2_TRANSPORT',None)
    try:
        with tempfile.TemporaryDirectory()as home:
            os.chmod(home,0o700)
            r=subprocess.run(['gpg','--homedir',home,'--batch','--quiet','--no-symkey-cache','--pinentry-mode','loopback','--passphrase-fd','0','--output',str(target),'--decrypt',str(source)],input=phrase+b'\n',stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,env=env,timeout=60)
        assert r.returncode==0,'transport_decryption_failed'
        assert target.stat().st_size==entry['bytes']
        assert hashlib.sha256(target.read_bytes()).hexdigest()==entry['sha256'],'plaintext_identity_mismatch'
        target.chmod(0o600)
    except BaseException:
        target.unlink(missing_ok=True)
        raise

def main():
    phrase=os.environ.pop('SCROBBLIX_ALPHA2_TRANSPORT','').encode()
    private=Path(os.environ['RUNNER_TEMP'])/'alpha2-private'
    assert not private.exists()
    private.mkdir(mode=0o700)
    root=Path(__file__).parent
    manifest=json.loads((root/'artifacts.json').read_text())
    # Missing final manifest/ciphertext identity intentionally blocks this preview.
    try:
        for name in ['baseline','candidate','test']:
            decrypt(root/(name+'.apk.gpg'),private/(name+'.apk'),manifest[name],phrase)
    except BaseException:
        for name in ['baseline','candidate','test']:(private/(name+'.apk')).unlink(missing_ok=True)
        raise
    finally:phrase=b''
if __name__=='__main__':main()
