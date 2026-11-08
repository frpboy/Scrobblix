#!/usr/bin/env bash
set -euo pipefail
# Fixed bounded retries address adbd restart/closed transport only. Never reboot,
# wipe, reinstall or start instrumentation here. Caller enforces 80s wall cap.
for attempt in 1 2 3; do
  echo "ABOUT_ROOT_READINESS attempt=$attempt/3"
  if timeout 8 adb root && timeout 8 adb wait-for-device; then
    uid=$(timeout 8 adb shell id -u | tr -d '\r') || uid=unavailable
    if [ "$uid" = 0 ]; then echo 'ABOUT_ROOT_READINESS verified_uid=0'; exit 0; fi
    echo 'ABOUT_ROOT_READINESS nonroot_or_unavailable'
  fi
  if [ "$attempt" != 3 ]; then sleep 2; fi
done
echo 'ABOUT_ROOT_READINESS failed_closed' >&2
exit 1
