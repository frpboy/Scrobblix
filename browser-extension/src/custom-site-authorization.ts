export class CustomSiteAuthorization {
  private readonly revokedSiteIds = new Set<string>();

  revoke(siteId: string): void {
    this.revokedSiteIds.add(siteId);
  }

  isRevoked(siteId: string): boolean {
    return this.revokedSiteIds.has(siteId);
  }

  async authorize(
    siteId: string,
    hasPermission: () => Promise<boolean>,
    isRegisteredAndActive: () => Promise<boolean>,
  ): Promise<boolean> {
    if (this.revokedSiteIds.has(siteId)) return false;
    if (!(await hasPermission())) return false;
    if (this.revokedSiteIds.has(siteId)) return false;

    const isActive = await isRegisteredAndActive();
    return isActive && !this.revokedSiteIds.has(siteId);
  }
}
