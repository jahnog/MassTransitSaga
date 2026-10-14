#!/usr/bin/env bash
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

page="$(curl -fsS -m 5 "${LISTEN_URL}/")"
orders="$(curl -fsS -m 5 "${LISTEN_URL}/orders")"
if [[ "${page}" != *"<title>Order saga</title>"* ]]; then
  echo "GET / did not return the order page" >&2
  exit 1
fi
printf 'GET / ok\n'
printf 'GET /orders %s\n' "${orders}"
