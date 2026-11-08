import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=p=>readFileSync(p,"utf8");
const surfaces=["android/app/src/main/kotlin/dev/scrobblix/app/ScrobblixAbout.kt","src/Scrobblix.Desktop/MainWindow.xaml.cs","browser-extension/about.html"];
const shared=["https://www.frpboy.in/","https://github.com/frpboy","https://www.linkedin.com/in/rahul-muraleedharan-it","https://t.me/FRPboy_Projects","https://github.com/frpboy/Scrobblix","https://github.com/frpboy/Scrobblix/releases"];
for(const path of surfaces){const text=read(path);for(const url of shared)assert.ok(text.includes(url),`${path}: ${url}`);assert.ok(text.includes("Apache"));assert.ok(!text.includes("JunkFood02"));assert.ok(!text.includes("GPLv3"));}
for(const path of [surfaces[0],surfaces[1],"browser-extension/sponsor.html"]){const text=read(path);for(const url of ["https://buymeacoffee.com/frpboy","https://www.patreon.com/c/FrpBoy"])assert.ok(text.includes(url));}
assert.match(read("browser-extension/popup.html"),/href="about.html"/);
assert.equal((read("browser-extension/about.html").match(/href="sponsor.html"/g)||[]).length,1);
assert.match(read("android/app/src/main/kotlin/dev/scrobblix/app/MainActivity.kt"),/"About" ->/);
assert.match(read("browser-extension/about.js"),/getManifest\(\)/);
for(const path of [".github/workflows/ci.yml","scripts/setup-browser-companion.ps1"]){for(const file of ["about.html","about.js","sponsor.html"])assert.ok(read(path).includes(file));}
console.log("PASS: 3-surface shared-content, sponsor, runtime metadata, navigation and packaging source checks");

const ci=read(".github/workflows/ci.yml"),setup=read("scripts/setup-browser-companion.ps1");
for(const dir of ["$bundle/browser-extension/","$bundle/browser-extension-firefox/","$companion/chromium/","$companion/firefox/"]){for(const file of ["about.html","about.js","sponsor.html"]){assert.ok(ci.includes(`-Destination "${dir}${file}"`));assert.ok(ci.includes(`"${dir}${file}",`));}}
for(const dir of ["$chromiumRoot","$firefoxRoot"]){for(const file of ["about.html","about.js","sponsor.html"]){assert.ok(setup.includes(`-Destination (Join-Path ${dir} '${file}')`));assert.ok(setup.includes(`(Join-Path ${dir} '${file}'),`));}}
console.log("PASS: all 4 CI package destinations and both setup-package destinations copy and require all 3 new files");
