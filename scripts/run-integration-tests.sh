#!/usr/bin/env bash
# Run integration tests against the docker-compose stack (floci, mongo, wiremock).

set -euo pipefail

docker compose up -d --wait --quiet-pull mssql

docker compose up -d --wait --quiet-pull wiremock

docker compose up -d --wait --quiet-pull mongodb

docker compose up -d --wait --quiet-pull servicebus-emulator

docker compose up -d --wait --quiet-pull floci 

dotnet test --filter "Category=IntegrationTest"
