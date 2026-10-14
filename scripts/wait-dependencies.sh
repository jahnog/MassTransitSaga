#!/usr/bin/env bash
# Exit 0 when the workstation Cosmos emulator and Kafka broker accept connections.
set -euo pipefail

deadline=$((SECONDS + 90))
while (( SECONDS < deadline )); do
  cosmos="$(curl -fsS -m 2 http://127.0.0.1:8080/ready 2>/dev/null || true)"
  if [[ "${cosmos}" == *'"ready": true'* ]] && timeout 1 bash -c 'echo >/dev/tcp/127.0.0.1/9092' 2>/dev/null; then
    exit 0
  fi
  sleep 2
done

echo "Cosmos emulator or Kafka is not ready." >&2
echo "Cosmos health: http://127.0.0.1:8080/ready" >&2
echo "Kafka: 127.0.0.1:9092" >&2
exit 1
