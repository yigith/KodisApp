# Kodis API

Backend for **[kod.is](https://kod.is)** — a notepad you share with a link. A visitor
types notes in the browser, posts them, and gets back a short URL such as
`kod.is/a3Bq7D` that anyone can open for the next 24 hours. Signing in with Google
turns that into a permanent notebook at `kod.is/@username`. This repository is the
ASP.NET Core 8 Web API behind it, running at
**[kodisapi.kod.is](https://kodisapi.kod.is)**; the React + Vite client lives in
[yigith/kodis](https://github.com/yigith/kodis) and is a pure client — every rule
described below is enforced here, on the server.

---

## Table of contents

- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [Data model](#data-model)
- [API reference](#api-reference)
- [Authentication](#authentication)
- [Expiry and cleanup](#expiry-and-cleanup)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Tests](#tests)

---

## Features

**Notebooks and slugs.** A notebook's public handle is a [Sqids](https://sqids.org)
encoding of its integer primary key over a shuffled alphabet, with a minimum length
of 8. There is no random generation and therefore no uniqueness retry loop: the slug
is a pure function of the id, so it is unique because the id is. Because the id is
only known after the row is inserted, creation runs as an insert followed by an
update inside one transaction — otherwise a crash in between would leave a notebook
with an empty slug and no way to reach it. The shuffled alphabet is what stops
`a3Bq7D` from being trivially decoded back to "notebook #4" and its neighbours from
being enumerated.

**Anonymous expiry.** Every notebook is created with an `ExpireDate` of
`now + Notebook:AnonymousLifetimeInHours` (24 by default). Expiry is enforced on
read, not by a job: `Notebook.IsAccessible(now)` gates every lookup, so a notebook
stops resolving the moment it expires even if nothing has swept the table yet. A
signed-in user's `@username` notebook is created with `ExpireDate =
DateTimeOffset.MaxValue` and never expires.

**Username uniqueness.** Usernames are normalised (trimmed, lower-cased) in the DTO
setter, validated against `^[a-zA-Z][a-zA-Z0-9]+$` at 5–20 characters, and backed by
a *filtered* unique index — `UNIQUE(UserName) WHERE UserName IS NOT NULL` — so that
the many users who have not picked one yet do not all collide on `NULL`. The
pre-check query is an optimisation for the common case; the index is the authority,
and a `DbUpdateException` from two requests racing for the same handle is translated
into a `409 Conflict` rather than a 500.

**Google credential verification.** Two flows, one result. One Tap ID tokens are
verified by `GoogleJsonWebSignature.ValidateAsync` with the configured client id as
the expected audience. OAuth access tokens are opaque and carry no verifiable
claims, so they are sent to Google's `tokeninfo` endpoint — the only thing that
reveals which client a token was issued to — and the returned `aud` is compared to
`Google:ClientId`. Without that comparison, an access token minted for *any* Google
application would be accepted here. Both flows require a verified email address.

**JWT issuance.** Sign-in returns an access/refresh pair. Access tokens carry the
user id, session id, email and display profile; refresh tokens carry only a session
id, a token id and a type.

**Refresh token rotation with replay detection.** Each session stores the `jti` of
the one refresh token currently valid for it. Every refresh mints a new `jti` and
overwrites the stored one, so the previous token is dead. Presenting a refresh token
whose `jti` no longer matches means the token was rotated away and is being replayed
— that is treated as theft and revokes the whole session.

**Rate limiting.** Fixed-window limiters partitioned by user id when signed in and by
client IP otherwise, with separate budgets for authentication, notebook reads and
notebook writes, plus a global backstop. Rejections return `429` with `Retry-After`,
which is added to the CORS exposed-headers list so the browser client can actually
read it.

**Optional notebook passwords.** A notebook can carry a view password, an edit
password, or both, hashed with PBKDF2-HMAC-SHA256 at 210,000 iterations and compared
in constant time. They travel in an `X-Notebook-Password` header rather than the URL,
which keeps them out of access logs, browser history and `Referer` headers.

**Operational hardening.** Every configuration section is bound to a typed options
class with data annotations and `ValidateOnStart()`, so a missing signing key fails
the boot instead of surfacing as a confusing 500 on the first sign-in. All errors are
RFC 7807 problem details, and unhandled exceptions never leak their message to the
caller. `alg` is pinned to HS256, so no token can arrive claiming `"alg": "none"`.
The token type is checked on every bearer request, so a 14-day refresh token cannot
be used as an access token.

---

## Tech stack

| | |
| --- | --- |
| Runtime | .NET 8 (`net8.0`), C# with nullable reference types and implicit usings |
| Web | ASP.NET Core 8 MVC controllers, built-in rate limiting, `IExceptionHandler` |
| Data | Entity Framework Core 8 over **SQLite** |
| Auth | JWT bearer (HS256), Google ID-token and OAuth access-token verification |
| Tests | xUnit against a real in-memory SQLite database |
| Hosting | systemd on a Linux VPS behind Caddy; a Dockerfile and compose file are also provided |

| NuGet package | Version | Why |
| --- | --- | --- |
| `Microsoft.EntityFrameworkCore` | 8.0.18 | ORM |
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.18 | Database provider |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.18 | Migration tooling (build-time only) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.18 | Bearer token validation |
| `Google.Apis.Auth` | 1.68.0 | Google ID token signature + audience verification |
| `Sqids` | 3.1.0 | Notebook slug encoding |
| `Swashbuckle.AspNetCore` | 6.9.0 | OpenAPI document and Swagger UI |
| `Microsoft.AspNetCore.OpenApi` | 8.0.18 | Endpoint metadata for the OpenAPI document |
| `Microsoft.Extensions.Hosting.Systemd` | 8.0.1 | `Type=notify` readiness and journald log levels |

Test project: `xunit` 2.9.2, `Microsoft.NET.Test.Sdk` 17.11.1, `coverlet.collector`
6.0.2. EF tooling is pinned as a local tool (`dotnet-ef` 8.0.18) in
`KodisApi/.config/dotnet-tools.json`.

### Why SQLite

The workload is a few thousand short-lived text rows with a read-heavy access pattern
and no analytical queries. A separate database server would add a process to run, a
backup story, a connection pool and a failure mode, in exchange for nothing this
application needs. The whole database is one file, which makes backup a single
command and local development a `git clone` away.

The cost is real and worth stating: SQLite permits **one writer at a time**, so the
API must run as a single instance, and the database file must never sit on a network
volume. That constraint is what the deployment is designed around. Moving to
PostgreSQL would mean changing the provider registration and regenerating the
migration — the model itself is provider-agnostic, and the cleanup query deliberately
avoids SQLite-only constructs for that reason.

---

## Architecture

```
KodisApp.sln
├── KodisApi/
│   ├── Program.cs                  Composition root: options, DI, middleware order
│   ├── Controllers/                HTTP surface only — bind, delegate, map to DTO
│   │   ├── AccountController.cs
│   │   └── NotebookController.cs
│   ├── Services/                   Business rules; no HTTP types
│   │   ├── NotebookService.cs      Slugs, expiry, access rules, note merging
│   │   ├── JwtService.cs           Session lifecycle, issuance, rotation, revocation
│   │   ├── GoogleAuthService.cs    Credential verification for both Google flows
│   │   ├── NotebookPasswordHasher.cs
│   │   └── DataCleanupService.cs   The purge query, isolated so it is testable
│   ├── Data/                       EF Core entities, DbContext, migrations
│   ├── Dtos/                       Request/response contracts
│   ├── Settings/                   Typed, validated configuration sections
│   ├── Extensions/                 Entity → DTO mapping, claims helpers
│   ├── Exceptions/                 ApiException hierarchy → HTTP status codes
│   └── Infrastructure/             Cross-cutting: exception handler, token validation
│                                   parameters, rate limit policies, cleanup host
└── KodisApi.Tests/                 xUnit tests over the real services
```

### Request flow

```
HTTP request
  → ForwardedHeaders        recover real scheme + client IP from behind Caddy
  → ExceptionHandler        unhandled exception → RFC 7807 problem details
  → RateLimiter             per-user or per-IP fixed window → 429 + Retry-After
  → CORS
  → Authentication          JWT bearer; rejects anything that is not token_type=access
  → Authorization
  → Controller              model binding + data annotation validation
      → Service             business rules; throws ApiException for expected failures
          → DbContext       EF Core → SQLite
      ← entity
  ← DTO (camelCase JSON)
```

The middleware order is not incidental. `UseForwardedHeaders` runs first because both
the rate limiter's IP partition and the HTTPS redirect read values it restores —
behind a reverse proxy, every request otherwise looks like it came from `127.0.0.1`
over plain HTTP, which would collapse every anonymous caller into a single shared
rate-limit bucket. `UseExceptionHandler` wraps everything after it, so a failure
inside any later component still produces a problem-details body rather than an empty
500. The rate limiter sits *before* authentication so that the cost of rejecting an
abusive caller stays low: turning away a flood of requests should not require
validating a signature first.

### Layering

Controllers are deliberately thin. They read the route, body and password header,
call one service method, and map the result to a DTO — they contain no `if` that
decides who may do what. Services own the rules and know nothing about HTTP: they
signal failure by throwing `NotFoundException`, `UnauthorizedException`,
`ForbiddenException`, `ConflictException` or `BadRequestException`, each of which
carries its own status code and title. `GlobalExceptionHandler` translates those into
problem details and lets nothing else through — any exception that is *not* an
`ApiException` is logged with its stack trace server-side and reported as a bare
"An unexpected error occurred", so an EF or SQLite message can never reach a client.

The practical benefit is that authorization lives in exactly one place per operation.
`NotebookService.AuthorizeEdit` is the single answer to "may this caller edit this
notebook", used identically whether the caller is anonymous, password-bearing or
signed in.

**A deliberate omission:** there is no repository layer over `DbContext`. `DbSet<T>`
is already a repository and `DbContext` is already a unit of work; wrapping them would
add indirection without adding a seam, since the tests run against a real SQLite
database anyway. `TimeProvider` is injected everywhere instead of calling
`DateTimeOffset.UtcNow`, which is the seam that actually matters — it lets the tests
advance the clock past a 24-hour expiry or a 14-day refresh window instantly.

### Dates in SQLite

SQLite has no native `DateTimeOffset`. EF Core stores it as text and then refuses to
translate comparisons against it, which silently turns every date filter into either
a client-side evaluation or a runtime failure — and this application filters on dates
constantly. A single `ValueConverter` registered in `ConfigureConventions` stores
every `DateTimeOffset` and `DateTimeOffset?` in the model as UTC ticks (`INTEGER`).
Comparisons become integer arithmetic: translatable, correctly ordered,
index-friendly, and lossless.

---

## Data model

```
NotebookUser 1 ──── * LoginSession        cascade delete
     │
     │ 1
     │
     * (0..1 main)
  Notebook 1 ──── * Note                  cascade delete
```

**NotebookUser** — one row per signed-in identity. `Id` is a GUID string. `Sub` is the
provider's stable subject; `LoginMethod` is an enum with room for other providers,
though only `Google` is wired up. `UserName` is null until the user picks one.
Indexes: unique on `Email`; unique on `UserName` filtered to non-null; unique on
`(LoginMethod, Sub)` filtered to non-null `Sub`.

**Notebook** — `Id` is an autoincrement integer, and it is what the slug encodes.
`Slug` is unique and is either a Sqids string or `@username`. `IsMain` marks a user's
permanent notebook, enforced one-per-user by a unique index on `NotebookUserId`
filtered to `IsMain = TRUE`. `ExpireDate` is indexed because the cleanup job scans on
it. `PasswordSalt` is shared by both password hashes; it is null exactly when the
notebook has no password at all. `NotebookUserId` is nullable — an anonymous notebook
has no owner — and uses `OnDelete(SetNull)`, so deleting an account does not take
notebooks down with it.

**Note** — `Id` is a GUID string, so the client can address a note without a round
trip. Cascade-deleted with its notebook. Title is capped at 50 characters, content at
`Notebook:MaxNoteContentLength`.

**LoginSession** — one row per login, not per token. `RefreshTokenId` holds the `jti`
of the single refresh token currently valid for the session and is what makes rotation
and replay detection possible. `RevokedDate` is set on logout or on detected replay.
`Expires` is indexed for the cleanup scan. `IsActive(now)` — not revoked and not
expired — is checked on every refresh.

Two fields are carried in the schema but not read by any current code path:
`Notebook.SecurityToken` and `Note.IsPrivate`. They are noted here rather than
described as features.

---

## API reference

Base URL `https://kodisapi.kod.is`. All request and response bodies are JSON with
camelCase property names (ASP.NET Core's default policy); binding is
case-insensitive, as is routing.

### Endpoints

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/Notebook/{slug}` | Optional | Read a notebook and its notes. A view password may be required. |
| `POST` | `/api/Notebook/Create` | Optional | Create a notebook; claimed by the caller when signed in. |
| `POST` | `/api/Notebook/Update/{slug}` | Optional | Merge changes into the notes of a notebook. |
| `POST` | `/api/Account/GoogleSigninByGoogleOneTap` | No | Exchange a Google One Tap ID token for a token pair. |
| `POST` | `/api/Account/GoogleSigninByTokenResponse` | No | Exchange a Google OAuth access token for a token pair. |
| `POST` | `/api/Account/RefreshLogin` | No | Rotate a refresh token into a new pair. |
| `POST` | `/api/Account/Check` | **Yes** | Probe whether the access token is still valid. |
| `POST` | `/api/Account/SetUsername` | **Yes** | Claim a username and create the `@username` notebook. |
| `POST` | `/api/Account/Logout` | **Yes** | Revoke the current session. |
| `GET` | `/health` | No | Liveness probe used by the deploy script and the Docker healthcheck. |

"Optional" means the endpoint accepts anonymous callers, but a valid
`Authorization: Bearer` header changes the outcome — it identifies an owner.

In `Development`, Swagger UI is served at `/swagger` and `/` redirects to it. In
`Production` both are off and `/` is a 404 — this is an API, not a site.

### `GET /api/Notebook/{slug}`

`{slug}` is either a Sqids handle (`a3Bq7D`) or `@username`. Send
`X-Notebook-Password` if the notebook is view-protected; the owner never needs it.

```http
GET /api/Notebook/a3Bq7D HTTP/1.1
X-Notebook-Password: hunter2
```

```json
{
  "slug": "a3Bq7D",
  "isViewProtected": true,
  "isEditProtected": false,
  "expireDate": "2026-08-23T09:14:22.117+00:00",
  "notes": [
    {
      "id": "0f3c9d1a6b7e4f2c8a5d0e1b2c3d4e5f",
      "title": "shopping",
      "content": "milk\neggs",
      "createdDate": "2026-08-22T09:14:22.117+00:00",
      "modifiedDate": "2026-08-22T09:31:05.882+00:00"
    }
  ]
}
```

Notes come back ordered by `createdDate`. A missing, soft-deleted or expired notebook
is a `404` — expired and never-existed are deliberately indistinguishable. A wrong or
absent view password is a `401`.

### `POST /api/Notebook/Create`

Notes are supplied as a title → content map. `viewPassword` and `editPassword` are
optional, 4–128 characters each.

```json
{
  "notes": { "shopping": "milk\neggs", "todo": "call the bank" },
  "editPassword": "hunter2"
}
```

Returns `201 Created` with a `Location` header pointing at the new notebook, and the
same body shape as `GET`. Signing the request claims the notebook for the caller.

`400` if a title is blank or over 50 characters, if any content exceeds
`Notebook:MaxNoteContentLength`, or if the notebook would hold more than
`Notebook:MaxNotesPerNotebook` notes.

### `POST /api/Notebook/Update/{slug}`

A merge, not a wholesale replacement. Each entry is matched by `id`: an entry with no
`id` is a new note, an entry with `isDeleted: true` removes one, and anything else
updates in place. Notes the client does not mention are left alone.

```json
{
  "notes": [
    { "id": "0f3c9d1a6b7e4f2c8a5d0e1b2c3d4e5f", "title": "shopping", "content": "milk\neggs\nbread" },
    { "title": "new tab", "content": "" },
    { "id": "7a1b2c3d4e5f60718293a4b5c6d7e8f9", "isDeleted": true }
  ]
}
```

An `id` that does not belong to this notebook is a `404` rather than being silently
created as a new note — that keeps a stale client from resurrecting a note it should
have forgotten, and stops ids from leaking across notebooks.

Who may edit, in order:

1. The signed-in owner — always, no password needed.
2. Otherwise, if an edit password is set, it must match (`401` if not).
3. Otherwise, if the notebook has an owner, nobody else may edit it (`403`).
4. Otherwise it is an anonymous notebook: whoever knows the slug may edit, subject to
   the view password if one is set.

### `POST /api/Account/GoogleSigninByGoogleOneTap`

The body is Google's One Tap `CredentialResponse`, forwarded verbatim by the client.

```json
{ "credential": "eyJhbGciOiJSUzI1NiIsImtpZCI6...", "select_by": "btn" }
```

### `POST /api/Account/GoogleSigninByTokenResponse`

The body is Google's OAuth token response, again forwarded verbatim — which is why
these DTO properties keep Google's snake_case shape rather than being renamed. Only
`access_token` is used.

```json
{ "access_token": "ya29.a0Ad52...", "expires_in": 3599, "token_type": "Bearer" }
```

Both sign-in endpoints return the same body:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2026-08-22T09:29:22.117+00:00",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshTokenExpiresAt": "2026-09-05T09:14:22.117+00:00"
}
```

`401` if the credential fails verification, was issued to a different Google
application, or belongs to an account whose email address is not verified.

### `POST /api/Account/RefreshLogin`

```json
{ "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }
```

Returns a fresh token pair; the presented refresh token is invalid from that moment
on. `401` covers every failure — expired, malformed, revoked, an access token
presented here by mistake, or a replayed token (which additionally kills the session).
The reasons are deliberately not distinguished to the caller; the server log separates
them.

### `POST /api/Account/SetUsername`

```json
{ "username": "yigit" }
```

The value is trimmed and lower-cased before validation: 5–20 characters, must start
with a letter, letters and digits only. On success the `@username` notebook is created
(or, if the user is renaming, its slug is moved) and a **new token pair** is returned —
the username is a claim inside the access token, so the old one would still show the
old value.

Setting the username you already own is idempotent and succeeds. `409` if it is taken,
`400` if it fails validation.

### `POST /api/Account/Check`

Returns `200` with `{ "userId": "…" }` for a valid access token, `401` otherwise. The
client uses it on load to decide whether to render as signed in.

### `POST /api/Account/Logout`

`204 No Content`. Revokes the session, so its refresh token stops working immediately
(see [Rotation and replay detection](#rotation-and-replay-detection) for what this does
and does not do to the access token).

### Error format

Every error is an RFC 7807 problem details document, sent as
`application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Notebook not found."
}
```

Model validation failures produce ASP.NET Core's standard `ValidationProblemDetails`,
which adds a per-field `errors` object:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Username": ["Username must start with a letter and contain only letters and digits."]
  }
}
```

`detail` is safe to show to a user for 4xx responses — those messages are written
deliberately. For `500` it is always the same generic string; the real exception is in
the server log only.

| Status | When |
| --- | --- |
| `400` | Validation failure, or a notebook/note limit exceeded |
| `401` | Missing or invalid token; wrong or missing notebook password |
| `403` | Editing someone else's notebook when no edit password is set |
| `404` | Notebook missing, expired or soft-deleted; a note id from elsewhere |
| `409` | Username already taken |
| `429` | Rate limit exceeded |
| `500` | Unexpected — detail is never disclosed |

### Rate limiting

Fixed windows, partitioned by user id when the caller is authenticated and by client
IP otherwise, so one signed-in user cannot spend another's budget and a shared NAT
does not throttle a logged-in user.

| Policy | Endpoints | Limit |
| --- | --- | --- |
| `auth` | Both sign-in endpoints and `RefreshLogin` | 20 requests / 5 minutes |
| `notebook-read` | `GET /api/Notebook/{slug}` | 120 requests / minute |
| `notebook-write` | `Create`, `Update` | 60 requests / minute |
| *(global backstop)* | Everything else | 300 requests / minute |

Read is limited harder than it might look to need, because a slug is a short string:
the limiter is what turns "guess a notebook id" from a scripted sweep into something
impractical. Auth is limited hardest because sign-in and refresh are cheap to call and
expensive to brute-force.

Exceeding a limit returns `429` with `Retry-After` in seconds:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 47
```

`Retry-After` is in the CORS exposed-headers list, so the browser client can read it
and back off precisely rather than guessing.

---

## Authentication

### Sign-in

```
Browser                        API                                   Google
   │  Google credential         │                                      │
   ├───────────────────────────>│                                      │
   │                            │  One Tap: verify RS256 signature      │
   │                            │  against Google's keys; audience      │
   │                            │  must equal Google:ClientId           │
   │                            │                                      │
   │                            │  OAuth: GET /tokeninfo ──────────────>│
   │                            │<───────────────── { aud, sub, email } │
   │                            │  aud must equal Google:ClientId       │
   │                            │  then GET /userinfo for the profile   │
   │                            │  (best-effort; identity already set)  │
   │                            │                                      │
   │                            │  require email_verified               │
   │                            │  find user by (LoginMethod, sub),     │
   │                            │  else by email; create or update      │
   │                            │  insert LoginSession                  │
   │  access + refresh token    │                                      │
   │<───────────────────────────┤                                      │
```

The One Tap path never leaves the process: an ID token is a signed JWT, so verifying
the signature against Google's published keys is sufficient. An OAuth access token is
an opaque string with nothing to verify locally, so it requires the network round trip
to `tokeninfo` — and that call is not optional. It is the only way to learn which
client the token was issued to, and skipping it would let a token obtained by any other
Google application be redeemed here for a kod.is session.

Account lookup is by `(LoginMethod, Sub)` **first**, falling back to email only for
rows created before subjects were indexed. Matching on email alone is the classic
account-takeover hole: a provider that does not verify addresses could hand out an
existing account to whoever controls that mailbox. The `email_verified` check closes
the same door from the other side.

### Tokens

Both tokens are HS256 JWTs signed with `JwtSettings:Secret`, issued and validated
against `kod.is` as issuer and audience.

| | Access token | Refresh token |
| --- | --- | --- |
| Lifetime | `AccessExpirationTimeInMinutes`, 15 by default | `RefreshExpirationTimeInMinutes`, 20160 (14 days) |
| `sub` | User id | — |
| `sid` | Session id | Session id |
| `jti` | Random per token | The session's current `RefreshTokenId` |
| `token_type` | `access` | `refresh` |
| Other | `email`, `name`, `given_name`, `family_name`, `username`, `picture`, `locale` | — |

The two token kinds share one signing key, which makes the `token_type` claim
load-bearing rather than decorative. Two checks enforce it: the bearer handler's
`OnTokenValidated` event fails any principal that is not `token_type=access`, and
`RefreshAsync` rejects anything that is not `token_type=refresh`. Without the first, a
14-day refresh token would work as a bearer credential; without the second, a
15-minute access token could be used to mint fresh long-lived ones indefinitely.

Validation parameters are built in exactly one place —
`JwtTokenValidation.BuildParameters` — and consumed by both the bearer middleware and
the refresh endpoint, so the two can never drift apart. `ValidAlgorithms` is pinned to
HS256 so nothing can arrive claiming `"alg": "none"`, and the same injected
`TimeProvider` stamps and checks lifetimes, so issuing and validation cannot disagree
about what "now" is.

The access token's profile claims exist so the client can render the account menu
without an extra request. They are display data. Authorization decisions on the server
read `sub` and `sid`, never the profile.

### Rotation and replay detection

A `LoginSession` row stores `RefreshTokenId`, the `jti` of the one refresh token
currently valid for that session.

```
refresh #1 (jti = A)   session.RefreshTokenId = A   ──> match, rotate
                       session.RefreshTokenId = B       new pair, jti = B

refresh #2 (jti = A)   session.RefreshTokenId = B   ──> MISMATCH
                       token A was valid once and has been rotated away.
                       session.RevokedDate = now        401, session dead
```

A mismatch is not a benign duplicate: token A was issued, used, and superseded, so
someone is presenting a copy. Whether the copy is in an attacker's hands or the
legitimate client's, the honest conclusion is that the token leaked, and the session is
revoked. The legitimate user is forced to sign in again — a real cost, accepted because
the alternative is an attacker holding a valid refresh token for up to 14 days.

Each successful rotation also slides `Expires` forward by the full refresh lifetime, so
an actively used session stays alive while an abandoned one ages out.

`Logout` sets `RevokedDate`, and because `IsActive(now)` is checked on every refresh and
re-issue, the session can no longer produce tokens. Note the asymmetry this leaves: a
revoked session's *access* token remains cryptographically valid until it expires — at
most 15 minutes — because bearer validation is stateless by design. Closing that window
entirely would mean a database read on every authenticated request. Those 15 minutes are
the price of not doing that, and they are why the access lifetime is short.

---

## Expiry and cleanup

Two mechanisms, doing different jobs.

**Lazy expiry** is what users experience. `Notebook.IsAccessible(now)` — not deleted and
`ExpireDate > now` — is evaluated inside `GetForReadAsync` and `UpdateAsync`, so a
notebook becomes a `404` the instant it expires, regardless of what any background job
has or has not done. Correctness does not depend on the sweeper running.

**`ExpiredDataCleanupService`**, a `BackgroundService` driven by a `PeriodicTimer` every
`Notebook:CleanupIntervalInMinutes` (60), is what keeps the tables from growing without
bound. Each pass computes `cutoff = now - Notebook:CleanupGraceInHours` (24) and
permanently deletes:

- notebooks where `!IsMain && (IsDeleted || ExpireDate < cutoff)` — notes go with them
  through the cascade;
- login sessions where `Expires < cutoff`, or revoked before the cutoff.

So an anonymous notebook stops resolving at 24 hours and is physically removed at
roughly 48. The grace period exists so that "gone from the UI" and "gone from disk" are
separate events — if a lifetime is misconfigured or a bug expires something early, there
is a day to notice before the data is unrecoverable. Main (`@username`) notebooks are
excluded from the sweep entirely, in addition to having an effectively infinite
`ExpireDate` — belt and braces, because accidentally deleting a signed-in user's
permanent notebook is the one unrecoverable mistake here.

The purge selects candidate ids and then deletes by primary key, rather than issuing one
set-based `ExecuteDelete` with the date predicate inline. SQLite cannot translate a
`DateTimeOffset` comparison inside a `DELETE`, and matching on ids keeps the code working
on any provider. The whole pass is wrapped in a `try`/`catch` that logs and waits for the
next tick: a failing cleanup must never take the host down.

The query itself lives in `DataCleanupService`, separate from the `BackgroundService`
that schedules it, so tests can run a pass against a real database and assert on what
survived without waiting on a timer. `PeriodicTimer` is constructed with the injected
`TimeProvider`, so the schedule can be driven by hand too.

---

## Getting started

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Google OAuth **client id** ([Google Cloud console](https://console.cloud.google.com/apis/credentials))
  with `http://localhost:5173` among its authorised JavaScript origins, if you want to
  exercise sign-in

No database server is needed — SQLite is a file.

### Setup

```bash
git clone https://github.com/yigith/KodisApp.git
cd KodisApp
dotnet restore
dotnet tool restore          # installs dotnet-ef locally
```

### Configuration

`appsettings.json` ships with every non-secret default filled in and every secret blank.
[`KodisApi/appsettings.Example.json`](KodisApi/appsettings.Example.json) documents the
full set of keys.

Supply the secrets through **user secrets**, which live outside the repository and
cannot be committed by accident:

```bash
cd KodisApi
dotnet user-secrets set "ConnectionStrings:ApplicationDbContext" "Data Source=kodis.db"
dotnet user-secrets set "JwtSettings:Secret" "$(openssl rand -base64 48)"
dotnet user-secrets set "Google:ClientId" "your-client-id.apps.googleusercontent.com"
```

Every setting also has an environment-variable form using `__` as the separator
(`JwtSettings__Secret`, `ConnectionStrings__ApplicationDbContext`,
`Cors__AllowedOrigins__0`), which is how the container and the systemd unit supply them
in production.

The application will not start with a required secret missing or malformed. Options are
validated with `ValidateOnStart()`, so a signing key shorter than 32 characters or a
blank client id fails the boot with a message naming the key.

### Database

In `Development`, `Database:MigrateOnStartup` is `true`, so the schema is created on
first run and there is nothing to do. To manage it explicitly:

```bash
dotnet ef database update      --project KodisApi
dotnet ef migrations add Name  --project KodisApi --output-dir Data/Migrations
dotnet ef migrations list      --project KodisApi
```

### Run

```bash
dotnet run --project KodisApi
```

Listens on `http://localhost:5247`, with Swagger UI at `/swagger` (`/` redirects there in
Development). The Development CORS policy already allows `http://localhost:5173`, the
Vite dev server the [frontend](https://github.com/yigith/kodis) runs on.

---

## Configuration

| Key | Meaning | Default |
| --- | --- | --- |
| `ConnectionStrings:ApplicationDbContext` | SQLite connection string, e.g. `Data Source=/var/lib/kodisapi/kodis.db` | *(required)* |
| `JwtSettings:Secret` | HMAC-SHA256 signing key, ≥ 32 characters. **Secret.** | *(required)* |
| `JwtSettings:Issuer` / `:Audience` | Issuer and audience stamped into, and required of, every token | `kod.is` |
| `JwtSettings:AccessExpirationTimeInMinutes` | Access token lifetime, 1–1440 | `15` |
| `JwtSettings:RefreshExpirationTimeInMinutes` | Refresh token lifetime, 1–525600 | `20160` (14 days) |
| `JwtSettings:ClockSkewInSeconds` | Tolerance for client clock drift, 0–300 | `60` |
| `Google:ClientId` | OAuth client id every Google credential must be issued to | *(required)* |
| `Sqids:Alphabet` | Shuffled alphabet for slugs, ≥ 16 characters. Changing it invalidates every slug already handed out. | *(required)* |
| `Sqids:MinLength` | Minimum slug length, 4–32 | `8` |
| `Cors:AllowedOrigins` | Allowed browser origins (array, at least one entry) | `["https://kod.is"]` |
| `Notebook:AnonymousLifetimeInHours` | How long an anonymous notebook stays readable, 1–8760 | `24` |
| `Notebook:MaxNotesPerNotebook` | Notes per notebook, 1–1000 | `100` |
| `Notebook:MaxNoteTitleLength` | Title cap, 1–50 | `50` |
| `Notebook:MaxNoteContentLength` | Content cap per note, 1024–1000000 | `100000` |
| `Notebook:CleanupIntervalInMinutes` | How often the purge runs, 1–1440 | `60` |
| `Notebook:CleanupGraceInHours` | Delay between expiry and physical deletion, 0–8760 | `24` |
| `Database:MigrateOnStartup` | Apply migrations on boot | `true` in Development |
| `Hosting:UseHttpsRedirection` | Enable only when this process terminates TLS itself | `false` |
| `DataProtection:KeyRingPath` | Where to persist data-protection keys | *(ephemeral if unset)* |
| `AllowedHosts` | Host header filter | `kod.is;*.kod.is;localhost;127.0.0.1` |

Only the three marked *(required)* have no default. `Sqids:Alphabet` is not a security
boundary — a determined attacker can recover the mapping from enough slug/id pairs — but
it is deployment-specific and worth treating as a value you do not publish.

`Hosting:UseHttpsRedirection` defaults to `false` on purpose. Caddy terminates TLS in
production, so the API only ever sees plain HTTP on `127.0.0.1`; enabling the middleware
there would accomplish nothing except log a warning on every single request.

---

## Deployment

The VPS runs the API as a systemd service. Caddy terminates TLS and reverse-proxies
`kodisapi.kod.is` to it on loopback.

| | |
| --- | --- |
| Binary | `/opt/kodisapi` — self-contained, so the server needs no .NET runtime |
| Database + data-protection keys | `/var/lib/kodisapi` |
| Secrets | `/etc/kodisapi/kodisapi.env`, mode `0640`, owned by `root:kodisapi` |
| Service | `kodisapi.service`, running as the unprivileged `kodisapi` user |
| Port | `127.0.0.1:3003` — reachable only through Caddy |

```bash
./scripts/deploy.sh            # test, publish linux-x64, install, restart, health-check
HOST=vps ./scripts/deploy.sh   # or name the ssh host explicitly
```

The script runs the test suite first and aborts on failure, stops the service before
replacing the binary — a running executable cannot be overwritten, and a brief outage
beats a half-written deployment — then verifies `/health` and dumps the last 30 log lines
if it does not come back. Secrets on the server are never touched by a deploy.

```bash
ssh vps systemctl status kodisapi
ssh vps journalctl -u kodisapi -f
```

`Database:MigrateOnStartup` is left on in production here. With SQLite and a single
instance there is no second process to race, and it keeps the schema in step with the
binary that was just installed. That reasoning does not survive scaling out; a
multi-instance deployment should migrate from the release pipeline instead.

`UseSystemd()` gives the host `Type=notify` readiness reporting — systemd considers the
unit started only once the application is actually listening — and maps ASP.NET log
levels onto journald priorities, so `journalctl -p err` works as expected.

### Backups

The whole database is one file, but copying it with `cp` while the service is running can
capture a torn write. Use SQLite's own backup command, which takes a consistent snapshot:

```bash
ssh vps sqlite3 /var/lib/kodisapi/kodis.db ".backup '/root/kodis-backup.db'"
```

### Docker

An alternative to the systemd path, not what production runs. `docker-compose.yml` reads
secrets from a `.env` file (see `KodisApi/.env.example`) and refuses to start if any are
missing, keeps the database and key ring on a named volume, and runs as the base image's
non-root `app` user.

```bash
cd KodisApi
cp .env.example .env    # fill in the real values
docker compose up --build
```

---

## Tests

```bash
dotnet test
```

xUnit, covering slug generation, notebook access and edit-authorization rules, expiry
boundaries, the note merge, password hashing, token issuance, refresh rotation and replay
detection, and the cleanup query.

The tests run against a **real SQLite database** — `DataSource=:memory:` with the
connection held open for the fixture's lifetime — rather than the EF in-memory provider.
That provider does not enforce unique indexes, foreign keys or cascade deletes, which is
precisely the behaviour these tests are checking; testing against it would prove nothing
about how the application actually stores data. Time comes from a `TestTimeProvider` the
tests move by hand, so a 24-hour expiry or a 14-day refresh window can be crossed without
waiting.

CI (GitHub Actions) restores, builds and tests on every push and pull request to `master`,
greps `appsettings*.json` for anything that looks like a committed secret and fails the
build if it finds one, then builds the Docker image.
