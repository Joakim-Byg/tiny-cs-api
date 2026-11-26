export PATH := env_var_or_default("HOME", "") + "/.local/share/mise/shims:" + env_var_or_default("PATH", "")
set shell := ["bash", "-uc"]

SERVICE_NAME := "tiny-cs-api"
IMAGE_TAG := "candidate"
CONTAINER_CMD := env_var_or_default("CONTAINER_CMD", if `command -v docker 2>/dev/null || true` != "" { "docker" } else if `command -v podman 2>/dev/null || true` != "" { "podman" } else { "docker" })
COMPOSE_CMD := env_var_or_default("COMPOSE_CMD", if CONTAINER_CMD == "podman" { if `command -v podman-compose 2>/dev/null || true` != "" { "podman-compose" } else { "podman compose" } } else { "docker compose" })

default:
    @just --list

# Format check, security, and static analysis
quality:
    @mkdir -p .sarif
    @echo "==> Checking .NET formatting and code style..."
    dotnet format --verify-no-changes
    @echo "==> Linting Dockerfile with hadolint..."
    hadolint Dockerfile
    @hadolint -f sarif Dockerfile > .sarif/quality.sarif 2>/dev/null || true
    @echo "==> Checking for secrets with gitleaks..."
    gitleaks protect --staged --verbose

# Unit and integration tests with retained results
test:
    @echo "==> Running tests and retaining artifacts..."
    @mkdir -p test-results
    dotnet test tests --logger "trx;LogFileName=test-results.trx" --results-directory ./test-results
    @echo "==> Test artifacts retained in ./test-results"

# Build container image
build tag=IMAGE_TAG:
    @echo "==> Building container image: {{SERVICE_NAME}}:{{tag}}..."
    {{CONTAINER_CMD}} build -t {{SERVICE_NAME}}:{{tag}} .

# Start service container and exercise under Restricted PSS constraints (UID 65532, read-only rootfs)
exercise tag=IMAGE_TAG port="8080":
    #!/usr/bin/env bash
    set -euo pipefail
    CONTAINER_NAME="{{SERVICE_NAME}}-exercise-$$"
    echo "==> Starting container $CONTAINER_NAME under Restricted PSS constraints..."
    {{CONTAINER_CMD}} run -d --rm -p {{port}}:8080 -v "$(pwd)/config:/app/config:ro" --user 65532:65532 --read-only --tmpfs /tmp --name "$CONTAINER_NAME" {{SERVICE_NAME}}:{{tag}}

    cleanup() {
        echo "==> Stopping test container $CONTAINER_NAME..."
        {{CONTAINER_CMD}} stop "$CONTAINER_NAME" >/dev/null 2>&1 || true
    }
    trap cleanup EXIT

    echo "==> Waiting for service to respond..."
    for i in {1..30}; do
        if curl -s -f "http://127.0.0.1:{{port}}/readyz" >/dev/null 2>&1; then
            break
        fi
        sleep 0.5
    done

    echo "==> Checking readiness probe (/readyz)..."
    curl -fsS "http://127.0.0.1:{{port}}/readyz" | grep -q "ready"
    echo "✓ Readiness probe passed"

    echo "==> Checking liveness probe (/livez)..."
    curl -fsS "http://127.0.0.1:{{port}}/livez" | grep -q "live"
    echo "✓ Liveness probe passed"

    echo "==> Checking metrics endpoint (/metrics)..."
    METRICS=$(curl -fsS "http://127.0.0.1:{{port}}/metrics")
    echo "$METRICS" | grep -q "http_requests_total"
    echo "$METRICS" | grep -q "http_request_duration_seconds"
    echo "✓ Metrics endpoint passed"

    echo "==> Checking features status endpoint (/features)..."
    curl -fsS "http://127.0.0.1:{{port}}/features" | grep -q "darkLaunch"
    echo "✓ Features endpoint passed"

    echo "==> Checking dark launch endpoint (/api/dark-launch)..."
    STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:{{port}}/api/dark-launch")
    if [ "$STATUS" = "404" ]; then
        echo "✓ Dark launch endpoint returned 404 (disabled by default)"
    else
        echo "✗ Unexpected status: $STATUS"
        exit 1
    fi

    echo "==> Checking database status endpoint (/api/db-status)..."
    DB_OUT=$(curl -fsS "http://127.0.0.1:{{port}}/api/db-status")
    echo "$DB_OUT" | grep -q '"configured"'
    echo "✓ DB status endpoint passed"

    echo "==> All endpoint exercises completed successfully!"

# Scan dependencies and container image for vulnerabilities with Trivy
scan tag=IMAGE_TAG:
    @mkdir -p .sarif
    @echo "==> Scanning filesystem dependencies with Trivy..."
    trivy fs --format sarif -o .sarif/trivy-fs.sarif --ignorefile .trivyignore .
    trivy fs --exit-code 0 --severity HIGH,CRITICAL --ignore-unfixed --ignorefile .trivyignore .
    @echo "==> Scanning container image with Trivy..."
    trivy image --format sarif -o .sarif/trivy-image.sarif --ignorefile .trivyignore {{SERVICE_NAME}}:{{tag}}
    trivy image --exit-code 0 --severity HIGH,CRITICAL --ignore-unfixed --ignorefile .trivyignore {{SERVICE_NAME}}:{{tag}}
    @echo "==> Vulnerability scan completed; SARIF reports saved in .sarif/"

# Execute full pipeline lifecycle
all: quality test build exercise scan
    @echo "==> All pipeline stages completed successfully!"

# Run service and local dependencies with Compose (docker compose / podman compose)
run conn="":
    #!/usr/bin/env bash
    set -euo pipefail
    echo "==> Starting {{SERVICE_NAME}} with {{COMPOSE_CMD}}..."
    if [ -n "{{conn}}" ]; then
        DB_CONNECTION_STRING="{{conn}}" {{COMPOSE_CMD}} up -d
    else
        {{COMPOSE_CMD}} up -d
    fi
    echo "✓ {{SERVICE_NAME}} running at http://127.0.0.1:8080"
    echo "  • Status:      http://127.0.0.1:8080/"
    echo "  • DB Status:   http://127.0.0.1:8080/api/db-status"
    echo "  • Features:    http://127.0.0.1:8080/features"
    echo "  • Dark Launch: http://127.0.0.1:8080/api/dark-launch"

# Alias for run
up: run

# Stop service and local dependencies
down:
    @echo "==> Stopping {{SERVICE_NAME}} with {{COMPOSE_CMD}}..."
    {{COMPOSE_CMD}} down
