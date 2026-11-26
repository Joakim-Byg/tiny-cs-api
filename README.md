# tiny-cs-api

C# (.NET 8) microservice template configured for pipeline execution and local development with `mise` and `just`.

## Pipeline Targets

```bash
just quality   # Format check, hadolint, and gitleaks
just test      # Run unit tests and retain artifacts in ./test-results
just build     # Build candidate container image (tiny-cs-api:candidate)
just exercise  # Exercise container under Restricted PSS constraints (UID 65532, read-only rootfs)
just scan      # Scan dependencies and container image with Trivy
just all       # Run all stages sequentially
just run       # Start service locally with Compose (Docker or Podman)
just down      # Stop local Compose service
```

## Endpoints

- `GET /`: Service status
- `GET /readyz`: Standard readiness probe
- `GET /livez`: Standard liveness probe
- `GET /health/startup`: Startup probe
- `GET /metrics`: Prometheus formatted metrics with request counter and duration histogram
- `GET /features`: Feature flags and configuration status
- `GET /api/dark-launch`: Dark-launched endpoint (returns 404 when disabled, 200 when enabled)

## Runtime Configuration & Dark Launch Demo

Configuration is loaded from `config/features.json` with `reloadOnChange: true`. Mount the `./config` directory into `/app/config:ro` to change feature flags at runtime without restarting the container:

```bash
# Start container with mounted config
docker run --rm -p 8080:8080 -v $(pwd)/config:/app/config:ro ghcr.io/joakim-byg/tiny-cs-api:latest

# 1. Dark-launch endpoint is disabled by default (returns 404)
curl -i http://localhost:8080/api/dark-launch

# 2. Check feature flags status
curl http://localhost:8080/features

# 3. Enable the feature live by editing config/features.json:
# Change "Enabled": false -> "Enabled": true

# 4. Request the endpoint again without restarting the container (returns 200)
curl -i http://localhost:8080/api/dark-launch
```

## Pull and Run Container

Once published via the GitHub Actions workflow:

```bash
docker pull ghcr.io/joakim-byg/tiny-cs-api:latest
docker run --rm -p 8080:8080 ghcr.io/joakim-byg/tiny-cs-api:latest
```

