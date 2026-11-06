"""Read package metadata without accepting license-shaped comments or CDATA."""
import json
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
local = lambda tag: tag.rsplit('}', 1)[-1]
metadata = next((child for child in root if local(child.tag) == 'metadata'), None)
if metadata is None:
    raise ValueError('Missing nuspec metadata')
license = next((child for child in metadata if local(child.tag) == 'license'), None)
if license is None or license.get('type') not in ('file', 'expression') or not license.text:
    raise ValueError('Missing license declaration')
copyright = next((child for child in metadata if local(child.tag) == 'copyright'), None)
print(json.dumps({'type': license.get('type'), 'value': license.text.strip(),
                 'copyright': copyright.text if copyright is not None else None}))
