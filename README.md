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
- `GET /api/db-status`: Masked database connection configuration status

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

## Testing Database Secrets Locally

Start the container locally with Compose:

```bash
just run
# Or supply a custom connection string:
just run "Host=custom-db;Database=demo;Username=admin;Password=secretpass;"
```

Inspect the database configuration status (passwords are safely masked):

```bash
curl http://localhost:8080/api/db-status
# Returns: {"configured":true,"connectionString":"Host=localhost;...;Password=******;","timestamp":"..."}
```

### Dynamic Secret Rotation Demo (Without Container Restart)

Override or rotate the connection string at runtime by writing to the mounted `secrets/` directory:

```bash
# Write or rotate a secret file
echo -n "Host=vault-pg;Database=production;Username=app_user;Password=newpassword;" > secrets/ConnectionStrings__DefaultConnection

# Query the endpoint immediately (KeyPerFile reloads live without restart)
curl http://localhost:8080/api/db-status
# Returns: {"configured":true,"connectionString":"Host=vault-pg;...;Password=******;","timestamp":"..."}
```

## Pull and Run Container

Once published via the GitHub Actions workflow:

```bash
docker pull ghcr.io/joakim-byg/tiny-cs-api:latest
docker run --rm -p 8080:8080 ghcr.io/joakim-byg/tiny-cs-api:latest
```

## Kubernetes Deployment Templates (`k8s/`)

The `k8s/` directory contains template manifests demonstrating how to deploy the container under Kubernetes Restricted PSS constraints, mount runtime configuration, and manage secrets via Vault or Kubernetes Secrets.

### 1. Generating ConfigMap from `config.json`

In GitOps repositories using Kustomize, generate a `ConfigMap` directly from your configuration file using `configMapGenerator`:

```yaml
# In your kustomization.yaml
configMapGenerator:
  - name: tiny-cs-api-config
    files:
      - config.json=path/to/config.json
```

Or via CLI:

```bash
kubectl create configmap tiny-cs-api-config --from-file=config.json=config/config.json
```

When mounted to `/app/config`, ASP.NET Core loads `/app/config/config.json` with `reloadOnChange: true`, updating live when the ConfigMap is modified.

### 2. Secret Template (`config/secret.example.json`)

A template structure for credentials is provided in `config/secret.example.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.internal;Port=5432;Database=tinydb;Username=tinyuser;Password=supersecretpass;"
  },
  "Database": {
    "Host": "db.internal",
    "Port": 5432,
    "Name": "tinydb",
    "User": "tinyuser",
    "Password": "supersecretpass"
  }
}
```

> **Security Note:** Real `secret.json` files are ignored by git (`.gitignore`). Never commit plain-text credentials to the repository.

### 3. Vault & External Secrets (`k8s/external-secret.yaml`)

For production environments using HashiCorp Vault with the External Secrets Operator (ESO), `k8s/external-secret.yaml` provides the template for syncing credentials into a Kubernetes `Secret`:

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: tiny-cs-api-secrets
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: vault-backend
    kind: SecretStore
  target:
    name: tiny-cs-api-secrets
    creationPolicy: Owner
  data:
    - secretKey: ConnectionStrings__DefaultConnection
      remoteRef:
        key: secret/data/tiny-cs-api/database
        property: connection_string
```

### 4. Verifying Secret & DB Configuration

ASP.NET Core resolves `ConnectionStrings__DefaultConnection` either through environment variables (`secretKeyRef`) or directory-mounted secrets (`/app/secrets` via `AddKeyPerFile` and `/app/secrets/secret.json`). Check configuration status safely without exposing passwords:

```bash
curl http://localhost:8080/api/db-status
```


