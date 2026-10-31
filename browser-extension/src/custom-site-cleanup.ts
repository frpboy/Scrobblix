export type CustomSiteBrowserCleanup = {
  unregisterContentScripts: (details: { ids: string[] }) => Promise<void>;
  removePermissions: (details: { origins: string[] }) => Promise<boolean>;
  containsPermissions: (details: { origins: string[] }) => Promise<boolean>;
};

/**
 * Attempt both cleanup operations even if the first one fails.
 * The caller has already revoked forwarding in the service worker, so a
 * browser cleanup failure must be surfaced rather than silently aborting.
 */
export async function cleanupCustomSiteBrowserAccess(
  siteId: string,
  pattern: string,
  cleanup: CustomSiteBrowserCleanup,
): Promise<boolean> {
  let failed = false;

  try {
    await cleanup.unregisterContentScripts({ ids: [siteId] });
  } catch {
    failed = true;
  }

  try {
    const permissionRemoved = await cleanup.removePermissions({ origins: [pattern] });
    if (!permissionRemoved && await cleanup.containsPermissions({ origins: [pattern] })) {
      failed = true;
    }
  } catch {
    try {
      if (await cleanup.containsPermissions({ origins: [pattern] })) failed = true;
    } catch {
      failed = true;
    }
  }

  return !failed;
}
