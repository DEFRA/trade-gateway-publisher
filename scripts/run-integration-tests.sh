#!/usr/bin/env bash
# Run integration tests against the docker-compose stack (floci, mongo, wiremock).

set -euo pipefail

docker compose up -d --wait --quiet-pull mssql

docker compose up -d --wait --quiet-pull wiremock

docker compose up -d --wait --quiet-pull mongodb

docker compose up -d --wait --quiet-pull servicebus-emulator

docker compose up -d --wait --quiet-pull floci

# One-off readiness gate: ensure floci init scripts have created all required resources.
for i in {1..60}; do
  if docker compose exec -T floci bash -c '/etc/floci/init/ready.d/99-ready.sh' >/dev/null 2>&1; then
    break
  fi

  if [ "$i" -eq 60 ]; then
    echo "Floci resources were not ready within 120 seconds." >&2
    docker compose logs floci >&2 || true
    exit 1
  fi

  sleep 2
done

dotnet test --filter "Category=IntegrationTest"
