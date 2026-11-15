#!/usr/bin/env bash
# DISPOSABLE stage-B verifier (branch test/ingest-synthetic-prep-20261116,
# never merged to main): exact path-bound verification of a qualified stage-A
# WINDOWS archive's extracted tree against its two sha256 manifests. Each
# manifest is verified against its OWN exact root-relative tree (manifests are
# written on Windows with backslash separators; paths are translated here).
# Rules: literal paths only (no basename fallback), traversal and rooted
# paths rejected, duplicates rejected, every hashed file must exist at its
# exact path with matching bytes, and every file under each root must be
# manifest-listed (sole exception: a zero-byte .gitkeep sentinel, excluded
# explicitly at manifest creation). Archive-top files must be the two
# manifests or the loopback wire dump. Prints the verified WireDump exe path
# as WIREDUMP_EXE_REL for the caller.
set -euo pipefail

dir="${1:?usage: verify-windows-archive.sh EXTRACT_DIR}"
cd "$dir"

declare -A seen=()
wiredump_exe=''
verify_tree() {
  local manifest="$1" root="$2"
  [ -f "$manifest" ] || { echo "FAIL: $manifest missing from archive root"; exit 1; }
  [ -d "$root" ] || { echo "FAIL: expected tree root missing: $root"; exit 1; }
  local line hash rel path actual
  while IFS= read -r line; do
    # Manifests are written on Windows: strip the CRLF carriage return only
    # (no other normalization), and compare hashes case-insensitively because
    # Get-FileHash emits uppercase while sha256sum emits lowercase.
    line="${line%$'\r'}"
    [ -z "$line" ] && continue
    hash="${line%%  *}"
    hash="${hash,,}"
    rel="${line#*  }"
    rel="${rel//\\//}"
    case "$rel" in
      *..*) echo "FAIL: traversal in manifest path: $rel"; exit 1 ;;
      /* | *:* | \\*) echo "FAIL: rooted/absolute manifest path: $rel"; exit 1 ;;
    esac
    path="$root/$rel"
    if [ -n "${seen[$path]:-}" ]; then echo "FAIL: duplicate manifest path: $path"; exit 1; fi
    seen[$path]=1
    [ -f "$path" ] || { echo "FAIL: missing at exact path: $path"; exit 1; }
    actual=$(sha256sum "$path" | cut -d' ' -f1)
    [ "$actual" = "$hash" ] || { echo "FAIL: sha mismatch: $path"; exit 1; }
    echo "verified exact path: $path $hash"
    if [ "$manifest" = 'windows-wiredump-manifest-sha256.txt' ] && [ "${rel##*/}" = 'Scrobblix.WireDump.exe' ]; then
      wiredump_exe="$path"
    fi
  done < "$manifest"
}

verify_tree 'windows-deps-manifest-sha256.txt' 'src/Scrobblix.Desktop/bin/Debug'
verify_tree 'windows-wiredump-manifest-sha256.txt' 'tests/Scrobblix.WireDump/bin/Debug'

# Membership: every file in the tree is manifest-listed, a zero-byte .gitkeep
# sentinel, or a known archive-top aux file.
while IFS= read -r f; do
  f="${f#./}"
  if [ -n "${seen[$f]:-}" ]; then continue; fi
  case "$f" in
    windows-deps-manifest-sha256.txt | windows-wiredump-manifest-sha256.txt | wire-dump-windows.txt) ;;
    *.gitkeep)
      [ "$(stat -c %s "$f")" = '0' ] || { echo "FAIL: non-zero-byte unexpected file: $f"; exit 1; }
      echo "allowed zero-byte .gitkeep sentinel: $f" ;;
    *) echo "FAIL: unexpected file in archive: $f"; exit 1 ;;
  esac
done < <(find . -type f | sort)

[ -n "$wiredump_exe" ] || { echo 'FAIL: verified WireDump exe path not found in manifest'; exit 1; }
echo "WIREDUMP_EXE_REL=$wiredump_exe"
echo 'windows archive verification PASS'
