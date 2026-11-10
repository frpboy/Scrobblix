#!/usr/bin/env bash
# LOCAL REVIEW CANDIDATE. Inputs already decrypted into a private directory by a separately reviewed transport.
# Never run on a user device. No owner key, token, provider traffic, uninstall or reset.
set -euo pipefail
api=${1:?API26 or36}
[[ $api = 26 || $api = 36 ]]
[[ ${GITHUB_ACTIONS:-} = true && ${RUNNER_OS:-} = Linux && ${GITHUB_RUN_ATTEMPT:-} = 1 ]]
private=${RUNNER_TEMP:?}/alpha2-private
evidence=$RUNNER_TEMP/alpha2-evidence
[[ -d $private && -f $private/baseline.apk && -f $private/candidate.apk && -f $private/test.apk ]]
mkdir -m700 "$evidence"
# Manifest/static identity gate is mandatory and must be supplied in reviewed closure packet.
python qa/alpha2-update/verify-artifacts.py "$private" qa/alpha2-update/artifacts.json
export JAVA_HOME="$JAVA_HOME_17_X64"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$ANDROID_HOME/cmdline-tools/latest/bin:$PATH"
export ANDROID_USER_HOME="$private/android" ANDROID_EMULATOR_HOME="$private/android" ANDROID_AVD_HOME="$private/android/avd"
mkdir -p "$ANDROID_AVD_HOME"
emulator_pid=''
cleanup() {
  status=$?
  set +e
  python qa/alpha2-update/failure-diagnostics.py "$private" "$evidence" "$api" "$status"
  if [[ $status != 0 ]]; then
    # A partial run must not retain any success-shaped receipt or pixels.
    rm -f "$evidence/api${api}-synthetic-update.json" "$evidence/api${api}-candidate.png" "$evidence/api${api}-candidate.xml" "$evidence/api${api}-capture.json"
  fi
  if [[ -n $emulator_pid ]]; then
    timeout 10 adb shell am force-stop com.frpboy.scrobblix >/dev/null 2>&1
    kill "$emulator_pid"
    wait "$emulator_pid"
  fi
  rm -f "$private/baseline.apk" "$private/candidate.apk" "$private/test.apk"
  exit "$status"
}
trap cleanup EXIT
test -e /dev/kvm
sudo chmod a+rw /dev/kvm
sdkmanager 'emulator' "system-images;android-$api;default;x86_64" >/dev/null
echo no | avdmanager create avd -n alpha2-exact -k "system-images;android-$api;default;x86_64" --path "$ANDROID_AVD_HOME/alpha2-exact.avd" --force >/dev/null
emulator -avd alpha2-exact -no-window -no-audio -no-snapshot -gpu swiftshader_indirect -no-boot-anim > "$private/emulator.log" 2>&1 &
emulator_pid=$!
timeout 180 adb wait-for-device
booted=false
for attempt in $(seq 1 120); do
  if [[ $(adb shell getprop sys.boot_completed | tr -d '\r') = 1 ]]; then booted=true; break; fi
  sleep 2
done
[[ $booted = true ]]
adb root
adb wait-for-device
test "$(timeout 15 adb shell id -u | tr -d '\r')" = 0
fence_check() {
  adb shell iptables -w 30 -C OUTPUT ! -o lo -j REJECT
  adb shell ip6tables -w 30 -C OUTPUT ! -o lo -j REJECT
}
adb shell iptables -w 30 -I OUTPUT 1 ! -o lo -j REJECT
adb shell ip6tables -w 30 -I OUTPUT 1 ! -o lo -j REJECT
fence_check
[[ -z $(adb shell pm path com.frpboy.scrobblix | tr -d '\r') ]]
adb install "$private/baseline.apk" > "$private/install-baseline.txt"
grep -Fx Success "$private/install-baseline.txt"
adb install "$private/test.apk" > "$private/install-test.txt"
grep -Fx Success "$private/install-test.txt"
# Exact UID extraction parser and firstInstallTime parser required in reviewed source, not guessed substring.
python qa/alpha2-update/package-state.py > "$private/baseline-state.json"
phase() {
  fence_check
  result=$(adb shell am instrument -w -r -e exactReleasePair canonical-alpha1-to-alpha2-isolated -e exactReleasePhase "$1" -e class dev.scrobblix.app.ExactReleasePairTest com.frpboy.scrobblix.test/androidx.test.runner.AndroidJUnitRunner)
  printf '%s\n' "$result"
  printf '%s\n' "$result" | grep -F 'OK (1 test)'
  printf '%s\n' "$result" | grep -Fx 'INSTRUMENTATION_CODE: -1'
  if printf '%s\n' "$result" | grep -E 'FAILURES|INSTRUMENTATION_FAILED|Process crashed|INSTRUMENTATION_STATUS_CODE: -[12]'; then exit 1; fi
}
phase prepare
adb shell am force-stop com.frpboy.scrobblix
fence_check
adb install -r "$private/candidate.apk" > "$private/install-update.txt"
grep -Fx Success "$private/install-update.txt"
python qa/alpha2-update/package-state.py > "$private/candidate-state.json"
python qa/alpha2-update/compare-package-state.py "$private/baseline-state.json" "$private/candidate-state.json"
phase verify
python qa/alpha2-update/write-receipt.py "$private" "$evidence" "$api"
fence_check
adb shell am start -W -n com.frpboy.scrobblix/dev.scrobblix.app.MainActivity > "$private/start.txt"
grep -F 'Status: ok' "$private/start.txt"
# Native geometry and exact foreground component before/after each adjacent pixel/XML capture.
# Capture validator must be supplied and independently reviewed with parsers and closure manifest.
python qa/alpha2-update/capture.py "$evidence" "$api"
fence_check
printf '%s\n' 'PASS exact canonical release-pair install-r; synthetic retention only. No user-device or live-provider proof.'
