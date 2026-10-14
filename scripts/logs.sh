#!/usr/bin/env bash
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

exec journalctl --user -u "${ORDER_UNIT}" -n 80 --no-pager "$@"
