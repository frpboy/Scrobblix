"use strict";
// Leave the honest HTML fallback intact if this is not a usable extension context.
try {
  const extensionApi = globalThis.browser ?? globalThis.chrome;
  if (extensionApi?.runtime?.getManifest) {
    const manifest = extensionApi.runtime.getManifest();
    const version = document.getElementById("about-version");
    const identity = document.getElementById("about-id");
    if (version) version.textContent = `Version: ${manifest.version ?? "Unavailable"}`;
    if (identity) identity.textContent = `Extension ID: ${extensionApi.runtime.id ?? "Unavailable"}`;
  }
} catch {
  // No raw error, guessed version or synthetic ID is shown to the user.
}
