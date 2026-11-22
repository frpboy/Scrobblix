#!/usr/bin/env bash
# Capture real installed-app screenshots on a booted emulator (CI lane).
# Requires: adb in PATH, emulator booted, debug APK built.
set -u
OUT=${1:-shots}
APK=${2:-android/app/build/outputs/apk/debug/app-debug.apk}
PKG=com.frpboy.scrobblix
mkdir -p "$OUT"

adb uninstall com.frpboy.scrobblix >/dev/null 2>&1 || true
adb install "$APK" || exit 1
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

# Listen tab: genuine scrolled state so the tracking controls below the fold are visible
adb shell input swipe 540 1500 540 700 400
sleep 2
adb exec-out screencap -p > "$OUT/03-app-listen-scrolled.png"
# Scroll back to the top: the tab chips live inside the scrollable content, so the
# tab taps below need the top of the page restored.
adb shell input swipe 540 700 540 1500 400
sleep 2

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
  # ensure the tab chips are in view before tapping
  adb shell input swipe 540 700 540 1500 400
  sleep 1
  tap_on_text "$TAB" || true
  adb exec-out screencap -p > "$OUT/04-tab-${TAB}.png"
  if [ "$TAB" = "Settings" ]; then
    # Settings tab: genuine scrolled state so the Last.fm connection controls are visible
    adb shell input swipe 540 1500 540 700 400
    sleep 2
    adb exec-out screencap -p > "$OUT/04-tab-Settings-scrolled.png"
  fi
done

# System app-info page: launcher-level proof of installed label/icon/version
adb shell am start -a android.settings.APPLICATION_DETAILS_SETTINGS -d package:com.frpboy.scrobblix >/dev/null
sleep 4
adb exec-out screencap -p > "$OUT/05-app-info.png"

# App drawer via dock icon tap (AOSP launcher: center dock icon)
adb shell input keyevent KEYCODE_HOME
sleep 1
adb shell input tap 360 1210
sleep 2
adb exec-out screencap -p > "$OUT/06-app-drawer2.png"

ls -la "$OUT"
