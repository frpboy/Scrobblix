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
# Pin the verifier output contract used by the exact signer assertion below.
# Build-tools 37 changes signer labels; never auto-select a different parser schema.
build_tools="$ANDROID_HOME/build-tools/36.0.0"
test -x "$build_tools/apksigner" && test -x "$build_tools/aapt"
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
# Use native guest geometry; measure physical pixels and bind XML before gestures.
[[ -z $(adb shell pm path com.frpboy.scrobblix | tr -d '\r') ]]
adb install "$apk" > "$private/install.txt"
grep -Fx Success "$private/install.txt"
adb shell am force-stop com.frpboy.scrobblix
adb shell am start -W -n com.frpboy.scrobblix/dev.scrobblix.app.MainActivity > "$private/start.txt"
grep -F 'Status: ok' "$private/start.txt"
pid=$(adb shell pidof com.frpboy.scrobblix | tr -d '\r'); [[ $pid =~ ^[0-9]+$ ]]
sleep 10
[[ $(adb shell pidof com.frpboy.scrobblix | tr -d '\r') = "$pid" ]]
python qa/alpha1-install/qualify.py "$evidence" "$pid" "$api"
adb shell am force-stop com.frpboy.scrobblix
[[ -z $(adb shell pidof com.frpboy.scrobblix | tr -d '\r') ]]
kill "$emulator_pid"; wait "$emulator_pid" || true
emulator_pid=''
python qa/alpha1-install/result.py "$evidence" "$api"
