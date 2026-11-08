import importlib.util,pathlib,tempfile,unittest,os
s=importlib.util.spec_from_file_location('pixels','scripts/about-pixel-log.py');p=importlib.util.module_from_spec(s);s.loader.exec_module(p)
class Boundaries(unittest.TestCase):
 def setUp(self):
  self.t=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.t.name);self.src=self.root/'input';self.src.mkdir()
  for n in p.NAMES:(self.src/n).write_bytes(b'\x89PNG\r\n\x1a\nfixture')
 def tearDown(self):self.t.cleanup()
 def reject(self):
  with self.assertRaises(ValueError):p.encode(self.src)
 def test_oversize_bounded_read(self):
  with (self.src/p.NAMES[0]).open('wb')as f:f.truncate(p.MAX_FILE*100)
  self.reject()
 def test_fifo_rejected_before_open(self):
  f=self.src/p.NAMES[0];f.unlink();os.mkfifo(f);self.reject()
 def test_directory_rejected(self):
  f=self.src/p.NAMES[0];f.unlink();f.mkdir();self.reject()
 def test_file_symlink(self):
  f=self.src/p.NAMES[0];f.unlink();f.symlink_to(self.src/p.NAMES[1]);self.reject()
 def test_root_symlink(self):
  alias=self.root/'alias';alias.symlink_to(self.src,target_is_directory=True)
  with self.assertRaises(ValueError):p.encode(alias)
 def test_root_regular(self):
  with self.assertRaises(ValueError):p.encode(self.src/p.NAMES[0])
 def test_decoder_input_limit(self):
  with self.assertRaises(ValueError):p.decode('x'*(p.MAX_LOG+1),self.root/'output')
  self.assertFalse((self.root/'output').exists())
if __name__=='__main__':unittest.main()
