#!/usr/bin/env bash
# Disposable current-V3 mechanism only. Never actual Test5 migration or owner-key proof.
set -euo pipefail
[[ "$API" = 26 || "$API" = 36 ]]
test -z "${SCROBBLIX_SIGNING_STORE:-}"; test -z "${SCROBBLIX_SIGNING_PASSWORD_FILE:-}"
export JAVA_HOME="$JAVA_HOME_17_X64"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$ANDROID_HOME/cmdline-tools/latest/bin:$PATH"
private=$(mktemp -d "$RUNNER_TEMP/narrow-private.XXXXXX")
chmod 700 "$private"
cause=setup
cleanup_state=no_guest
cleanup_guest() {
  if [[ -n "${emulator_pid:-}" ]]; then
    local pgid=$emulator_pid
    kill -TERM -- "-$pgid" 2>/dev/null || true
    for attempt in 1 2 3; do
      if ! kill -0 -- "-$pgid" 2>/dev/null; then break; fi
      sleep 1
    done
    kill -KILL -- "-$pgid" 2>/dev/null || true
    wait "$emulator_pid" 2>/dev/null || true
    unset emulator_pid
    # A killed orphan can remain as an init-owned zombie briefly; it cannot execute.
    local processes
    if ! processes=$(ps -eo pgid=,stat=); then cleanup_state=failed; return 1; fi
    local live
    if ! live=$(printf '%s\n' "$processes" | awk -v group="$pgid" '$1==group && $2 !~ /^Z/ {live=1} END {print live+0}'); then cleanup_state=failed; return 1; fi
    if [[ "$live" != 0 ]]; then cleanup_state=failed; return 1; fi
    cleanup_state=dead
  fi
}
cleanup() {
  local result=$?
  trap - EXIT TERM INT
  set +e
  # Remove the disposable private key before any guest or potentially large tree cleanup.
  rm -f "$private/test.p12"
  local key_result=$?
  cleanup_guest
  local guest_result=$?
  timeout --kill-after=1s 2s rm -rf "$private"
  local remove_result=$?
  if [[ "$key_result" != 0 || "$guest_result" != 0 || "$remove_result" != 0 ]]; then result=1; cleanup_state=failed; fi
  RESULT="$result" CAUSE="$cause" CLEANUP="$cleanup_state" python3 - <<'PYCLEAN'
import json,os
from pathlib import Path
root=Path('narrow-evidence');root.mkdir(exist_ok=True)
data=dict(source_tree=os.environ['EXPECTED_TREE'],api=os.environ['API'],attempt=os.environ.get('GITHUB_RUN_ATTEMPT','1'),status='pass' if os.environ['RESULT']=='0' else 'failed',first_cause='none' if os.environ['RESULT']=='0' else os.environ['CAUSE'],cleanup=os.environ['CLEANUP'],error_category='none' if os.environ['RESULT']=='0' else ('cleanup_failure' if os.environ['CLEANUP']=='failed' else 'bounded_command_nonzero'))
(root/'manifest.json').write_text(json.dumps(data,sort_keys=True)+'\n')
# Keep partial packets bounded, not arbitrary command output or private paths.
for p in root.iterdir():
    if p.is_file() and p.stat().st_size>1024*1024: p.write_text('bounded-output-exceeded\n')
PYCLEAN
  exit "$result"
}
trap cleanup EXIT
trap 'exit 143' TERM
trap 'exit 130' INT
mkdir narrow-evidence
printf '%s\n' "$EXPECTED_TREE" > narrow-evidence/source.txt
cause=dependency_online
# Build ALL debug, instrumentation and release closures before any guest exists.
timeout --kill-after=10s 420s android/gradlew -p android -I qa/resolve-qualification.init.gradle :core:compileTestKotlin :app:assembleDebug :app:assembleDebugAndroidTest :app:assembleRelease resolveQualificationRuntimeInputs --no-daemon > "$private/online.log" 2>&1
cause=dependency_offline
timeout --kill-after=10s 60s android/gradlew -p android --offline -I qa/resolve-qualification.init.gradle :app:assembleDebug :app:assembleDebugAndroidTest :app:assembleRelease resolveQualificationRuntimeInputs --no-daemon > "$private/offline.log" 2>&1
for mode in online offline; do grep '^QUALIFICATION_RUNTIME_INPUT ' "$private/$mode.log" | sort > "narrow-evidence/inputs-$mode.sha256"; done
test -s narrow-evidence/inputs-online.sha256
cmp narrow-evidence/inputs-online.sha256 narrow-evidence/inputs-offline.sha256
cp android/app/build/outputs/apk/debug/app-debug.apk "$private/baseline.apk"
cp android/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk "$private/test.apk"
cause=version_overlay
versions=$(python3 scripts/android-rehearsal-version.py android/app/build.gradle.kts)
IFS=$'\t' read -r baseline_version candidate_version <<< "$versions"
[[ "$candidate_version" -gt "$baseline_version" ]]
printf 'baseline=%s candidate=%s source=%s\n' "$baseline_version" "$candidate_version" "$EXPECTED_TREE" > narrow-evidence/overlay.txt
sha256sum android/app/build.gradle.kts >> narrow-evidence/overlay.txt
# Only the reviewed single literal version assignment may differ from the source checkout.
python3 - "$baseline_version" "$candidate_version" <<'PYOVERLAY'
from pathlib import Path
import subprocess,sys
base=subprocess.check_output(['git','show','HEAD:android/app/build.gradle.kts'],text=True)
current=Path('android/app/build.gradle.kts').read_text()
assert current==base.replace('versionCode = '+sys.argv[1]+'\n','versionCode = '+sys.argv[2]+'\n',1)
changed=subprocess.check_output(['git','diff','--name-only'],text=True).splitlines()
assert changed==['android/app/build.gradle.kts']
PYOVERLAY
cause=candidate_offline
timeout --kill-after=10s 180s android/gradlew -p android --offline :app:assembleRelease --no-daemon > "$private/candidate.log" 2>&1
cp android/app/build/outputs/apk/release/app-release-unsigned.apk "$private/candidate.apk"
cause=disposable_signing
keytool -genkeypair -keystore "$private/test.p12" -storetype PKCS12 -alias rehearsal -storepass disposable-only -keypass disposable-only -keyalg RSA -keysize 3072 -validity 2 -dname 'CN=Disposable Narrow Rehearsal' >/dev/null 2>&1
build_tools=$(find "$ANDROID_HOME/build-tools" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -1)
for name in baseline test candidate; do
  "$build_tools/apksigner" sign --ks "$private/test.p12" --ks-key-alias rehearsal --ks-pass pass:disposable-only --key-pass pass:disposable-only "$private/$name.apk"
  "$build_tools/apksigner" verify --verbose --print-certs "$private/$name.apk" > "$private/$name.signature"
done
cause=signer_output_contract
printf 'build_tools=%s\n' "$(basename "$build_tools")" >> narrow-evidence/overlay.txt
sha256sum "$build_tools/lib/apksigner.jar" | awk '{print "apksigner_jar_sha256=" $1}' >> narrow-evidence/overlay.txt
tool_version=$("$build_tools/apksigner" version)
python3 qa/signer-output-contract.py narrow-evidence/signer.json "$tool_version" "$private/baseline.signature" "$private/test.signature" "$private/candidate.signature"
for name in baseline candidate; do "$build_tools/aapt" dump badging "$private/$name.apk" | grep '^package:' > "$private/$name.package"; done
grep -F "name='com.frpboy.scrobblix' versionCode='$baseline_version'" "$private/baseline.package"
grep -F "name='com.frpboy.scrobblix' versionCode='$candidate_version'" "$private/candidate.package"
{ cat "$private/baseline.package" "$private/candidate.package"; cat narrow-evidence/signer.json; sha256sum "$private/baseline.apk" "$private/test.apk" "$private/candidate.apk" | sed "s|$private/||"; } > narrow-evidence/identity.txt
export ANDROID_USER_HOME="$private/android" ANDROID_EMULATOR_HOME="$private/android" ANDROID_AVD_HOME="$private/android/avd"
mkdir -p "$ANDROID_AVD_HOME"
test -e /dev/kvm
sudo chmod a+rw /dev/kvm
cause=sdk_image
timeout 180 sdkmanager 'emulator' "system-images;android-$API;default;x86_64" > "$private/sdk.log" 2>&1
boot_fenced() {
  local avd=$1
  local deadline=$((SECONDS+600))
  echo no | timeout 30 avdmanager create avd -n "$avd" -k "system-images;android-$API;default;x86_64" --path "$ANDROID_AVD_HOME/$avd.avd"
  setsid emulator -avd "$avd" -no-window -no-audio -no-snapshot -gpu swiftshader_indirect -no-boot-anim > "$private/$avd.log" 2>&1 &
  emulator_pid=$!
  timeout 180 adb wait-for-device
  local booted=false rooted=false
  for attempt in $(seq 1 120); do
    test "$SECONDS" -lt "$deadline"
    if [[ $(timeout 2 adb shell getprop sys.boot_completed | tr -d '\r') = 1 ]]; then booted=true; break; fi
    sleep 2
  done
  test "$booted" = true
  for attempt in 1 2 3; do
    test "$SECONDS" -lt "$deadline"
    timeout 30 adb wait-for-device; timeout 10 adb root || true; timeout 30 adb wait-for-device
    if [[ $(timeout 10 adb shell id -u | tr -d '\r') = 0 ]]; then rooted=true; break; fi
  done
  test "$rooted" = true
  test "$(timeout 10 adb shell getprop ro.build.version.sdk | tr -d '\r')" = "$API"
  for family in iptables ip6tables; do
    test "$SECONDS" -lt "$deadline"
    timeout 40 adb shell "$family" -w 30 -I OUTPUT 1 ! -o lo -j REJECT
    timeout 40 adb shell "$family" -w 30 -C OUTPUT ! -o lo -j REJECT
  done
  test "$SECONDS" -lt "$deadline"
  timeout 60 adb install "$private/baseline.apk"
  test "$SECONDS" -lt "$deadline"
  timeout 60 adb install "$private/test.apk"
  timeout 10 adb shell pm list instrumentation | grep -Fx 'instrumentation:com.frpboy.scrobblix.test/androidx.test.runner.AndroidJUnitRunner (target=com.frpboy.scrobblix)'
}
phase() {
  local output=$1 gate=$2 value=$3 cls=$4 method=$5
  cause=$output
  local extra=()
  if [[ "$cls" = dev.scrobblix.app.SignedUpdateRehearsalTest ]]; then extra=(-e signedUpdateRehearsal disposable-isolated-only); fi
  local phase_result=0
  timeout 120 adb shell am instrument -w -r "${extra[@]}" -e "$gate" "$value" -e class "$cls#$method" com.frpboy.scrobblix.test/androidx.test.runner.AndroidJUnitRunner > "$private/phase-raw.txt" 2>&1 || phase_result=$?
  python3 - "$output" "$cls" "$method" "$private/phase-raw.txt" <<'PY'
import sys
from pathlib import Path
import runpy
contract=runpy.run_path('qa/narrow-target-contract.py')
raw=Path(sys.argv[4]).read_text()
safe=contract['redact_phase'](raw)
try:
    contract['phase'](raw,sys.argv[2],sys.argv[3])
except AssertionError:
    safe+='INSTRUMENTATION_FAILED: redacted_failure\n'
    Path('narrow-evidence',sys.argv[1]+'.txt').write_text(safe)
    raise SystemExit(1)
Path('narrow-evidence',sys.argv[1]+'.txt').write_text(safe)
contract['phase'](safe,sys.argv[2],sys.argv[3])
PY
  test "$phase_result" = 0
}
# Inline parser import uses the hyphenated source filename without renaming its authority.
cause=process_guest
boot_fenced process-boundary
phase process-prepare processRecoveryPhase prepare dev.scrobblix.app.ProcessRecoveryTest prepareBeforeProcessDeath
timeout 10 adb shell am force-stop com.frpboy.scrobblix
phase process-verify processRecoveryPhase verify dev.scrobblix.app.ProcessRecoveryTest verifyAfterProcessDeath
cleanup_guest
# Distinct newly created AVD, never uninstall/reset an existing app installation.
cause=update_guest
boot_fenced disposable-update
phase update-prepare signedUpdatePhase prepare dev.scrobblix.app.SignedUpdateRehearsalTest preserveSyntheticStateAcrossSignedReleaseUpdate
timeout 10 adb shell am force-stop com.frpboy.scrobblix
cause=update_install
timeout 60 adb install -r "$private/candidate.apk"
timeout 10 adb shell dumpsys package com.frpboy.scrobblix | grep -E "versionCode=$candidate_version([[:space:]]|$)"
# Existing test demands this exact additional gate as well as explicit phase.
phase update-verify signedUpdatePhase verify dev.scrobblix.app.SignedUpdateRehearsalTest preserveSyntheticStateAcrossSignedReleaseUpdate
cleanup_guest
cause=complete
