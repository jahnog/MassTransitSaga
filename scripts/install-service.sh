#!/usr/bin/env bash
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

linger="$(loginctl show-user "${USER}" -p Linger --value 2>/dev/null || true)"
if [[ "${linger}" != "yes" ]]; then
  echo "systemd linger is not enabled for ${USER}." >&2
  echo "User units will not start at boot until you run:" >&2
  echo "  sudo loginctl enable-linger ${USER}" >&2
  exit 1
fi

if [[ ! -x "${DOTNET}" ]]; then
  echo "missing ${DOTNET}" >&2
  exit 1
fi

mkdir -p "${USER_UNIT_DIR}" "${PUBLISH_DIR}"

if systemctl --user is-active --quiet "${ORDER_UNIT}"; then
  systemctl --user stop "${ORDER_UNIT}"
fi

export DOTNET_ROOT="${HOME_DIR}/.dotnet"
export PATH="${DOTNET_ROOT}:${PATH}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
"${DOTNET}" publish "${REPO_ROOT}/src/OrderSaga.Host/OrderSaga.Host.csproj" \
  -c Release \
  -f net8.0 \
  -o "${PUBLISH_DIR}" \
  --nologo

order_saga_render_unit "${REPO_ROOT}/systemd/docker-rootless.service" "${USER_UNIT_DIR}/${DOCKER_UNIT}"
order_saga_render_unit "${REPO_ROOT}/systemd/kafka.service" "${USER_UNIT_DIR}/${KAFKA_UNIT}"
order_saga_render_unit "${REPO_ROOT}/systemd/order-saga.service" "${USER_UNIT_DIR}/${ORDER_UNIT}"
order_saga_render_unit "${REPO_ROOT}/systemd/cosmos-tailscale.service" "${USER_UNIT_DIR}/${COSMOS_TS_UNIT}"

systemctl --user daemon-reload
systemctl --user enable "${DOCKER_UNIT}" "${KAFKA_UNIT}" "${ORDER_UNIT}" "${COSMOS_TS_UNIT}"
systemctl --user restart "${COSMOS_TS_UNIT}"

if systemctl --user is-active --quiet "${DOCKER_UNIT}"; then
  systemctl --user start "${KAFKA_UNIT}"
elif [[ -S "${XDG_RUNTIME_DIR}/docker.sock" ]]; then
  echo "Rootless Docker is already running outside ${DOCKER_UNIT}."
  echo "Leave it until that process is stopped, then run: systemctl --user start ${DOCKER_UNIT} ${KAFKA_UNIT}"
else
  systemctl --user start "${DOCKER_UNIT}"
  systemctl --user start "${KAFKA_UNIT}"
fi

echo "Waiting for Cosmos and Kafka..."
if ! "${REPO_ROOT}/scripts/wait-dependencies.sh"; then
  echo "${ORDER_UNIT} is enabled. It will start after Cosmos and Kafka answer." >&2
  exit 1
fi

systemctl --user restart "${ORDER_UNIT}"

ok=0
for _ in $(seq 1 30); do
  if "${REPO_ROOT}/scripts/healthcheck.sh" >/dev/null 2>&1; then
    ok=1
    break
  fi
  sleep 1
done

if [[ "${ok}" -ne 1 ]]; then
  echo "Order saga did not answer ${LISTEN_URL} within 30s. Logs:" >&2
  journalctl --user -u "${ORDER_UNIT}" -n 80 --no-pager >&2 || true
  exit 1
fi

echo
echo "${ORDER_UNIT} is active."
echo "Page: ${LISTEN_URL}"
echo "Logs: ./scripts/logs.sh"
echo "Status: ./scripts/status.sh"
