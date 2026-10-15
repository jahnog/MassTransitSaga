#!/usr/bin/env bash
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

echo "== units =="
systemctl --user --no-pager --full status "${ORDER_UNIT}" "${KAFKA_UNIT}" "${DOCKER_UNIT}" "${COSMOS_TS_UNIT}" || true

echo
echo "== health =="
if "${REPO_ROOT}/scripts/healthcheck.sh"; then
  :
else
  echo "healthcheck failed (server down or still starting)"
fi
