"""Drive the real Inno wizard on a disposable Windows runner only."""
import ctypes as c
from ctypes import wintypes as w
import json, os, pathlib, subprocess, sys, time

if os.environ.get('GITHUB_ACTIONS') != 'true' or os.environ.get('RUNNER_OS') != 'Windows':
    raise SystemExit('Disposable Windows CI only')
exe, output = pathlib.Path(sys.argv[1]).resolve(), pathlib.Path(sys.argv[2]).resolve()
output.mkdir(exist_ok=True, parents=True)
u = c.WinDLL('user32', use_last_error=True)
u.SendMessageW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
u.SendMessageW.restype = w.LPARAM
u.IsWindowVisible.argtypes = [w.HWND]
u.IsWindowEnabled.argtypes = [w.HWND]
u.GetWindowThreadProcessId.argtypes = [w.HWND, c.POINTER(w.DWORD)]
u.SetForegroundWindow.argtypes = [w.HWND]
CALLBACK = c.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
u.EnumWindows.argtypes = [CALLBACK, w.LPARAM]
u.EnumChildWindows.argtypes = [w.HWND, CALLBACK, w.LPARAM]
u.GetWindowTextW.argtypes = [w.HWND, w.LPWSTR, c.c_int]
u.GetClassNameW.argtypes = [w.HWND, w.LPWSTR, c.c_int]
class RECT(c.Structure):
    _fields_ = [('left', c.c_long), ('top', c.c_long), ('right', c.c_long), ('bottom', c.c_long)]
u.GetWindowRect.argtypes = [w.HWND, c.POINTER(RECT)]
def text(h):
    b = c.create_unicode_buffer(2048); u.GetWindowTextW(h, b, 2048); return b.value

def cls(h):
    b = c.create_unicode_buffer(256); u.GetClassNameW(h, b, 256); return b.value

def windows(parent=None):
    result = []
    @CALLBACK
    def collect(h, _):
        if u.IsWindowVisible(h): result.append(h)
        return True
    (u.EnumChildWindows(parent, collect, 0) if parent else u.EnumWindows(collect, 0))
    return result

def capture(name):
    # System.Drawing is already used by the owning installer qualification.
    script = "Add-Type -AssemblyName System.Windows.Forms;Add-Type -AssemblyName System.Drawing;$r=[Windows.Forms.Screen]::PrimaryScreen.Bounds;$b=New-Object Drawing.Bitmap($r.Width,$r.Height);$g=[Drawing.Graphics]::FromImage($b);$g.CopyFromScreen($r.Left,$r.Top,0,0,$b.Size);$b.Save('" + str(output / name).replace("'", "''") + "');$g.Dispose();$b.Dispose()"
    subprocess.run(['pwsh', '-NoProfile', '-Command', script], check=True, capture_output=True)

def desktop_processes():
    result = subprocess.run(['pwsh', '-NoProfile', '-Command', "@(Get-Process Scrobblix.Desktop -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id) | ConvertTo-Json -Compress"], capture_output=True, text=True, check=True)
    value = json.loads(result.stdout or '[]'); return value if isinstance(value, list) else [value]

results = []
for run_app, visit in [(True, False), (False, False), (False, True), (True, True)]:
    label = f'finish-run{int(run_app)}-github{int(visit)}'
    if desktop_processes(): raise RuntimeError('Unexpected app already running before wizard')
    log = output / (label + '.log')
    p = subprocess.Popen([str(exe), '/SP-', '/NORESTART', '/LOG=' + str(log)])
    finish = None; deadline = time.monotonic() + 180
    while time.monotonic() < deadline:
        tops = [h for h in windows() if text(h).startswith('Setup - Scrobblix')]
        if not tops:
            if p.poll() is not None: raise RuntimeError('Wizard exited before Finish')
            time.sleep(.3); continue
        h = tops[0]; u.SetForegroundWindow(h)
        controls = windows(h)
        for b in controls:
            if text(b).replace('&', '').strip().lower() == 'i accept the agreement' and u.IsWindowEnabled(b):
                u.SendMessageW(b, 0xF5, 0, 0)
        buttons = [b for b in controls if cls(b).endswith('Button') and u.IsWindowEnabled(b)]
        finish = next((b for b in buttons if text(b).replace('&', '') == 'Finish'), None)
        if finish: break
        advance = next((b for b in buttons if text(b).replace('&', '') in ('Next', 'Next >', 'Install')), None)
        if advance: u.SendMessageW(advance, 0xF5, 0, 0)
        time.sleep(.5)
    if not finish:
        capture(label + '-timeout.png')
        (output / (label + '-controls.json')).write_text(json.dumps([{'title': text(t), 'class': cls(t), 'children': [{'text':text(b), 'class':cls(b), 'enabled':bool(u.IsWindowEnabled(b))} for b in windows(t)]} for t in windows()], indent=2))
        raise RuntimeError('Real Finish page not reached')
    controls = windows(h)
    lists = [b for b in controls if cls(b) == 'TNewCheckListBox']
    if len(lists) != 1: raise RuntimeError('Finish choices control missing: ' + repr([(cls(b), text(b)) for b in controls]))
    choices = lists[0]
    if u.SendMessageW(choices, 0x18B, 0, 0) != 2: raise RuntimeError('Expected exactly two Finish choices')
    if run_app and not visit: capture('installer-finish-defaults.png')
    # Inno's native checklist is a Windows list box. Toggle via its ordinary keyboard path.
    for index, toggle in [(0, not run_app), (1, visit)]:
        if toggle:
            u.SendMessageW(choices, 0x186, index, 0)  # LB_SETCURSEL
            u.SendMessageW(choices, 0x100, 0x20, 0)  # WM_KEYDOWN VK_SPACE
            u.SendMessageW(choices, 0x101, 0x20, 0)
    capture(label + '-choices.png')
    u.SendMessageW(finish, 0xF5, 0, 0)
    p.wait(timeout=60)
    time.sleep(6)
    launched = desktop_processes()
    if bool(launched) != run_app: raise RuntimeError(label + ': app selection/launch mismatch')
    body = log.read_text(encoding='utf-8-sig')
    browser_launch = 'Filename: https://github.com/frpboy/Scrobblix' in body
    if browser_launch != visit: raise RuntimeError(label + ': browser selection/ShellExec log mismatch')
    if run_app: capture(label + '-launched.png')
    results.append({'run_app': run_app, 'visit_github': visit, 'app_process_observed': bool(launched), 'github_shell_launch_logged': browser_launch, 'setup_exit': p.returncode})
    for pid in launched:
        subprocess.run(['taskkill', '/PID', str(pid), '/F'], check=True, capture_output=True)
    time.sleep(1)
(output / 'finish-matrix.json').write_text(json.dumps(results, indent=2))
print('PASS actual normal Finish wizard, defaults, all four selections, app process and selected-only GitHub ShellExec; no claim of browser page load from shell launch alone')
