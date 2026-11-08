#!/usr/bin/env bash
set -euo pipefail
case "${ABOUT_API:?ABOUT_API required}" in 26|36) ;; *) exit 2;; esac
if [ "${ABOUT_ARGV_FIXTURE:-0}" = 1 ]; then
  sdkmanager emulator "system-images;android-${ABOUT_API};default;x86_64"
  avdmanager create avd -n scrobblix-test -k "system-images;android-${ABOUT_API};default;x86_64"
  exit 0
fi
export JAVA_HOME="$JAVA_HOME_17_X64"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$ANDROID_HOME/cmdline-tools/latest/bin:$PATH"
# Resolve only the exact checksum-pinned toolchain before emulator startup.
python3 scripts/prepare-about-gradle.py
export ANDROID_USER_HOME="$RUNNER_TEMP/scrobblix-android"
export ANDROID_EMULATOR_HOME="$ANDROID_USER_HOME"
export ANDROID_AVD_HOME="$ANDROID_USER_HOME/avd"
mkdir -p "$ANDROID_AVD_HOME"
test -e /dev/kvm
sudo chmod a+rw /dev/kvm
sdkmanager 'emulator' "system-images;android-${ABOUT_API:?ABOUT_API required};default;x86_64"
echo no | avdmanager create avd -n scrobblix-test -k "system-images;android-${ABOUT_API:?ABOUT_API required};default;x86_64" --path "$ANDROID_AVD_HOME/scrobblix-test.avd" --force
test -f "$ANDROID_AVD_HOME/scrobblix-test.ini"
emulator -list-avds | grep -Fx scrobblix-test
emulator -avd scrobblix-test -no-window -no-audio -no-snapshot -gpu swiftshader_indirect -no-boot-anim > /tmp/scrobblix-emulator.log 2>&1 &
emulator_pid=$!
trap 'kill "$emulator_pid" || true' EXIT
if ! timeout 180 adb wait-for-device; then
  cat /tmp/scrobblix-emulator.log
  exit 1
fi
booted=false
for attempt in $(seq 1 120); do
  if [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; then booted=true; break; fi
  sleep 2
done
if [ "$booted" != true ]; then tail -80 /tmp/scrobblix-emulator.log; exit 1; fi
# All instrumentation uses synthetic identities. Once a default endpoint
# exists, block emulator egress so queued fixture work cannot reach it.
# ADB uses the emulator transport, not this guest IP output path.
# Root is a bounded guest-readiness handshake, not an app/test retry.
# Failure is fail-closed before firewall verification, compilation or install.
if ! timeout 80 bash scripts/about-adb-root-ready.sh; then
  timeout 5 adb get-state || true
  timeout 5 adb shell getprop sys.boot_completed || true
  tail -80 /tmp/scrobblix-emulator.log
  exit 1
fi
adb shell iptables -w 30 -I OUTPUT 1 ! -o lo -j REJECT
adb shell ip6tables -w 30 -I OUTPUT 1 ! -o lo -j REJECT
adb shell iptables -w 30 -C OUTPUT ! -o lo -j REJECT
adb shell ip6tables -w 30 -C OUTPUT ! -o lo -j REJECT
android/gradlew -p android :app:assembleDebug :app:assembleDebugAndroidTest --no-daemon
adb install -r android/app/build/outputs/apk/debug/app-debug.apk
adb install -r android/app/build/outputs/apk/androidTest/debug/app-debug-androidTest.apk
mkdir -p android-about-evidence
result=$(adb shell am instrument -w -r -e class dev.scrobblix.app.AboutUiTest com.frpboy.scrobblix.test/androidx.test.runner.AndroidJUnitRunner)
printf '%s\n' "$result" | tee android-about-evidence/instrumentation.txt
printf '%s\n' "$result" | grep -F 'OK (1 test)'
if printf '%s\n' "$result" | grep -E 'FAILURES|INSTRUMENTATION_FAILED|Process crashed'; then exit 1; fi
for scale in 1.0 2.0; do
  for label in about-$scale-top about-$scale-identity about-$scale-failure sponsor-$scale; do
    adb exec-out run-as com.frpboy.scrobblix cat cache/scrobblix-$label.png > android-about-evidence/$label.png
    test -s android-about-evidence/$label.png
  done
done
