# Kodis API

Backend for [kod.is](https://kod.is) — shareable notebooks made of notes.
ASP.NET Core 8 · SQLite · EF Core · Google sign-in · JWT.

Live at **https://kodisapi.kod.is**.

## Running locally

Prerequisites: the .NET 8 SDK. The database is a SQLite file, so there is
nothing to install or run alongside it.

```bash
dotnet restore
dotnet tool restore
```

Secrets are **not** in `appsettings.json`. Provide them through user-secrets:

```bash
cd KodisApi
dotnet user-secrets set "JwtSettings:Secret" "$(openssl rand -base64 64)"
dotnet user-secrets set "ConnectionStrings:ApplicationDbContext" "Data Source=kodis.db"
dotnet user-secrets set "Google:ClientId" "your-client-id.apps.googleusercontent.com"
```

Then:

```bash
dotnet run --project KodisApi          # Swagger UI at /swagger
dotnet test                            # unit tests
```

In `Development`, `Database:MigrateOnStartup` defaults to `true`, so the schema
is created on first run.

## Configuration

Every setting can be overridden with an environment variable using the `__`
separator (`JwtSettings__Secret`, `ConnectionStrings__ApplicationDbContext`, …).
All sections are validated at startup — a missing or malformed value fails the
boot rather than surfacing later as a 500.

| Key | Meaning | Default |
| --- | --- | --- |
| `ConnectionStrings:ApplicationDbContext` | SQLite connection string, e.g. `Data Source=/var/lib/kodisapi/kodis.db` | *(required)* |
| `JwtSettings:Secret` | HMAC-SHA256 signing key, ≥32 bytes | *(required)* |
| `JwtSettings:AccessExpirationTimeInMinutes` | Access token lifetime | `15` |
| `JwtSettings:RefreshExpirationTimeInMinutes` | Refresh token lifetime | `20160` (14 days) |
| `JwtSettings:ClockSkewInSeconds` | Tolerance for client clock drift | `60` |
| `Google:ClientId` | OAuth client id that tokens must be issued to | *(required)* |
| `Sqids:Alphabet` | Alphabet used to obfuscate notebook slugs | *(required)* |
| `Cors:AllowedOrigins` | Allowed browser origins (array) | `["https://kod.is"]` |
| `Notebook:AnonymousLifetimeInHours` | How long an anonymous notebook lives | `24` |
| `Notebook:MaxNoteContentLength` | Per-note character cap | `100000` |
| `Database:MigrateOnStartup` | Apply migrations on boot | `true` in Development |
| `Hosting:UseHttpsRedirection` | Only enable when this process terminates TLS | `false` |
| `DataProtection:KeyRingPath` | Where to persist data-protection keys | *(ephemeral if unset)* |

### Deployment

The VPS runs the API as a systemd service; Caddy terminates TLS and reverse
proxies `kodisapi.kod.is` to it. Nothing is shared with the other projects on
that box, which run under PM2.

| | |
| --- | --- |
| Binary | `/opt/kodisapi` (self-contained, no .NET runtime needed) |
| Database + keys | `/var/lib/kodisapi` |
| Secrets | `/etc/kodisapi/kodisapi.env` (`0640`, owned by `root:kodisapi`) |
| Service | `kodisapi.service`, runs as the unprivileged `kodisapi` user |
| Port | `127.0.0.1:3003`, reachable only through Caddy |

To ship a new version:

```bash
./scripts/deploy.sh
```

That runs the tests, publishes for `linux-x64`, replaces `/opt/kodisapi`,
restarts the service and checks `/health`. Secrets on the server are never
touched.

```bash
ssh vps systemctl status kodisapi
ssh vps journalctl -u kodisapi -f
```

`Database:MigrateOnStartup` is on in production here: with SQLite and a single
instance there is no second process to race, and it keeps the schema in step
with the binary that was just deployed.

#### Backups

The whole database is one file. Back it up with SQLite's own command so a
concurrent write cannot produce a torn copy:

```bash
ssh vps sqlite3 /var/lib/kodisapi/kodis.db ".backup '/root/kodis-backup.db'"
```

## Authentication

Two Google flows are supported, both returning the same token pair:

| Endpoint | Credential | How it is verified |
| --- | --- | --- |
| `POST /api/Account/GoogleSigninByGoogleOneTap` | One Tap ID token | Signature + audience via `GoogleJsonWebSignature` |
| `POST /api/Account/GoogleSigninByTokenResponse` | OAuth access token | Google `tokeninfo`, whose `aud` must equal `Google:ClientId` |

Both require a **verified** email address. Accounts are matched on the provider
subject (`sub`), falling back to email only for rows created before subjects
were indexed.

### Tokens

Access and refresh tokens are signed with the same key but carry a `token_type`
claim (`access` / `refresh`) that is checked on every use, so a long-lived
refresh token cannot be presented as a bearer credential.

Refresh tokens **rotate**: each call to `POST /api/Account/RefreshLogin` issues a
new one and invalidates the old. Presenting an already-rotated token is treated
as theft and revokes the whole session.

`POST /api/Account/Logout` revokes the current session immediately.

## Notebooks

Notebooks are shared by link, so reads and writes are anonymous by default.
Authentication is optional and only changes what happens for notebooks that
have an owner.

| Endpoint | Access rule |
| --- | --- |
| `GET /api/Notebook/{slug}` | Public, unless a view password is set |
| `POST /api/Notebook/Create` | Public; claimed by the caller when signed in |
| `POST /api/Notebook/Update/{slug}` | Owner always; otherwise edit password, else open only for ownerless notebooks |

Passwords are supplied in the `X-Notebook-Password` header (kept out of URLs so
they do not land in access logs) and stored as PBKDF2-HMAC-SHA256 hashes.

Anonymous notebooks expire after `Notebook:AnonymousLifetimeInHours` and are
physically deleted by a background job once past the grace period. A user's
`@username` notebook never expires.

## Notes on SQLite

SQLite allows a single writer at a time, so the API must run as **one**
instance and its database file must never live on a network volume. That is the
constraint the deployment above is built around.

Dates are stored as UTC ticks (`INTEGER`) rather than as text. SQLite has no
native `DateTimeOffset`, and EF Core refuses to translate comparisons against
the text form — which silently breaks any query that filters on a date. The
conversion is declared once in `ApplicationDbContext.ConfigureConventions`.
