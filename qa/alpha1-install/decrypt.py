"""Pinned AES256 OpenPGP transport, secret via stdin only, exact APK digest."""
import hashlib,os,pathlib,subprocess,tempfile
CIPHER_SHA='ac8d094dc8230e47d9f3aa3f656de151c99687fb15d24f31e8391b09b09e1ee3'
PLAIN_SHA='8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc'

def decrypt(source,target,cipher_sha,plain_sha,passphrase):
    assert source.stat().st_size<9_000_000
    assert hashlib.sha256(source.read_bytes()).hexdigest()==cipher_sha
    assert not target.exists() and len(passphrase)>=40
    env=dict(os.environ);env.pop('SCROBBLIX_ALPHA1_TRANSPORT',None)
    with tempfile.TemporaryDirectory() as home:
        os.chmod(home,0o700)
        result=subprocess.run(['gpg','--homedir',home,'--batch','--quiet','--no-symkey-cache','--pinentry-mode','loopback','--passphrase-fd','0','--output',str(target),'--decrypt',str(source)],input=passphrase+b'\n',stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,env=env)
    if result.returncode!=0:
        target.unlink(missing_ok=True)
        raise AssertionError('transport_decryption_failed')
    if target.stat().st_size!=8632540 or hashlib.sha256(target.read_bytes()).hexdigest()!=plain_sha:
        target.unlink(missing_ok=True)
        raise AssertionError('plaintext_identity_mismatch')
    target.chmod(0o600)

if __name__=='__main__':
    phrase=os.environ.pop('SCROBBLIX_ALPHA1_TRANSPORT','').encode()
    try:
        decrypt(pathlib.Path(__file__).with_name('candidate.apk.gpg'),pathlib.Path(os.environ['RUNNER_TEMP'])/'alpha1-exact.apk',CIPHER_SHA,PLAIN_SHA,phrase)
    finally:
        phrase=b''
