#!/usr/bin/env bash
# Run integration tests against the docker-compose stack (floci, mongo, wiremock).

set -euo pipefail

docker compose up -d --wait --quiet-pull mssql

docker compose up -d --wait --quiet-pull wiremock

docker compose up -d --wait --quiet-pull mongodb

docker compose up -d --wait --quiet-pull servicebus-emulator

docker compose up -d --wait --quiet-pull floci

# One-off readiness gate: wait for Floci to emit its ready marker.
# Floci already runs 10-setup.sh and 99-ready.sh during startup hooks.
if ! command -v timeout >/dev/null 2>&1; then
  echo "'timeout' command is not available on this runner." >&2
  exit 1
fi

max_attempts=60
ready=0
for i in $(seq 1 "$max_attempts"); do
  echo "Floci readiness check attempt $i/$max_attempts..."

  if timeout 10s docker compose logs --no-color floci 2>/dev/null | grep -q "=== AWS Local Emulator Ready ==="; then
    echo "Floci reports ready."
    ready=1
    break
  fi

  if [[ $((i % 10)) -eq 0 ]]; then
    echo "Floci not ready yet; container status:" >&2
    docker compose ps floci >&2 || true
  fi

  sleep 2
done

if [[ "$ready" -ne 1 ]]; then
  echo "Floci did not report ready within 120 seconds." >&2
  docker compose logs floci >&2 || true
  exit 1
fi

dotnet test --filter "Category=IntegrationTest"
