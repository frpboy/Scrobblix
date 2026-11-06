import { readdirSync, readFileSync, existsSync, mkdirSync, copyFileSync, writeFileSync } from 'node:fs';
import { join, basename, dirname, resolve } from 'node:path';
import { homedir } from 'node:os';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const decodeXml = (value) => value.replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');

export function collectNotices(bundle, cache, applicationLicense) {
  const packages = new Map();
  function visit(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name);
      if (entry.isDirectory()) visit(path);
      else if (entry.name.endsWith('.deps.json')) {
        for (const [identity, metadata] of Object.entries(JSON.parse(readFileSync(path, 'utf8')).libraries ?? {})) {
          if (metadata.type !== 'package') continue;
          const separator = identity.lastIndexOf('/');
          const id = identity.slice(0, separator), version = identity.slice(separator + 1);
          if (!/^[a-z0-9_.-]+$/i.test(id) || !/^[a-z0-9.+-]+$/i.test(version)) throw new Error('Invalid package identity');
          packages.set(identity, { id, version });
        }
      }
    }
  }
  visit(bundle);
  if (!packages.size) throw new Error('No NuGet packages found in published dependency manifests');
  const output = join(bundle, 'third-party-notices');
  mkdirSync(output, { recursive: true });
  copyFileSync(applicationLicense, join(bundle, 'LICENSE'));
  const inventory = [];
  for (const [identity, { id, version }] of [...packages.entries()].sort()) {
    const directory = join(cache, id.toLowerCase(), version.toLowerCase());
    const files = readdirSync(directory);
    const nuspec = files.find(name => name.toLowerCase().endsWith('.nuspec'));
    if (!nuspec) throw new Error(`Missing restored package metadata: ${identity}`);
    const metadata = readFileSync(join(directory, nuspec), 'utf8');
    const license = metadata.match(/<license\s+type=["'](expression|file)["'][^>]*>([^<]+)<\/license>/i);
    if (!license) throw new Error(`Missing license declaration: ${identity}`);
    const type = license[1].toLowerCase(), value = decodeXml(license[2].trim());
    const destination = join(output, `${id}-${version}`);
    mkdirSync(destination, { recursive: true });
    copyFileSync(join(directory, nuspec), join(destination, nuspec));
    const included = [];
    for (const name of files.filter(name => /license|licence|notice|copying|copyright/i.test(name))) {
      if (!/\.(txt|md)$/i.test(name) && !/^(license|licence|copying|copyright)$/i.test(name)) continue;
      copyFileSync(join(directory, name), join(destination, name));
      included.push(name);
    }
    if (type === 'file') {
      // NuGet license paths are package-relative; reject traversal and absolute paths.
      const normalized = value.replace(/\\/g, '/');
      if (normalized.startsWith('/') || normalized.split('/').some(part => part === '..' || part.includes(':'))) throw new Error(`Unsafe license path: ${identity}`);
      const source = join(directory, normalized);
      if (!existsSync(source)) throw new Error(`Missing declared license file: ${identity}`);
      const name = basename(normalized);
      copyFileSync(source, join(destination, name));
      if (!included.includes(name)) included.push(name);
    } else {
      if (!['MIT', 'Apache-2.0'].includes(value)) throw new Error(`License expression requires review: ${identity} (${value})`);
      const name = `SPDX-${value}.txt`;
      copyFileSync(join(scriptDirectory, 'licenses', `${value}.txt`), join(destination, name));
      included.push(name);
    }
    inventory.push({ id, version, licenseType: type, license: value, includedFiles: included.sort() });
  }
  writeFileSync(join(output, 'packages.json'), JSON.stringify(inventory, null, 2) + '\n');
  writeFileSync(join(output, 'README.txt'), 'Third-party package license declarations and redistribution notices. Package nuspec metadata and available license/notice texts are preserved. SPDX expression texts come from spdx/license-list-data. This inventory is not a legal opinion or a claim that Microsoft redistribution restrictions have been independently certified.\n');
  return inventory;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const bundle = process.argv[2];
  if (!bundle) throw new Error('Usage: node scripts/collect-third-party-notices.mjs BUNDLE');
  const result = collectNotices(resolve(bundle), process.env.NUGET_PACKAGES || join(homedir(), '.nuget', 'packages'), resolve('LICENSE'));
  console.log(`Included license evidence for ${result.length} published NuGet packages.`);
}
