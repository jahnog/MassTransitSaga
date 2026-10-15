#!/usr/bin/env bash
# Listen on this machine's Tailscale IPv4 address and forward to the loopback emulator.
set -euo pipefail

if ! command -v socat >/dev/null; then
  echo "socat is required" >&2
  exit 1
fi
if ! command -v tailscale >/dev/null; then
  echo "tailscale is required" >&2
  exit 1
fi

deadline=$((SECONDS + 90))
while (( SECONDS < deadline )); do
  if timeout 1 bash -c 'echo >/dev/tcp/127.0.0.1/8081' 2>/dev/null; then
    break
  fi
  sleep 2
done

if ! timeout 1 bash -c 'echo >/dev/tcp/127.0.0.1/8081' 2>/dev/null; then
  echo "Cosmos emulator is not accepting connections on 127.0.0.1:8081." >&2
  exit 1
fi

ts_ip="$(tailscale ip -4)"
if [[ -z "${ts_ip}" ]]; then
  echo "tailscale has no IPv4 address" >&2
  exit 1
fi

exec socat "TCP-LISTEN:8081,bind=${ts_ip},fork,reuseaddr" TCP:127.0.0.1:8081
