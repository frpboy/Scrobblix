import importlib.util,json,pathlib,tempfile,unittest
ROOT=pathlib.Path(__file__).resolve().parent

def load(name,path):
    s=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
f=load('fields','about-effective-fields.py')
p=load('probe','probe-about-effective-bars.py')
px=load('pixels6','about-effective-pixels.py')
S=b'''LightBarController:
 mAppearance=LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS
 mNavigationLight=true
 mHasLightNavigationBar=true
 mStatusBarMode=0 mNavigationBarMode=0
 mForceDarkForScrim=false
 mForceLightForScrim=false
 mQsCustomizing=false
 mQsExpanded=false
 mBouncerVisible=false
 mGlobalActionsVisible=false
 mDirectReplying=false
 mNavbarColorManagedByIme=false
 Recent Calculation Logs:
 private-unrelated-text
 NavigationBarTransitionsController:
 mTransitionDeferring=false
 mTransitionPending=false mTintChangePending=false
 mPendingDarkIntensity=1.0 mDarkIntensity=1.0 mNextDarkIntensity=1.0
'''
W=b'''Display: mDisplayId=0
 unrelated data never emitted
 DisplayPolicy
   mLastAppearance=LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS
   mHasBottomNavigationBar=true
   mNavBarColorWindowCandidate=Window{abcdef u0 com.frpboy.scrobblix/dev.scrobblix.app.MainActivity}
 DisplayRotation
   mLastAppearance=LOW_PROFILE_BARS
Display: mDisplayId=1
 DisplayPolicy
   mLastAppearance=LOW_PROFILE_BARS
'''
class Fields(unittest.TestCase):
    def test_valid(self):
        x=f.systemui(S);self.assertEqual('true',x['effectiveLightNav']);self.assertEqual('1.0',x['mDarkIntensity'])
        self.assertEqual({'navColorCandidate':'main','lastEffectiveLightNav':'true','hasBottomNav':'true'},f.policy(W))
    def test_source_space_format_not_pipe(self):self.assertEqual('unknown',f.systemui(S.replace(b'LIGHT_STATUS_BARS LIGHT',b'LIGHT_STATUS_BARS|LIGHT'))['effectiveLightNav'])
    def test_missing(self):self.assertEqual('unknown',f.systemui(S.replace(b' mNavigationLight=true\n',b''))['mNavigationLight'])
    def test_duplicate(self):self.assertEqual('unknown',f.systemui(S.replace(b' mNavigationLight=true',b' mNavigationLight=true mNavigationLight=false'))['mNavigationLight'])
    def test_duplicate_malformed(self):self.assertEqual('unknown',f.systemui(S.replace(b' mNavigationLight=true',b' mNavigationLight=true mNavigationLight=garbage'))['mNavigationLight'])
    def test_malformed_bool(self):self.assertEqual('unknown',f.systemui(S.replace(b'mNavigationLight=true',b'mNavigationLight=trueevil'))['mNavigationLight'])
    def test_intensity_bounds(self):
        for bad in (b'1.1',b'-0.1',b'nan',b'0.5evil'):
            self.assertEqual('unknown',f.systemui(S.replace(b'mDarkIntensity=1.0',b'mDarkIntensity='+bad))['mDarkIntensity'])
    def test_unknown_flag(self):self.assertEqual('unknown',f.systemui(S.replace(b'LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS',b'NEW_FLAG'))['effectiveLightNav'])
    def test_no_light(self):self.assertEqual('false',f.systemui(S.replace(b'LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS',b'LIGHT_STATUS_BARS'))['effectiveLightNav'])
    def test_truncated(self):self.assertEqual('unknown',f.systemui(S[:S.index(b' mNavigationLight')])['mNavigationLight'])
    def test_missing_context(self):
        for method,raw in ((f.systemui,b'mNavigationLight=true'),(f.policy,b'DisplayPolicy\n mLastAppearance=LIGHT_NAVIGATION_BARS')):
            with self.assertRaises(ValueError):method(raw)
    def test_duplicate_context(self):
        with self.assertRaises(ValueError):f.systemui(S+S)
        with self.assertRaises(ValueError):f.policy(W+W)
    def test_display_scope(self):self.assertEqual('true',f.policy(W)['lastEffectiveLightNav'])
    def test_policy_scope(self):self.assertEqual('unknown',f.policy(W.replace(b'   mLastAppearance=LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS\n',b''))['lastEffectiveLightNav'])
    def test_zero_omission_unknown(self):self.assertEqual('unknown',f.policy(W.replace(b'   mLastAppearance=LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS\n',b''))['lastEffectiveLightNav'])
    def test_title_never_escapes(self):
        raw=W.replace(b'com.frpboy.scrobblix/dev.scrobblix.app.MainActivity',b'PRIVATE TITLE user@example.invalid')
        out=json.dumps(f.policy(raw));self.assertNotIn('PRIVATE',out);self.assertEqual('other',f.policy(raw)['navColorCandidate'])
    def test_lookalike_title(self):self.assertEqual('other',f.policy(W.replace(b'MainActivity}',b'MainActivity.evil}'))['navColorCandidate'])
    def test_absent_not_inferred(self):self.assertEqual('unknown',f.policy(W.replace(b'   mNavBarColorWindowCandidate=Window{abcdef u0 com.frpboy.scrobblix/dev.scrobblix.app.MainActivity}\n',b''))['navColorCandidate'])
    def test_limits(self):
        for raw in (b'x'*(f.MAX_BYTES+1),b'x'*(f.MAX_LINE+1)):
            with self.assertRaises(ValueError):f.systemui(raw)
    def test_private_ignored(self):self.assertNotIn('private-unrelated',json.dumps(f.systemui(S)))
    def test_app_schema(self):
        state='resumed=true focused=true decor=0,0,100,200 visible=0,0,100,200 display=100,200 nav=0,0,0,48 navVisible=true statusLight=true navLight=true contrast=true'
        raw=('INSTRUMENTATION_STATUS: stream=BARS_APP host=main phase=before '+state+'\nINSTRUMENTATION_STATUS: stream=BARS_APP host=main phase=after '+state+'\nOK (1 test)\n').encode()
        self.assertEqual(2,len(p.app_records(raw,'main')))
        for bad in (raw.replace(b'focused=true',b'focused=evil',1),raw.replace(b'OK (1 test)',b'FAILURES'),raw+b'BARS_APP PRIVATE\n'):
            with self.assertRaises(ValueError):p.app_records(bad,'main')
    def test_bounded_command(self):
        self.assertEqual(b'ok\n',p.bounded(['python3','-c','print("ok")']))
        with self.assertRaises(ValueError):p.bounded(['python3','-c','print("x"*1000)'],limit=10)
        with self.assertRaises(ValueError):p.bounded(['python3','-c','import time;time.sleep(1)'],seconds=.01)
    def test_six_transport_roundtrip_and_missing(self):
        with tempfile.TemporaryDirectory()as t:
            root=pathlib.Path(t);src=root/'src';src.mkdir()
            for n in px.pixels.NAMES:(src/n).write_bytes(b'\x89PNG\r\n\x1a\n' + b'synthetic')
            text=px.encode(src);self.assertEqual(6,len(px.decode(text,root/'out')))
            with self.assertRaises(ValueError):px.decode(text[:text.rfind('SCROBBLIX_ABOUT_PIXEL_V1 ')],root/'no')
if __name__=='__main__':unittest.main()
