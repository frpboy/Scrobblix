import importlib.util,pathlib,tempfile,unittest
s=importlib.util.spec_from_file_location('pixels','scripts/about-pixel-log.py');p=importlib.util.module_from_spec(s);s.loader.exec_module(p)
class Pixels(unittest.TestCase):
    def setUp(self):
        self.t=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.t.name);self.src=self.root/'input';self.src.mkdir()
        # Actual previously captured PNG, not a textual assertion standing in for pixels.
        self.original=pathlib.Path('/downloads/scrobblix-about-browser.png').read_bytes()
        for n in p.NAMES:(self.src/n).write_bytes(self.original)
        self.text=p.encode(self.src)
    def tearDown(self):self.t.cleanup()
    def test_roundtrip_timestamp_prefix(self):
        prefixed='\n'.join('2026-11-08T00:00:00Z '+l for l in self.text.splitlines());out=self.root/'output';self.assertEqual(len(p.decode(prefixed,out)),8)
        for n in p.NAMES:self.assertEqual((out/n).read_bytes(),self.original)
    def bad(self,text):
        with self.assertRaises((ValueError,KeyError)):p.decode(text,self.root/'bad')
        self.assertFalse((self.root/'bad').exists())
    def test_missing_chunk(self):self.bad('\n'.join(l for i,l in enumerate(self.text.splitlines())if i!=1))
    def test_duplicate(self):self.bad(self.text.splitlines()[0]+'\n'+self.text)
    def test_masked(self):self.bad(self.text.replace('iVBOR','***',1))
    def test_truncated(self):self.bad(self.text.rsplit('\n',2)[0])
    def test_unsafe_name(self):self.bad(self.text.replace(p.NAMES[0],'../secret',1))
    def test_oversize_encode(self):
        (self.src/p.NAMES[0]).write_bytes(b'\x89PNG\r\n\x1a\n'+b'x'*p.MAX_FILE)
        with self.assertRaises(ValueError):p.encode(self.src)
if __name__=='__main__':unittest.main()
