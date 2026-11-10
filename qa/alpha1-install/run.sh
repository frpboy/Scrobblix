#!/usr/bin/env bash
# Fresh disposable guest only. No accounts, keys, auth, notification grants or app data reset.
set -euo pipefail
api=${1:?API}; [[ $api = 26 || $api = 36 ]]
[[ $GITHUB_ACTIONS = true && $RUNNER_OS = Linux && $GITHUB_RUN_ATTEMPT = 1 ]]
unset SCROBBLIX_SIGNING_STORE SCROBBLIX_SIGNING_PASSWORD_FILE
export JAVA_HOME="$JAVA_HOME_17_X64"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$ANDROID_HOME/cmdline-tools/latest/bin:$PATH"
apk="$RUNNER_TEMP/alpha1-exact.apk"
evidence="$RUNNER_TEMP/alpha1-evidence"
private="$RUNNER_TEMP/alpha1-private"
mkdir -m 700 "$private" "$evidence"
emulator_pid=''
cleanup() {
  if [[ -n $emulator_pid ]]; then
    adb shell am force-stop com.frpboy.scrobblix >/dev/null 2>&1 || true
    kill "$emulator_pid" 2>/dev/null || true
    wait "$emulator_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
build_tools=$(find "$ANDROID_HOME/build-tools" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -1)
"$build_tools/apksigner" verify --verbose --print-certs "$apk" > "$private/signature.txt"
"$build_tools/aapt" dump badging "$apk" > "$private/package.txt"
python - "$private" <<'PY'
import pathlib,re,sys
p=pathlib.Path(sys.argv[1]);s=(p/'signature.txt').read_text()
assert re.findall(r'^Signer #\d+ certificate SHA-256 digest: (\w+)$',s,re.M)==['a9b37328df9b9c166ceb41247cf9fd394427676832ab391c10f7494a711f7cde']
assert "package: name='com.frpboy.scrobblix' versionCode='6' versionName='0.1.0-alpha.1'" in (p/'package.txt').read_text()
PY
export ANDROID_USER_HOME="$private/android"
export ANDROID_EMULATOR_HOME="$ANDROID_USER_HOME"
export ANDROID_AVD_HOME="$ANDROID_USER_HOME/avd"
mkdir -p "$ANDROID_AVD_HOME"
test -e /dev/kvm
sudo chmod a+rw /dev/kvm
sdkmanager 'emulator' "system-images;android-$api;default;x86_64" >/dev/null
echo no | avdmanager create avd -n alpha1-exact -k "system-images;android-$api;default;x86_64" --path "$ANDROID_AVD_HOME/alpha1-exact.avd" --force >/dev/null
emulator -avd alpha1-exact -no-window -no-audio -no-snapshot -gpu swiftshader_indirect -no-boot-anim > "$private/emulator.log" 2>&1 &
emulator_pid=$!
timeout 180 adb wait-for-device
booted=false
for attempt in $(seq 1 120); do
  if [[ $(adb shell getprop sys.boot_completed | tr -d '\r') = 1 ]]; then booted=true; break; fi
  sleep 2
done
[[ $booted = true ]]
adb root >/dev/null; adb wait-for-device
# Mandatory IPv4/IPv6 guest OUTPUT blocks BEFORE the APK is installed or launched.
adb shell iptables -w 30 -I OUTPUT 1 ! -o lo -j REJECT
adb shell ip6tables -w 30 -I OUTPUT 1 ! -o lo -j REJECT
adb shell iptables -w 30 -C OUTPUT ! -o lo -j REJECT
adb shell ip6tables -w 30 -C OUTPUT ! -o lo -j REJECT
# Fixed 480x960 logical viewport makes the untouched opt-in switch visible.
adb shell wm size 960x1920
adb shell wm density 320
[[ -z $(adb shell pm path com.frpboy.scrobblix | tr -d '\r') ]]
adb install "$apk" > "$private/install.txt"
grep -Fx Success "$private/install.txt"
adb shell am force-stop com.frpboy.scrobblix
adb shell am start -W -n com.frpboy.scrobblix/dev.scrobblix.app.MainActivity > "$private/start.txt"
grep -F 'Status: ok' "$private/start.txt"
pid=$(adb shell pidof com.frpboy.scrobblix | tr -d '\r'); [[ $pid =~ ^[0-9]+$ ]]
sleep 10
[[ $(adb shell pidof com.frpboy.scrobblix | tr -d '\r') = "$pid" ]]
adb shell uiautomator dump /sdcard/window.xml >/dev/null
adb pull /sdcard/window.xml "$private/window.xml" >/dev/null
python - "$private/window.xml" <<'PY'
import xml.etree.ElementTree as E,sys
nodes=list(E.parse(sys.argv[1]).iter('node'))
texts={n.get('text') for n in nodes if n.get('package')=='com.frpboy.scrobblix'}
assert {'Scrobblix','Tracking is paused','Media access is off'}<=texts
assert not any('Last.fm connected:' in (t or '') for t in texts)
switches=[n for n in nodes if n.get('package')=='com.frpboy.scrobblix' and n.get('class')=='android.widget.Switch']
assert len(switches)==1 and switches[0].get('checked')=='false'
PY
# Only fresh app pixels, not XML/logs/private directories, leave the disposable guest.
adb exec-out screencap -p > "$evidence/actual-app.png"
adb shell iptables -w 30 -C OUTPUT ! -o lo -j REJECT
adb shell ip6tables -w 30 -C OUTPUT ! -o lo -j REJECT
adb shell am force-stop com.frpboy.scrobblix
[[ -z $(adb shell pidof com.frpboy.scrobblix | tr -d '\r') ]]
kill "$emulator_pid"; wait "$emulator_pid" || true
emulator_pid=''
python - "$evidence" "$api" <<'PY'
import hashlib,json,pathlib,sys,os
p=pathlib.Path(sys.argv[1]);png=p/'actual-app.png';assert png.read_bytes().startswith(b'\x89PNG\r\n\x1a\n') and 100<png.stat().st_size<5000000
r={'status':'ASSERTIONS_PASS_VISUAL_REVIEW_PENDING','api':int(sys.argv[2]),'source_commit':'acf3d1bfc7c395bedff3a854b05e1b6be870b50e','probe_ref':os.environ['GITHUB_SHA'],'run_id':os.environ['GITHUB_RUN_ID'],'run_attempt':1,'apk_sha256':'8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc','signer_sha256':'a9b37328df9b9c166ceb41247cf9fd394427676832ab391c10f7494a711f7cde','package':'com.frpboy.scrobblix','version_code':6,'fresh_install':True,'cold_start_alive_seconds':10,'tracking_enabled':False,'media_access_enabled':False,'account_connected':False,'egress':'guest IPv4 and IPv6 OUTPUT reject before install and after capture','cleanup':'owned app force-stopped and emulator stopped; no data reset/uninstall','not_proven':['owner-device update or data preservation','provider authorization or delivery','real player/background listening','full beta readiness']}
(p/'result.json').write_text(json.dumps(r,indent=2)+'\n');(p/'sha256.json').write_text(json.dumps({n.name:hashlib.sha256(n.read_bytes()).hexdigest() for n in p.iterdir() if n.is_file()},indent=2)+'\n')
PY
