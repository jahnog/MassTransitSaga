#!/usr/bin/env bash
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

systemctl --user disable --now "${ORDER_UNIT}" 2>/dev/null || true
rm -f "${USER_UNIT_DIR}/${ORDER_UNIT}"
systemctl --user daemon-reload

echo "Removed ${ORDER_UNIT}."
echo "Left ${KAFKA_UNIT} and ${DOCKER_UNIT} in place, and left ${PUBLISH_DIR}."
