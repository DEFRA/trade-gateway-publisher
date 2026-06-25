#!/usr/bin/env bash
# Run integration tests against the docker-compose stack (floci, mongo, wiremock).

set -euo pipefail

docker compose up -d --quiet-pull floci mongodb wiremock
docker compose up --force-recreate --no-deps aws-init

dotnet test --filter "Category=IntegrationTest"
