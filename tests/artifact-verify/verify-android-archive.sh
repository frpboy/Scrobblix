#!/usr/bin/env bash
# DISPOSABLE stage-B verifier (branch test/ingest-synthetic-prep-20261116,
# never merged to main): exact path-bound verification of a qualified stage-A
# ANDROID archive's extracted tree against its sha256 manifest.
# Rules: literal manifest paths only (no basename fallback), traversal and
# rooted/absolute paths rejected, duplicates rejected, every hashed file must
# exist at its exact path with matching bytes, and every file in the tree must
# be either a manifest-listed entry or a known aux file (the manifest itself,
# the merged manifest, the loopback wire dump). Prints the verified APK paths
# as ANDROID_APK_REL / ANDROID_TESTAPK_REL lines for the caller.
set -euo pipefail

dir="${1:?usage: verify-android-archive.sh EXTRACT_DIR}"
cd "$dir"
manifest='android-artifact-sha256.txt'
[ -f "$manifest" ] || { echo "FAIL: $manifest missing from archive root"; exit 1; }

declare -A seen=()
while IFS= read -r line; do
  [ -z "$line" ] && continue
  hash="${line%%  *}"
  path="${line#*  }"
  case "$path" in
    *..*) echo "FAIL: traversal in manifest path: $path"; exit 1 ;;
    /* | *:* | \\*) echo "FAIL: rooted/absolute manifest path: $path"; exit 1 ;;
  esac
  if [ -n "${seen[$path]:-}" ]; then echo "FAIL: duplicate manifest path: $path"; exit 1; fi
  seen[$path]=1
  [ -f "$path" ] || { echo "FAIL: missing at exact path: $path"; exit 1; }
  actual=$(sha256sum "$path" | cut -d' ' -f1)
  [ "$actual" = "$hash" ] || { echo "FAIL: sha mismatch: $path"; exit 1; }
  echo "verified exact path: $path $hash"
done < "$manifest"

# Membership: every file in the tree is manifest-listed or known aux.
while IFS= read -r f; do
  f="${f#./}"
  if [ -n "${seen[$f]:-}" ]; then continue; fi
  case "$f" in
    android-artifact-sha256.txt | merged-AndroidManifest.xml | wiredump-android.txt) ;;
    *) echo "FAIL: unexpected file in archive: $f"; exit 1 ;;
  esac
done < <(find . -type f | sort)

paths() { awk '{ $1=""; sub(/^ +/, ""); print }' "$manifest"; }
apk=$(paths | grep -F 'outputs/apk/debug/' | grep -vF 'androidTest' | head -1)
testapk=$(paths | grep -F 'androidTest' | head -1)
[ -n "$apk" ] || { echo 'FAIL: app apk path not found in manifest'; exit 1; }
[ -n "$testapk" ] || { echo 'FAIL: androidTest apk path not found in manifest'; exit 1; }
echo "ANDROID_APK_REL=$apk"
echo "ANDROID_TESTAPK_REL=$testapk"
echo 'android archive verification PASS'
