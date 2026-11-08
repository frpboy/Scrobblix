import importlib.util,pathlib,subprocess,tempfile,unittest
spec=importlib.util.spec_from_file_location('route',pathlib.Path(__file__).with_name('classify-docs-ci.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class Routes(unittest.TestCase):
    def test_paths(self):
        for raw in (b'README.md\0',b'docs/android/STATUS.md\0',b'README.md\0docs/a.md\0'):
            self.assertTrue(m.docs_paths(raw))
        for raw in (b'',b'\0',b'README.md',b'readme.md\0',b'docs/a.py\0',b'docs/../a.md\0',b'docs//a.md\0',b'./README.md\0',b'docs/a\\b.md\0',b'docs/a\n.md\0',b'docs/\xff.md\0',b'.github/workflows/ci.yml\0',b'scripts/validate-docs.mjs\0',b'README.md\0src/x.cs\0',b'README.md\0'*10001):
            self.assertFalse(m.docs_paths(raw),repr(raw[:80]))
    def test_git_history(self):
        with tempfile.TemporaryDirectory()as t:
            def git(*a):return subprocess.check_output(['git','-C',t,*a],stderr=subprocess.DEVNULL)
            git('init','-q');git('config','user.email','test@example.invalid');git('config','user.name','Test')
            root=pathlib.Path(t);(root/'README.md').write_text('base');git('add','.');git('commit','-qm','base');base=git('rev-parse','HEAD').decode().strip()
            (root/'README.md').write_text('docs');git('add','.');git('commit','-qm','docs');head=git('rev-parse','HEAD').decode().strip()
            push={'ref':'refs/heads/main','before':base,'after':head}
            self.assertTrue(m.classify('push',push,head,git))
            self.assertFalse(m.classify('workflow_dispatch',push,head,git))
            self.assertFalse(m.classify('workflow_call',push,head,git))
            for key,val in [('forced',True),('created',True),('deleted',True),('ref','refs/tags/v1'),('before','0'*40),('after',base)]:
                self.assertFalse(m.classify('push',{**push,key:val},head,git))
            self.assertFalse(m.classify('push',push,base,git))
            pr={'pull_request':{'base':{'ref':'main','sha':base},'head':{'sha':head}}}
            self.assertTrue(m.classify('pull_request',pr,head,git))
            self.assertFalse(m.classify('pull_request',{'pull_request':{}},head,git))
            # Deleted/renamed source is never hidden by rename detection.
            (root/'source.py').write_text('code');git('add','.');git('commit','-qm','source');code=git('rev-parse','HEAD').decode().strip()
            (root/'docs').mkdir();git('mv','source.py','docs/code.md');git('commit','-qm','rename');renamed=git('rev-parse','HEAD').decode().strip()
            self.assertFalse(m.classify('push',{'ref':'refs/heads/main','before':code,'after':renamed},renamed,git))
            # A changed Markdown symlink cannot select documentation validation.
            (root/'docs'/'link.md').symlink_to('../README.md');git('add','.');git('commit','-qm','link');link=git('rev-parse','HEAD').decode().strip()
            self.assertFalse(m.classify('push',{'ref':'refs/heads/main','before':renamed,'after':link},link,git))
            # Empty commit is full CI, not success through an empty allowed list.
            git('commit','--allow-empty','-qm','empty');empty=git('rev-parse','HEAD').decode().strip()
            self.assertFalse(m.classify('push',{'ref':'refs/heads/main','before':link,'after':empty},empty,git))
    def test_invalid_event_sha(self):
        for sha in ('','x'*40,'a'*39,None):self.assertFalse(m.classify('push',{},sha,lambda *a:b''))
if __name__=='__main__':unittest.main()
