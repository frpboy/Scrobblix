from pathlib import Path
import json,hashlib,re,subprocess,sys,os,zipfile
p=Path(sys.argv[1]);manifest=json.loads(Path(sys.argv[2]).read_text());tools=Path(os.environ['ANDROID_HOME'])/'build-tools/36.0.0'
for name in ['baseline','candidate','test']:
 f=p/(name+'.apk');m=manifest[name]
 assert f.stat().st_size==m['bytes'] and hashlib.sha256(f.read_bytes()).hexdigest()==m['sha256']
 result=subprocess.check_output([str(tools/'apksigner'),'verify','--print-certs',str(f)],text=True)
 assert re.findall(r'^Signer #\d+ certificate SHA-256 digest: (\w+)$',result,re.M)==[manifest['signer_sha256']]
 package=subprocess.check_output([str(tools/'aapt'),'dump','badging',str(f)],text=True)
 expected='com.frpboy.scrobblix.test'if name=='test'else'com.frpboy.scrobblix'
 assert re.search(r"^package: name='"+re.escape(expected)+r"'",package,re.M)
 if name!='test':
  assert "versionCode='"+str(m['versionCode'])+"'"in package
  assert "versionName='"+m['versionName']+"'"in package
  assert "sdkVersion:'26'"in package and "targetSdkVersion:'36'"in package
  assert 'application-debuggable'not in package
 else:
  xml=subprocess.check_output([str(tools/'aapt'),'dump','xmltree',str(f),'AndroidManifest.xml'],text=True)
  assert 'android:name(0x01010003)="androidx.test.runner.AndroidJUnitRunner"'in xml
  assert 'android:targetPackage(0x01010021)="com.frpboy.scrobblix"'in xml
  with zipfile.ZipFile(f)as archive:
   dexes=[archive.read(x)for x in archive.namelist()if x.endswith('.dex')]
   assert any(b'ExactReleasePairTest'in x for x in dexes)
   assert not any(b'BrokerRuntimeTest'in x for x in dexes)
print('PASS exact artifact hashes, packages, release versions, instrumentation target/class and same owner certificate')
