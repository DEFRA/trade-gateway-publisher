#!/usr/bin/env bash
# Run integration tests against the docker-compose stack (floci, mongo, wiremock).

set -euo pipefail

docker compose up -d --force-recreate --quiet-pull floci mongodb servicebus-emulator mssql wiremock

dotnet test --filter "Category=IntegrationTest"
