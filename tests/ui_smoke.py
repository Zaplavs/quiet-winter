"""Integration check against the real Windows desktop; run after build.ps1."""
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import subprocess
import time


ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'dist' / 'QuietWinter.exe'
ART = ROOT / 'artifacts'
REPORT = ART / 'ui-live.json'
CONFIG = ART / 'ui-preferences.json'
u = ctypes.windll.user32
u.SetProcessDPIAware()
u.GetForegroundWindow.restype = wintypes.HWND
u.WindowFromPoint.argtypes = [wintypes.POINT]
u.WindowFromPoint.restype = wintypes.HWND
u.SetForegroundWindow.argtypes = [wintypes.HWND]
u.GetWindowLongW.argtypes = [wintypes.HWND, ctypes.c_int]
u.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
u.ChildWindowFromPointEx.argtypes = [wintypes.HWND, wintypes.POINT, wintypes.UINT]
u.ChildWindowFromPointEx.restype = wintypes.HWND
u.SendMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
u.SendMessageW.restype = wintypes.LPARAM
u.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
u.FindWindowExW.argtypes = [wintypes.HWND, wintypes.HWND, wintypes.LPCWSTR, wintypes.LPCWSTR]
u.FindWindowExW.restype = wintypes.HWND
u.GetAncestor.argtypes = [wintypes.HWND, wintypes.UINT]
u.GetAncestor.restype = wintypes.HWND
CALLBACK = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)


def windows(pid):
    result = []
    @CALLBACK
    def visit(h, _):
        owner = wintypes.DWORD()
        u.GetWindowThreadProcessId(h, ctypes.byref(owner))
        if owner.value == pid:
            title = ctypes.create_unicode_buffer(256)
            u.GetWindowTextW(h, title, 256)
            if title.value in ('QuietWinter.Overlay', 'Тихая зима'):
                result.append((h, title.value, bool(u.IsWindowVisible(h))))
        return True
    u.EnumWindows(visit, 0)
    return result


def read():
    try:
        return json.loads(REPORT.read_text(encoding='utf-8'))
    except (OSError, json.JSONDecodeError):
        return {}


def wait_for(predicate, timeout=8):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        value = read()
        if predicate(value):
            return value
        if app.poll() is not None:
            raise AssertionError(f'App exited early: {app.returncode}')
        time.sleep(.12)
    raise AssertionError(f'Timeout. Last report: {read()}')


checks = []
def check(name, action):
    result = action()
    if result is not False:
        checks.append({'name': name, 'passed': True})


def require(condition, message):
    if not condition:
        raise AssertionError(message)


for path in (REPORT, CONFIG):
    path.unlink(missing_ok=True)
app = subprocess.Popen([str(EXE), '--settings', '--run-for', '90', '--config', str(CONFIG), '--diagnostics', str(REPORT)])
try:
    report = wait_for(lambda r: r.get('Frames', 0) > 10)
    own = windows(app.pid)
    overlay = next(h for h, title, visible in own if title == 'QuietWinter.Overlay')
    settings = next(h for h, title, visible in own if title == 'Тихая зима' and visible)
    u.SetForegroundWindow(settings)
    p = wintypes.POINT(0, 0)
    u.ClientToScreen(settings, ctypes.byref(p))
    hotkeys = u.FindWindowExW(wintypes.HWND(-3), None, None, 'QuietWinter.Hotkeys')
    require(bool(hotkeys), 'Message-only hotkey window missing')
    def activate(x, y, key=13):
        target = u.ChildWindowFromPointEx(settings, wintypes.POINT(x, y), 7)
        require(target and target != settings, f'Control missing at {x}, {y}')
        u.SendMessageW(target, 0x100, key, 0)
        u.SendMessageW(target, 0x101, key, 0)
        time.sleep(.1)

    def native_flags():
        d = report['Displays'][0]
        require(d['Layered'] and d['ClickThrough'] and d['NoActivate'], 'Missing native overlay flags')
        require(d['Width'] == u.GetSystemMetrics(0), 'Wrong physical screen size')
        point = wintypes.POINT(p.x + 470, p.y + 210)
        require(u.WindowFromPoint(point) != overlay, 'Overlay intercepts pointer hit testing')
        require(report.get('PauseHotkey') and report.get('QuitHotkey'), 'Global shortcuts could not register')
    check('Native alpha overlay, click through and no activation', native_flags)

    check('Snow density button receives keyboard activation', lambda: (
        activate(620, 210), wait_for(lambda r: r.get('Density') == 2 and r['Displays'][0]['Flakes'] >= 600)))
    check('Wind slider changes direction and persists', lambda: (
        activate(65, 307, 36), [activate(65, 307, 37) for _ in range(17)], wait_for(lambda r: r.get('Wind', 0) < -70)))
    check('Garland switch responds', lambda: (
        activate(338, 472, 32), wait_for(lambda r: r.get('Garland') is False)))
    check('Pause button hides all snow windows', lambda: (
        activate(620, 626), wait_for(lambda r: r.get('Paused') is True and all(d['Sleeping'] for d in r['Displays']))))
    check('Ctrl+Alt+S resumes animation', lambda: (
        u.PostMessageW(hotkeys, 0x312, 1, 0), wait_for(lambda r: r.get('Paused') is False and not r['Displays'][0]['Sleeping'])))
    check('Manual excavator clears real accumulated snow', lambda: (
        activate(160, 626), wait_for(lambda r: r['Displays'][0]['Cleared'] > 15)))
    check('Snow burst button starts a temporary flurry', lambda: (
        activate(380, 626), wait_for(lambda r: r['Displays'][0]['BurstRemaining'] > 1)))
    check('Winter friends can be toggled independently', lambda: (
        activate(708, 364, 32), wait_for(lambda r: r.get('WinterFriends') is False), activate(708, 364, 32)))
    check('Cursor broom can be disabled independently', lambda: (
        activate(708, 418, 32), wait_for(lambda r: r.get('CursorBroom') is False), activate(708, 418, 32)))
    check('New Year countdown can be disabled independently', lambda: (
        activate(708, 472, 32), wait_for(lambda r: r.get('Countdown') is False), activate(708, 472, 32)))

    # Restore the shipped visual defaults before capturing the settings window.
    activate(380, 210)
    activate(338, 472, 32)
    activate(380, 307, 36)
    wait_for(lambda r: r.get('Density') == 1 and r.get('Garland') is True)
    subprocess.run([str(EXE), '--render-settings', str(ART / 'settings.png')], check=True, timeout=8)

    def single_instance():
        second = subprocess.run([str(EXE), '--settings'], timeout=8)
        require(second.returncode == 0, 'Second instance failed')
        require(len([w for w in windows(app.pid) if w[1] == 'QuietWinter.Overlay']) == len(report['Displays']), 'Duplicate overlay created')
    check('Second launch opens the existing settings window', single_instance)

    def fullscreen():
        import tkinter as tk
        root = tk.Tk()
        observations = []
        try:
            root.title('QuietWinter fullscreen verification')
            root.attributes('-fullscreen', True)
            root.configure(bg='#142a38')
            root.attributes('-topmost', True)
            root.update()
            native_root = u.GetAncestor(root.winfo_id(), 2)
            u.SetForegroundWindow(native_root)
            root.focus_force()
            root.update()
            if u.GetForegroundWindow() != native_root:
                checks.append({'name': 'Real fullscreen focus', 'skipped': True, 'detail': 'Windows kept another application active; user input was not forced. Fullscreen geometry is covered by the self-test.'})
                return False
            until = time.monotonic() + 5
            paused = False
            while time.monotonic() < until:
                root.update()
                r = wintypes.RECT()
                fg = u.GetForegroundWindow()
                u.GetWindowRect(fg, ctypes.byref(r))
                observations.append({'foreground': fg, 'test': native_root, 'bounds': [r.left, r.top, r.right, r.bottom]})
                if read().get('Displays', [{}])[0].get('Sleeping') is True:
                    paused = True
                    break
                time.sleep(.08)
            (ART / 'fullscreen-observations.json').write_text(json.dumps(observations), encoding='utf-8')
            require(paused, f'Fullscreen did not pause. Last foreground observation: {observations[-1]}')
        finally:
            root.destroy()
        wait_for(lambda r: r['Displays'][0]['Sleeping'] is False)
    check('Fullscreen application pauses snow and restores it afterwards', fullscreen)

    preferences = json.loads(CONFIG.read_text(encoding='utf-8'))
    check('UI preferences saved to disk', lambda: require(preferences['Density'] == 1 and preferences['Garland'], 'Settings not persisted'))

    def stop_hotkey():
        u.PostMessageW(hotkeys, 0x312, 2, 0)
        require(app.wait(timeout=8) == 0, 'Quit hotkey did not exit cleanly')
        require(not windows(app.pid), 'Overlay window was left behind')
    check('Ctrl+Alt+Q exits cleanly and removes overlays', stop_hotkey)
except Exception as e:
    checks.append({'name': 'Integration failure', 'passed': False, 'detail': repr(e)})
    subprocess.run([str(EXE), '--render-settings', str(ART / 'ui-failure.png')], timeout=8)
    raise
finally:
    if app.poll() is None:
        subprocess.run([str(EXE), '--stop'], timeout=8)
        try:
            app.wait(timeout=5)
        except subprocess.TimeoutExpired:
            app.terminate()
    (ART / 'ui-smoke.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'checks': checks, 'exit_code': app.returncode}, ensure_ascii=False))
