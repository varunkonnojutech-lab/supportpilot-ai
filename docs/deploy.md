# Deploying SupportPilot AI in production

A short, honest guide: SupportPilot is a two-container app with a SQLite
database. That is perfect for a single small server — read the scaling
notes at the bottom before you outgrow it.

## 1. Build the images

On the host (or in your CI, pushing to a registry):

```bash
docker compose build
docker compose up -d
```

This starts `api` (port 8080) and `web` (port 3000, nginx). In
production you normally don't publish those ports directly — put a
reverse proxy in front (next step).

## 2. Required environment

Copy `.env.example` to `.env` next to `docker-compose.yml` and set at
least:

| Variable | Why |
|---|---|
| `Jwt__Key` | **Required.** Long random string (32+ chars), e.g. `openssl rand -base64 48`. The compose dev default must never reach production — anyone who has seen this repo knows it. |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` (also turns off Swagger and the demo-friendly defaults). Decide deliberately whether you still want the seeded demo business; don't ship demo credentials you don't want. |
| `ConnectionStrings__DefaultConnection` | Defaults to `Data Source=/app/data/supportpilot.db` on the `api-data` volume. Change only if you move databases (see §5). |

Optional, only if you use them:

- **LLM answers:** `Ai__Provider=openai`, `OpenAi__ApiKey`, and as
  needed `OpenAi__BaseUrl`, `OpenAi__ChatModel`, `OpenAi__EmbeddingModel`.
  Without them the built-in offline provider serves answers.
- **Billing:** `Stripe__SecretKey`, `Stripe__PublishableKey`,
  `Stripe__WebhookSecret` — live keys for production (see §4).

Recreate the API after changing `.env`: `docker compose up -d`.

## 3. Put it behind HTTPS

The widget is loaded by your customers' websites and the dashboard
handles logins, so HTTPS is not optional.

- Point your domain (e.g. `support.example.com`) at the host.
- Terminate TLS in a reverse proxy — Caddy, nginx, Traefik, or a cloud
  load balancer — and proxy everything to the **web** container
  (`localhost:3000`). The web container already proxies `/api/` to the
  API, so the SPA, widget, demo page, and API stay same-origin and no
  CORS setup is needed.
- Customers then embed:
  `<script src="https://support.example.com/widget/supportpilot-widget.js" data-widget-key="..." async></script>`
- Keep port 8080 (the raw API) off the public internet, or at least
  behind the same proxy rules — all public traffic should arrive via
  the web origin so forwarded headers (`X-Forwarded-Proto`, etc.) are
  set consistently.

## 4. Stripe (live)

1. In the [Stripe dashboard](https://dashboard.stripe.com/), switch to
   live mode and create the Starter ($29/mo) and Pro ($99/mo) prices.
2. Set the live `Stripe__SecretKey` (`sk_live_...`),
   `Stripe__PublishableKey` (`pk_live_...`) in `.env`.
3. Add a webhook endpoint in Stripe pointing at:

   ```
   https://support.example.com/api/billing/webhook
   ```

   subscribed to the checkout/subscription events, and put its signing
   secret (`whsec_...`) in `Stripe__WebhookSecret`.
4. Recreate the containers and run one real upgrade to confirm the
   webhook flips the plan. Plan state changes **only** from signed
   webhooks — if plans don't move, check the webhook delivery log in
   Stripe first.

## 5. Data: back up SQLite, or move databases

All tenant data — accounts, knowledge chunks, conversations — lives in
one SQLite file on the `api-data` Docker volume.

**Backups:** copy the file out on a schedule, while the API is stopped
or using SQLite's backup command (a raw copy of a live file plus its
`-wal` can be inconsistent):

```bash
docker compose stop api
docker run --rm -v supportpilot-ai_api-data:/data -v "$PWD":/backup alpine \
  cp /data/supportpilot.db /backup/supportpilot-backup.db
docker compose start api
```

(Your volume name may differ — check with `docker volume ls`.)

**Moving up:** point `ConnectionStrings__DefaultConnection` at SQL
Server or Postgres instead. The app uses EF Core, so the connection
string is the main lever — but you also need the matching EF Core
provider package and a migration/EnsureCreated pass for that database;
treat it as a small project, not a config flip, and test tenant
isolation against the new database before switching traffic.

## 6. Scaling notes (the SQLite caveat)

- SQLite + a single API container is a deliberate v1 trade-off: simple,
  cheap, and reliable for a small business's support volume.
- It means **one API instance**: don't run multiple `api` replicas
  against the same SQLite file — concurrent writers will contend and
  the file must live on local/volume storage, not shared network
  mounts.
- When you need replicas, move to SQL Server/Postgres (§5) first, make
  the API stateless (it already keeps state in the database, not in
  memory), then scale the `api` service behind the proxy. The `web`
  container is static files and scales freely at any point.
- Watch the two numbers that tell you it's time: monthly chats per
  tenant approaching Pro limits across many tenants, and API response
  times on the chat endpoint under load.
