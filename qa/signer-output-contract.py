#!/usr/bin/env python3
"""Strict public certificate receipt. Never outputs raw signer diagnostics or paths."""
import json
from pathlib import Path
import re
import sys

class ContractError(ValueError): pass

def parse(text):
    lines=text.replace('\r','').splitlines()
    if lines.count('Verifies')!=1 or lines.count('Number of signers: 1')!=1:
        raise ContractError('signer_count_or_verification_missing')
    prefixes=[];digests=[]
    for line in lines:
        if 'certificate SHA-256 digest:' not in line: continue
        match=re.fullmatch(r'(Signer #1|V(?:2|3|3\.1|3\.2|4) Signer:) certificate SHA-256 digest: ([0-9a-fA-F]{64})',line)
        if not match: raise ContractError('certificate_digest_malformed')
        prefixes.append(match[1]);digests.append(match[2].lower())
    if not digests: raise ContractError('certificate_digest_missing')
    if len(prefixes)!=len(set(prefixes)): raise ContractError('duplicate_signer_entry')
    if len(set(digests))!=1: raise ContractError('multiple_certificate_digests')
    return digests[0]

def receipt(texts,tool):
    if set(texts)!= {'baseline','test','candidate'}: raise ContractError('artifact_set_mismatch')
    digests={name:parse(text) for name,text in texts.items()}
    if len(set(digests.values()))!=1: raise ContractError('artifact_certificate_mismatch')
    if not re.fullmatch(r'[0-9]+(?:\.[0-9]+){1,3}',tool): raise ContractError('tool_version_malformed')
    return dict(status='pass',error_category='none',tool_version=tool,certificate_sha256=digests)

def main():
    output=Path(sys.argv[1])
    try:
        result=receipt({name:Path(path).read_text() for name,path in zip(['baseline','test','candidate'],sys.argv[3:])},sys.argv[2])
    except (ContractError,OSError) as error:
        category=str(error) if isinstance(error,ContractError) else 'signature_file_unavailable'
        output.write_text(json.dumps(dict(status='failed',error_category=category))+'\n')
        raise SystemExit(1)
    output.write_text(json.dumps(result,sort_keys=True)+'\n')
if __name__=='__main__': main()
