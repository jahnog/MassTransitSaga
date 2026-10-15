#!/usr/bin/env bash
# Shared helpers for the order saga workstation service. Source this file; do not execute it.
# shellcheck shell=bash

set -euo pipefail

_ORDER_SAGA_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${_ORDER_SAGA_LIB_DIR}/.." && pwd -P)"
HOME_DIR="${HOME}"
# Names below are the shared API for the scripts that source this file.
# shellcheck disable=SC2034
DOTNET="${HOME_DIR}/.dotnet/dotnet"
PUBLISH_DIR="${REPO_ROOT}/artifacts/service"
USER_UNIT_DIR="${XDG_CONFIG_HOME:-${HOME_DIR}/.config}/systemd/user"
ORDER_UNIT="order-saga.service"
KAFKA_UNIT="kafka.service"
DOCKER_UNIT="docker-rootless.service"
COSMOS_TS_UNIT="cosmos-tailscale.service"
LISTEN_URL="http://127.0.0.1:5080"

order_saga_render_unit() {
  local src="$1"
  local dest="$2"
  sed -e "s|__REPO_ROOT__|${REPO_ROOT}|g" -e "s|__HOME__|${HOME_DIR}|g" "${src}" >"${dest}"
}
