"""Only a fixed typed schema and owned-window PNGs can leave the fresh guest."""
import hashlib, json, os, pathlib, shutil
root = pathlib.Path(os.environ['RUNNER_TEMP'])
src = root/'scrobblix-local-core-evidence'
dest = root/'local-core-public-evidence'
buffers={}
try:
    result=json.loads((src/'result.json').read_text(encoding='utf-8-sig'))
except (OSError, ValueError):
    result={'status':'FAIL','phase':'before_probe_receipt'}
allowed_phases={'firewall','baseline','native_playing_to_eligible','actual_playing_ui','native_paused',
                'actual_history_ui','package_identity_after_runtime','cleanup','before_probe_receipt','evidence_filter'}
try:
    if result.get('status') != 'ASSERTIONS_PASS_VISUAL_REVIEW_PENDING':
        phase=result.get('phase')
        safe={'status':'FAIL','phase':phase if phase in allowed_phases else 'before_probe_receipt'}
        buffers['result.json']=(json.dumps(safe,indent=2)+'\n').encode()
    else:
        # No raw DB/config/log/UIA tree/desktop/driver stdout is copied, including on failures.
        expected={'status','source_commit','package_sha256','package_bytes','app_sha256','driver_sha256',
                  'probe_head','run_id','run_attempt','playback_wall_seconds','scope','account_count',
                  'provider_action_count','egress','cleanup','not_proven'}
        assert set(result)==expected
        assert result['source_commit']=='da37da0caa8edf5653a9b02483a4e37d739179cf'
        assert result['package_sha256']=='5191988d22cd2c9c6ee8c6942dec43dd85d1882115790ef4123d8f06de604afc'
        assert result['package_bytes']==136124914
        assert result['account_count']==0 and result['provider_action_count']==0
        assert result['run_attempt']=='1'
        for key in ('app_sha256','driver_sha256','probe_head'):
            v=result[key]; assert isinstance(v,str) and len(v)==(40 if key=='probe_head' else 64)
            assert all(c in '0123456789abcdef' for c in v)
        assert str(result['run_id']).isdigit()
        assert isinstance(result['playback_wall_seconds'],(float,int)) and 45<=result['playback_wall_seconds']<=140
        assert result['scope']=='ordinary app synthetic local GSMTC capture/history/eligibility/pause only'
        assert result['egress']=='guest firewall application outbound block'
        assert result['cleanup']=='owned processes stopped; guest-only rule removed; no graceful-close/soak claim'
        assert result['not_proven']==['provider delivery or acknowledgement','installed browser extension path',
                                     'all real players','full beta readiness']
        buffers['result.json']=(json.dumps(result,indent=2)+'\n').encode()
        state_keys={'accounts','actions','plays','observations','unexpected_observations','synthetic_play_rows',
                    'playing_observations','paused_observations','latest_state','duration_ms','confirmed_ms',
                    'threshold_ms','eligible'}
        for name in ('baseline.json','eligible.json','paused.json','stable.json'):
            state=json.loads((src/name).read_text(encoding='utf-8-sig'))
            assert set(state)<=state_keys and all(v is None or type(v) is int for v in state.values())
            assert state['accounts']==state['actions']==state['unexpected_observations']==0
            buffers[name]=(json.dumps(state,sort_keys=True,indent=2)+'\n').encode()
        for name in ('ordinary-playing.png','ordinary-history.png'):
            path=src/name
            assert 100 < path.stat().st_size <= 5_000_000
            assert path.open('rb').read(8)==b'\x89PNG\r\n\x1a\n'
            buffers[name]=path.read_bytes()
except (AssertionError, OSError, ValueError, TypeError, KeyError):
    buffers={'result.json':b'{"status":"FAIL","phase":"evidence_filter"}\n'}

# Exact local provenance remains available after branch/artifact expiry.
buffers['closure-manifest.json']=pathlib.Path(__file__).with_name('closure-manifest.json').read_bytes()
dest.mkdir(exist_ok=False)
for name,data in buffers.items():
    (dest/name).write_bytes(data)
hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(dest.iterdir()) if p.is_file()}
(dest/'evidence-sha256.json').write_text(json.dumps(hashes,sort_keys=True,indent=2)+'\n')
print('fixed evidence allowlist prepared; actual screenshots require visual review')
