# Kodis API

Backend for [kod.is](https://kod.is) — shareable notebooks made of notes.
ASP.NET Core 8 · PostgreSQL · EF Core · Google sign-in · JWT.

## Running locally

Prerequisites: .NET 8 SDK and a PostgreSQL instance.

```bash
dotnet restore
dotnet tool restore
```

Secrets are **not** in `appsettings.json`. Provide them through user-secrets:

```bash
cd KodisApi
dotnet user-secrets set "JwtSettings:Secret" "$(openssl rand -base64 64)"
dotnet user-secrets set "ConnectionStrings:ApplicationDbContext" "Host=localhost;Port=5432;Database=KodisApiDb;Username=postgres;Password=..."
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
| `ConnectionStrings:ApplicationDbContext` | PostgreSQL connection string | *(required)* |
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

### Deployment

```bash
cd KodisApi
cp .env.example .env      # fill in real values
docker compose up -d --build
```

The API listens on `8080` inside the container (mapped to `3333`) and is
expected to sit behind a TLS-terminating reverse proxy. `X-Forwarded-For` and
`X-Forwarded-Proto` are honoured, which is what makes per-IP rate limiting
meaningful.

Migrations are **not** applied on startup in Production. Run them from the
release pipeline:

```bash
dotnet ef database update --project KodisApi
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

## Upgrading an existing database

The `SecurityHardening` migration adds unique indexes and therefore normalises
existing rows first: empty `UserName`/`Sub` become `NULL`, slugless notebooks
get an `orphan-<id>` placeholder, duplicate main notebooks lose the flag, and
**all login sessions are deleted** (the signing key is rotated as part of this
change, so every token in circulation was void anyway — clients simply sign in
again).

The migration will fail loudly if two users share an email address; that
duplicate has to be resolved by hand rather than silently merged.
