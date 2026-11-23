# tiny-cs-api

C# (.NET 8) microservice template configured for pipeline execution and local development with `mise` and `just`.

## Pipeline Targets

```bash
just quality   # Format check, hadolint, and gitleaks
just test      # Run unit tests and retain artifacts in ./test-results
just build     # Build Chainguard candidate container image (tiny-cs-api:candidate)
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
