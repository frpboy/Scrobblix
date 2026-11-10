#!/usr/bin/env python3
"""Bump a disposable checkout only. No APK, signing, account, or installation operations."""
import argparse
from pathlib import Path
import re

MAX_VERSION_CODE = 2100000000
PATTERN = re.compile(r'(?m)^([ \t]*versionCode[ \t]*=[ \t]*)([0-9]+)([ \t]*)$')

def prepare(source):
    matches = list(PATTERN.finditer(source))
    if len(matches) != 1:
        raise ValueError('Exactly one literal versionCode assignment is required')
    match = matches[0]
    baseline = int(match[2])
    if not 1 <= baseline < MAX_VERSION_CODE:
        raise ValueError('Baseline versionCode cannot be safely incremented')
    candidate = baseline + 1
    return source[:match.start(2)] + str(candidate) + source[match.end(2):], baseline, candidate

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('build_file', type=Path)
    args = parser.parse_args()
    before = args.build_file.read_text()
    after, baseline, candidate = prepare(before)
    # Called only after baseline APKs have been built and copied in the owned disposable checkout.
    args.build_file.write_text(after)
    if args.build_file.read_text() != after:
        raise RuntimeError('Disposable version override readback mismatch; stop before candidate build')
    print(f'{baseline}\t{candidate}')

if __name__ == '__main__':
    main()
