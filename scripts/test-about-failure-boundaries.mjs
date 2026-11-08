import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import vm from "node:vm";
const read=p=>readFileSync(p,"utf8");
const script=read("browser-extension/about.js");
for(const mode of ["missing","throw","success","null-version"]){
 const version={textContent:"version fallback"},identity={textContent:"id fallback"};
 const chrome=mode==="missing"?undefined:{runtime:{id:"test-id",getManifest(){if(mode==="throw")throw Error("private error");return{version:mode==="null-version"?undefined:"9.8.7-test"}}}};
 vm.runInNewContext(script,{chrome,document:{getElementById:id=>id==="about-version"?version:identity}});
 assert.equal(version.textContent,mode==="success"?"Version: 9.8.7-test":mode==="null-version"?"Version: Unavailable":"version fallback");
 assert.equal(identity.textContent,mode==="success"||mode==="null-version"?"Extension ID: test-id":"id fallback");
}
vm.runInNewContext(script,{chrome:{runtime:{getManifest:()=>({version:"1"})}},document:{getElementById:()=>null}});
const cs=read("src/Scrobblix.Desktop/MainWindow.xaml.cs").split("private readonly TrackingMode")[0];
assert.match(cs,/if \(_aboutFlowActive\) return;/);assert.match(cs,/_aboutFlowActive = true;[\s\S]*try/);assert.match(cs,/catch \(Exception\)[\s\S]*AboutStatusText.Text/);assert.match(cs,/finally[\s\S]*_aboutFlowActive = false/);
const kt=read("android/app/src/main/kotlin/dev/scrobblix/app/ScrobblixAbout.kt");
assert.match(kt,/try \{ handler.openUri\(url\); false \} catch \(_:Exception\) \{ true \}/);assert.match(kt,/SelectionContainer \{ Text\(url\) \}/);assert.match(kt,/getPackageInfo[\s\S]*catch \(_:Exception\) \{ "Unavailable" \}/);
console.log("PASS: browser missing/throw/success/missing-version/missing-elements runtime cases; Windows guard/finally + Android launch/metadata/copy-fallback SOURCE assertions only (not native runtime tests)");
