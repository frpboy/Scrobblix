#!/usr/bin/env bash
# Pull preserved synthetic test frames off the emulator, then remove them.
# Requires: adb in PATH, emulator booted, connected tests already run.
set -u
OUT=${1:-shots/synthetic-frames}
mkdir -p "$OUT"
for f in $(adb shell ls /sdcard/Download/scrobblix-*.png 2>/dev/null | tr -d '\r'); do
  adb pull "$f" "$OUT/" || exit 1
done
adb shell rm -f /sdcard/Download/scrobblix-*.png || true
ls -la "$OUT"
