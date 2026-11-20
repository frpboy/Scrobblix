#!/usr/bin/env bash
# Capture real installed-app screenshots on a booted emulator (CI lane).
# Requires: adb in PATH, emulator booted, debug APK built.
set -u
OUT=${1:-shots}
APK=${2:-android/app/build/outputs/apk/debug/app-debug.apk}
PKG=com.frpboy.scrobblix
mkdir -p "$OUT"

adb install -r "$APK" || exit 1
adb shell input keyevent KEYCODE_WAKEUP
adb shell input keyevent KEYCODE_HOME
sleep 2
adb exec-out screencap -p > "$OUT/01-home.png"

# App drawer (swipe up from bottom of home screen)
adb shell input swipe 540 1900 540 600 400
sleep 2
adb exec-out screencap -p > "$OUT/02-app-drawer.png"
adb shell input keyevent KEYCODE_HOME
sleep 1

# Launch the app
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null
sleep 15
adb exec-out screencap -p > "$OUT/03-app-listen.png"

tap_on_text() {
  local label="$1" coords
  adb shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1
  adb pull /sdcard/ui.xml /tmp/ui.xml >/dev/null 2>&1
  coords=$(python3 - "$label" <<'PYEOF'
import re, sys
label = sys.argv[1]
xml = open('/tmp/ui.xml', encoding='utf-8', errors='ignore').read()
pats = [
    r'text="' + re.escape(label) + r'"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"',
    r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"[^>]*text="' + re.escape(label) + r'"',
]
for p in pats:
    m = re.search(p, xml)
    if m:
        x = (int(m.group(1)) + int(m.group(3))) // 2
        y = (int(m.group(2)) + int(m.group(4))) // 2
        print(x, y)
        break
PYEOF
)
  if [ -n "$coords" ]; then
    echo "tap '$label' at $coords"
    adb shell input tap $coords
    sleep 4
    return 0
  fi
  echo "WARN: '$label' not found on screen"
  return 1
}

for TAB in History Queue Settings; do
  tap_on_text "$TAB" || true
  adb exec-out screencap -p > "$OUT/04-tab-${TAB}.png"
done

ls -la "$OUT"
