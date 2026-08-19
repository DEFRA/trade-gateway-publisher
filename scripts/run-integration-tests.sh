#!/usr/bin/env bash
# Run integration tests against the docker-compose stack (floci, mongo, wiremock).

set -euo pipefail

docker compose up -d --force-recreate --quiet-pull

dotnet test --filter "Category=IntegrationTest"
