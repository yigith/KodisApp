using KodisApi.Exceptions;
using KodisApi.Infrastructure;
using KodisApi.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;


namespace KodisApi.Services
{
    public sealed class JwtService
    {
        public const string TokenTypeClaim = "token_type";
        public const string AccessTokenType = "access";
        public const string RefreshTokenType = "refresh";
        public const string UsernameClaim = "username";

        private readonly JwtSettings _settings;
        private readonly ApplicationDbContext _db;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<JwtService> _logger;

        public JwtService(
            IOptions<JwtSettings> settings,
            ApplicationDbContext db,
            TimeProvider timeProvider,
            ILogger<JwtService> logger)
        {
            _settings = settings.Value;
            _db = db;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public SymmetricSecurityKey SigningKey => JwtTokenValidation.CreateSigningKey(_settings);

        /// <summary>
        /// True when the principal carries a token of the given kind. The two
        /// token kinds share a signing key, so this claim is what stops a
        /// refresh token from being replayed as a bearer credential.
        /// </summary>
        public static bool HasTokenType(ClaimsPrincipal principal, string tokenType) =>
            string.Equals(
                principal.FindFirst(TokenTypeClaim)?.Value, tokenType, StringComparison.Ordinal);

        /// <summary>Starts a brand new login session for the user.</summary>
        public async Task<TokensDto> CreateSessionAsync(NotebookUser user, CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();

            var session = new LoginSession
            {
                NotebookUserId = user.Id,
                Expires = now.AddMinutes(_settings.RefreshExpirationTimeInMinutes),
                CreatedDate = now,
                RefreshedDate = now
            };

            _db.LoginSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);

            return IssueTokens(user, session, now);
        }

        /// <summary>
        /// Re-issues the token pair for an already authenticated session, used
        /// when claims baked into the access token change (e.g. the username).
        /// Rotates the refresh token too, so only one pair is ever live.
        /// </summary>
        public async Task<TokensDto> ReissueForSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();

            var session = await _db.LoginSessions
                .Include(x => x.NotebookUser)
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

            if (session is null || !session.IsActive(now))
            {
                throw new UnauthorizedException("The login session is no longer valid.");
            }

            return await RotateAsync(session, now, cancellationToken);
        }

        /// <summary>
        /// Exchanges a refresh token for a fresh pair. The presented token must
        /// be the newest one issued for its session; replaying an older one is
        /// treated as theft and kills the session.
        /// </summary>
        public async Task<TokensDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow();
            var handler = CreateHandler();

            ClaimsPrincipal principal;
            try
            {
                principal = handler.ValidateToken(
                    refreshToken, JwtTokenValidation.BuildParameters(_settings, _timeProvider), out _);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                _logger.LogInformation(ex, "Refresh token rejected during validation.");
                throw new UnauthorizedException("Invalid refresh token.");
            }

            if (!HasTokenType(principal, RefreshTokenType))
            {
                // An access token would otherwise be accepted here, letting a
                // short-lived credential mint long-lived ones.
                _logger.LogInformation("A non-refresh token was presented to the refresh endpoint.");
                throw new UnauthorizedException("Invalid refresh token.");
            }

            var sessionId = principal.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
            var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(tokenId))
            {
                throw new UnauthorizedException("Invalid refresh token.");
            }

            var session = await _db.LoginSessions
                .Include(x => x.NotebookUser)
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

            if (session is null || !session.IsActive(now))
            {
                throw new UnauthorizedException("Invalid refresh token.");
            }

            if (!string.Equals(session.RefreshTokenId, tokenId, StringComparison.Ordinal))
            {
                // The token was valid once but has already been rotated away.
                // Either it leaked or a client is replaying it - drop the session.
                _logger.LogWarning(
                    "Refresh token reuse detected for session {SessionId}; revoking it.", session.Id);
                session.RevokedDate = now;
                await _db.SaveChangesAsync(cancellationToken);
                throw new UnauthorizedException("Invalid refresh token.");
            }

            return await RotateAsync(session, now, cancellationToken);
        }

        /// <summary>Ends a session; both of its tokens stop working immediately.</summary>
        public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var session = await _db.LoginSessions
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

            if (session is null || session.RevokedDate != null)
            {
                return;
            }

            session.RevokedDate = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<TokensDto> RotateAsync(LoginSession session, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var user = session.NotebookUser
                ?? await _db.NotebookUsers.FirstOrDefaultAsync(x => x.Id == session.NotebookUserId, cancellationToken)
                ?? throw new UnauthorizedException("Invalid refresh token.");

            session.RefreshTokenId = Guid.NewGuid().ToString("N");
            session.RefreshedDate = now;
            session.Expires = now.AddMinutes(_settings.RefreshExpirationTimeInMinutes);
            await _db.SaveChangesAsync(cancellationToken);

            return IssueTokens(user, session, now);
        }

        private TokensDto IssueTokens(NotebookUser user, LoginSession session, DateTimeOffset now)
        {
            var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);
            var accessExpiresAt = now.AddMinutes(_settings.AccessExpirationTimeInMinutes);

            var accessClaims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Sid, session.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(TokenTypeClaim, AccessTokenType)
            };

            AddIfPresent(accessClaims, JwtRegisteredClaimNames.Name, user.FullName);
            AddIfPresent(accessClaims, JwtRegisteredClaimNames.GivenName, user.GivenName);
            AddIfPresent(accessClaims, JwtRegisteredClaimNames.FamilyName, user.FamilyName);
            AddIfPresent(accessClaims, UsernameClaim, user.UserName);
            AddIfPresent(accessClaims, "picture", user.Picture);
            AddIfPresent(accessClaims, "locale", user.Locale);

            var refreshClaims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sid, session.Id),
                new Claim(JwtRegisteredClaimNames.Jti, session.RefreshTokenId),
                new Claim(TokenTypeClaim, RefreshTokenType)
            };

            var handler = CreateHandler();

            return new TokensDto
            {
                AccessToken = handler.WriteToken(new JwtSecurityToken(
                    _settings.Issuer, _settings.Audience, accessClaims,
                    notBefore: now.UtcDateTime,
                    expires: accessExpiresAt.UtcDateTime,
                    signingCredentials: credentials)),
                AccessTokenExpiresAt = accessExpiresAt,
                RefreshToken = handler.WriteToken(new JwtSecurityToken(
                    _settings.Issuer, _settings.Audience, refreshClaims,
                    notBefore: now.UtcDateTime,
                    expires: session.Expires.UtcDateTime,
                    signingCredentials: credentials)),
                RefreshTokenExpiresAt = session.Expires
            };
        }

        private static void AddIfPresent(ICollection<Claim> claims, string type, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(new Claim(type, value));
            }
        }

        private static JwtSecurityTokenHandler CreateHandler() => new() { MapInboundClaims = false };
    }
}
