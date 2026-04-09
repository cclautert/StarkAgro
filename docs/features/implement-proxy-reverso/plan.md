# Implementation Plan: Implement Reverse Proxy (Nginx)
Generated: 2026-04-09

---

## Context

The project currently exposes the Angular UI directly on port 80 via the Nginx Alpine container that is baked into the `agripewebui` Docker image. That image already acts as an internal reverse proxy (forwarding `/api/` traffic to the API service). What is missing is an external, host-level Nginx reverse proxy that:

1. Terminates TLS (HTTPS on port 443) using Let's Encrypt certificates.
2. Redirects plain HTTP (port 80) to HTTPS.
3. Forwards all requests to the correct internal Docker service with the standard proxy headers.

The issue targets a Linux VPS (Ubuntu) deployment using Docker Compose, not the AWS ECS/ALB path (which already handles TLS at the load-balancer level via Terraform).

---

## Approach

Two design options exist. The chosen option is documented first; the alternative is noted.

### Chosen Design: Dedicated Nginx Proxy Container (Certbot Standalone)

Add a new `nginx-proxy` service to `docker-compose.yml`. This container:
- Runs `nginx:alpine` with a custom configuration mounted from `docker/nginx/`.
- Listens on ports 80 and 443 of the host.
- Routes all traffic to the internal `agripewebui` service (which stays on its internal port 80 and is no longer exposed to the host).
- Terminates TLS using certificates obtained by a companion `certbot` service.

The existing `agripewebui` service's port binding (`"80:80"`) is removed. The `agripewebui` container becomes an internal service only, reachable at `http://agripewebui:80` within `app-network`. The `agripewebui` internal Nginx already handles `/api/` proxying to `agripewebapi:8080`, so the outer proxy only needs to forward everything to `agripewebui`.

The `agripewebapi` service also removes its host port binding (`"8080:8080"`) in production, since external callers will reach it through the UI's internal proxy at `/api/`. (The binding can be kept for local development convenience — see notes in the Files to Modify section.)

### Alternative Design (Not Chosen): Certbot with nginx-proxy + acme-companion

Tools like `jwilder/nginx-proxy` + `acme-companion` auto-generate Nginx config from container labels. This adds complexity and is harder to audit. The manual approach is preferred for a project of this size.

---

## New Files to Create

### 1. `docker/nginx/nginx.conf`

Type: Nginx configuration — production reverse proxy.

Purpose: The main Nginx configuration for the proxy container. Handles HTTP-to-HTTPS redirect and TLS termination.

Key structure:

```
# HTTP block: redirect all HTTP to HTTPS, except Let's Encrypt ACME challenge
server {
    listen 80;
    server_name CHANGE_ME_DOMAIN;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$host$request_uri;
    }
}

# HTTPS block: terminate TLS, forward to agripewebui
server {
    listen 443 ssl;
    server_name CHANGE_ME_DOMAIN;

    ssl_certificate     /etc/letsencrypt/live/CHANGE_ME_DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/CHANGE_ME_DOMAIN/privkey.pem;

    # Modern TLS settings
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;
    ssl_session_cache   shared:SSL:10m;
    ssl_session_timeout 1d;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options DENY always;
    add_header X-Content-Type-Options nosniff always;

    location / {
        proxy_pass         http://agripewebui:80;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        "upgrade";
        proxy_read_timeout 60s;
        proxy_buffering    off;
    }
}
```

The implementer must replace `CHANGE_ME_DOMAIN` with the actual domain (e.g., `agripeweb.com`).

The `Upgrade` / `Connection` headers are included for future WebSocket compatibility (e.g., MQTT over WebSocket).

### 2. `docker/nginx/nginx.http-only.conf`

Type: Nginx configuration — HTTP-only bootstrap (used before certificates exist).

Purpose: During the first-time Certbot certificate issuance, the HTTPS block cannot be active because the certificate files do not exist yet. This file serves as a temporary configuration that only listens on port 80 and exposes the ACME challenge path. The operator uses this file on first deploy, then switches to `nginx.conf` after certificates are issued.

Key structure:

```
server {
    listen 80;
    server_name CHANGE_ME_DOMAIN;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 200 "OK";
    }
}
```

### 3. `docker/nginx/README.md`

Wait — per project instructions, README files should not be created unless explicitly requested. SSL setup instructions will instead be included in the plan and in inline comments within the config files.

---

## Files to Modify

### `docker/docker-compose.yml`

**Change 1 — Remove host port binding from `agripewebui`.**

Current:
```yaml
agripewebui:
  ports:
    - "80:80"
```

Change to: remove the `ports` block entirely (or keep it commented out for local dev). The service becomes internal-only, accessible within `app-network` as `agripewebui:80`.

Note: For local development without the proxy container, keep the port binding. A recommended approach is to use a separate `docker-compose.override.yml` for local dev that re-adds the binding. This plan shows the production compose configuration.

**Change 2 — Remove host port binding from `agripewebapi` (optional but recommended for production).**

Current:
```yaml
agripewebapi:
  ports:
    - "8080:8080"
```

Change to: remove `ports` block. The API is already reachable internally via the UI's Nginx proxy. External clients only reach it via `https://DOMAIN/api/`. Flag: if direct API access from the host is needed for debugging, keep this binding gated behind an override file.

**Change 3 — Add `nginx-proxy` service.**

```yaml
nginx-proxy:
  container_name: nginx-proxy
  image: nginx:alpine
  ports:
    - "80:80"
    - "443:443"
  volumes:
    - ./nginx/nginx.conf:/etc/nginx/conf.d/default.conf:ro
    - certbot-webroot:/var/www/certbot:ro
    - letsencrypt-certs:/etc/letsencrypt:ro
  depends_on:
    - agripewebui
  networks:
    - app-network
  restart: unless-stopped
```

**Change 4 — Add `certbot` service.**

```yaml
certbot:
  container_name: certbot
  image: certbot/certbot:latest
  volumes:
    - certbot-webroot:/var/www/certbot
    - letsencrypt-certs:/etc/letsencrypt
  entrypoint: /bin/sh -c "trap exit TERM; while :; do certbot renew --webroot -w /var/www/certbot --quiet; sleep 12h & wait $${!}; done"
  networks:
    - app-network
  restart: unless-stopped
```

The Certbot container runs in renewal-loop mode (checks every 12 hours). Initial certificate issuance is done manually as a one-time step (see SSL Setup Instructions below).

**Change 5 — Add new named volumes.**

```yaml
volumes:
  mongo-data:
  mosquitto-data:
  certbot-webroot:
  letsencrypt-certs:
```

**Change 6 — Add `nginx-proxy` to `depends_on` of no other service** (it depends on `agripewebui`, not the other way around — no circular deps).

---

## SSL Setup Instructions (One-Time Bootstrap)

These steps must be executed on the VPS before the full `docker-compose up` with TLS. They are to be documented in `docker/nginx/nginx.conf` as a header comment block and given to the operator.

**Prerequisites on the Ubuntu VPS:**
- Docker and Docker Compose v2 installed.
- Domain DNS A record pointing to the VPS public IP (e.g., `agripeweb.com → 1.2.3.4`).
- Ports 80 and 443 open in the VPS firewall (`ufw allow 80` and `ufw allow 443`).

**Step 1 — Bootstrap with HTTP-only config.**

Copy `nginx.http-only.conf` to `nginx.conf` (or mount it instead). Start only the proxy:

```bash
docker compose -f docker/docker-compose.yml up -d nginx-proxy
```

**Step 2 — Obtain the initial certificate.**

```bash
docker compose -f docker/docker-compose.yml run --rm certbot \
  certonly --webroot \
  -w /var/www/certbot \
  -d agripeweb.com \
  --email admin@agripeweb.com \
  --agree-tos \
  --no-eff-email
```

Replace `agripeweb.com` and `admin@agripeweb.com` with actual values.

**Step 3 — Switch to the full TLS config.**

Replace the mounted `nginx.conf` with the HTTPS version (the `docker/nginx/nginx.conf` described above). Reload Nginx:

```bash
docker compose -f docker/docker-compose.yml exec nginx-proxy nginx -s reload
```

**Step 4 — Start remaining services.**

```bash
docker compose -f docker/docker-compose.yml up -d
```

**Certificate Renewal.**

The `certbot` service renews automatically every 12 hours. After renewal, Nginx must reload to pick up the new certificate. This can be done by:
- Adding a host cron job: `0 */12 * * * docker exec nginx-proxy nginx -s reload`
- Or: replacing the certbot entrypoint with a post-hook: `certbot renew --deploy-hook "docker exec nginx-proxy nginx -s reload"`

The recommended approach is the deploy-hook. The `certbot` service command becomes:

```yaml
entrypoint: /bin/sh -c "trap exit TERM; while :; do certbot renew --webroot -w /var/www/certbot --deploy-hook 'nginx -s reload' --quiet; sleep 12h & wait $${!}; done"
```

Note: the `--deploy-hook` here would attempt to run `nginx -s reload` inside the certbot container, which does not have Nginx. The correct production approach is to use a shared Docker socket or a cron job on the host. Flag this as an operational decision for the implementer — see Risks & Flags.

---

## Network Architecture After Change

```
Internet
  │
  ▼ 80 / 443 (host)
nginx-proxy (nginx:alpine)
  │  TLS terminates here
  │  HTTP → HTTPS redirect
  ▼ http://agripewebui:80 (internal, app-network)
agripewebui (nginx:alpine, multi-stage build)
  │  Serves Angular SPA for /
  │  Proxies /api/ → http://agripewebapi:8080 (internal)
  ▼
agripewebapi (dotnet aspnet:10.0)
  │
  ▼
db (mongo:8)
```

The `agripewebui` container's internal Nginx already sets the correct forwarding headers for the API. The outer proxy must pass `X-Forwarded-Proto: https` so that the .NET API's `Request.Scheme` reflects HTTPS correctly (important for OAuth redirect URIs and HSTS).

---

## Impact on Existing `agripewebui` Internal Nginx

The `nginx.conf.template` inside the `agripewebui` image (at `AgripeWebUI/nginx.conf.template`) proxies `/api/` using `proxy_set_header X-Forwarded-Proto $scheme`. When the outer Nginx proxy forwards a request on port 80 to `agripewebui`, `$scheme` inside the inner Nginx will be `http` even though the original client used HTTPS. This is expected and harmless for most functionality, but the outer Nginx must set `X-Forwarded-Proto: https` so the .NET API behind it reports the correct scheme.

No changes to `AgripeWebUI/nginx.conf.template` or `AgripeWebUI/entrypoint.sh` are required.

---

## CORS and OAuth Redirect URI Considerations

**CORS.** The API's `ApiConfig.cs` allows `agripeweb.com` in production (per CLAUDE.md). Once HTTPS is live, verify that the CORS policy origin includes `https://agripeweb.com` (with scheme), not just the bare domain.

**Google OAuth.** The Google OAuth `redirect_uri` registered in Google Cloud Console must include the HTTPS URL (e.g., `https://agripeweb.com/login/callback`). This is an external configuration step, not a code change, but the implementer must verify it.

**JWT Issuer/Audience.** If `JwtSettings` in `appsettings.json` contains an issuer URL, it must match the HTTPS domain. Check `AgripeWebAPI/appsettings.json`.

---

## Risks & Flags

**Risk 1 — Port 80 conflict.**
The current `docker-compose.yml` binds `"80:80"` on the `agripewebui` service. The new `nginx-proxy` service also binds `"80:80"`. Both cannot coexist. The implementer must remove the port binding from `agripewebui` before adding the proxy service, or the compose file will fail to start with a port-in-use error.

**Risk 2 — Certificate renewal reload.**
The Certbot container cannot directly signal the `nginx-proxy` container to reload after renewal. The safest production solution is a host cron job:
```
0 3 * * * docker exec nginx-proxy nginx -s reload
```
Flag: the implementer must decide between the cron approach and mounting the Docker socket into Certbot (which has security implications). The plan leaves this as an operational decision.

**Risk 3 — First-deploy chicken-and-egg.**
Nginx cannot start with the HTTPS config if the certificate files do not yet exist (Nginx will fail to find `fullchain.pem`). The bootstrap sequence (HTTP-only config first, obtain cert, then switch) is mandatory. Skipping it will cause `nginx-proxy` to crash-loop.

**Risk 4 — Domain name is a placeholder.**
Both `nginx.conf` and the Certbot command use `CHANGE_ME_DOMAIN`. The implementer must replace every occurrence with the real domain. A validation check (`grep -r CHANGE_ME docker/nginx/`) should be part of the deployment checklist.

**Risk 5 — Mobile service (`agripewebui-mobile`) also binds port 3000.**
The mobile container is exposed on `3000:80` and is a separate Angular app. If HTTPS access to the mobile app is also needed, the `nginx-proxy` config must add a second `server_name` block or a path-based route. The issue does not mention the mobile app, so this is out of scope but flagged.

**Risk 6 — AWS ECS deployment is unaffected.**
The Terraform ALB already handles TLS termination in the AWS deployment path. This Nginx proxy plan applies only to the direct VPS/Docker Compose deployment. The two deployment paths are independent and must not be conflated.

**Risk 7 — `agripewebapi` port 8080 still exposed.**
If the `agripewebapi` port binding is kept in the compose file, the API is reachable directly on `http://VPS_IP:8080` without HTTPS or authentication. For a production VPS, this port should be blocked at the firewall level (`ufw deny 8080`) or the binding removed from compose.

**Assumption:** The issue assumes a single domain (e.g., `agripeweb.com`). If `www.agripeweb.com` is also needed, the Certbot command and Nginx `server_name` must include both. Flag this for the operator.

---

## Summary of All Deliverables

| Deliverable | Path | Action |
|---|---|---|
| Reverse proxy Nginx config (HTTPS) | `docker/nginx/nginx.conf` | Create |
| Bootstrap Nginx config (HTTP-only) | `docker/nginx/nginx.http-only.conf` | Create |
| Updated Docker Compose | `docker/docker-compose.yml` | Modify |
| SSL setup instructions | This plan document | Reference |

---

## Verification

**Step 1 — Validate Compose file syntax.**
```bash
docker compose -f docker/docker-compose.yml config
```
Must exit 0 with no warnings about duplicate port bindings.

**Step 2 — Verify HTTP redirect.**
```bash
curl -v http://agripeweb.com/
```
Expected: `HTTP/1.1 301 Moved Permanently` with `Location: https://agripeweb.com/`.

**Step 3 — Verify HTTPS loads the Angular app.**
```bash
curl -v https://agripeweb.com/
```
Expected: `HTTP/2 200` with `<!DOCTYPE html>` body.

**Step 4 — Verify API proxy over HTTPS.**
```bash
curl -v https://agripeweb.com/api/v1/health
```
Expected: `200 OK` with JSON health payload.

**Step 5 — Verify forwarded headers reach the API.**
Add a temporary log line to `HealthController` (or check existing logs) to confirm `X-Forwarded-Proto: https` is present in the request.

**Step 6 — Verify TLS certificate.**
```bash
openssl s_client -connect agripeweb.com:443 -servername agripeweb.com </dev/null 2>&1 | grep "subject\|issuer\|Verify"
```
Expected: Let's Encrypt issuer, `Verify return code: 0 (ok)`.

**Step 7 — Verify certificate auto-renewal (dry run).**
```bash
docker compose -f docker/docker-compose.yml exec certbot certbot renew --dry-run
```
Expected: `Congratulations, all simulated renewals succeeded`.
