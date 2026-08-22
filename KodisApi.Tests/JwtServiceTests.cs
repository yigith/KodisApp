using KodisApi.Exceptions;
using KodisApi.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KodisApi.Tests
{
    public class JwtServiceTests : IDisposable
    {
        private readonly TestHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private ClaimsPrincipal Validate(string token)
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            return handler.ValidateToken(
                token, JwtTokenValidation.BuildParameters(_harness.JwtSettings, _harness.TimeProvider), out _);
        }

        [Fact]
        public async Task CreateSession_marks_the_two_tokens_with_distinct_types()
        {
            var user = _harness.AddUser();

            var tokens = await _harness.JwtService.CreateSessionAsync(user);

            Assert.True(JwtService.HasTokenType(Validate(tokens.AccessToken), JwtService.AccessTokenType));
            Assert.True(JwtService.HasTokenType(Validate(tokens.RefreshToken), JwtService.RefreshTokenType));
        }

        [Fact]
        public async Task Access_token_is_not_accepted_as_a_refresh_token()
        {
            var user = _harness.AddUser();
            var tokens = await _harness.JwtService.CreateSessionAsync(user);

            // Signature and issuer are fine; only the token_type claim differs.
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.JwtService.RefreshAsync(tokens.AccessToken));
        }

        [Fact]
        public async Task Refresh_token_is_not_accepted_as_a_bearer_credential()
        {
            var user = _harness.AddUser();
            var tokens = await _harness.JwtService.CreateSessionAsync(user);

            var principal = Validate(tokens.RefreshToken);

            Assert.False(JwtService.HasTokenType(principal, JwtService.AccessTokenType));
        }

        [Fact]
        public async Task Refreshing_rotates_the_refresh_token()
        {
            var user = _harness.AddUser();
            var first = await _harness.JwtService.CreateSessionAsync(user);

            _harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            var second = await _harness.JwtService.RefreshAsync(first.RefreshToken);

            Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        }

        [Fact]
        public async Task Replaying_a_rotated_refresh_token_revokes_the_whole_session()
        {
            var user = _harness.AddUser();
            var first = await _harness.JwtService.CreateSessionAsync(user);

            _harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            var second = await _harness.JwtService.RefreshAsync(first.RefreshToken);

            // The stolen (already rotated) token is presented again.
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.JwtService.RefreshAsync(first.RefreshToken));

            // ...which must also kill the token the legitimate client holds.
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.JwtService.RefreshAsync(second.RefreshToken));

            var session = await _harness.Db.LoginSessions.SingleAsync();
            Assert.NotNull(session.RevokedDate);
        }

        [Fact]
        public async Task Refreshing_after_logout_fails()
        {
            var user = _harness.AddUser();
            var tokens = await _harness.JwtService.CreateSessionAsync(user);
            var sessionId = Validate(tokens.RefreshToken).FindFirst(JwtRegisteredClaimNames.Sid)!.Value;

            await _harness.JwtService.RevokeSessionAsync(sessionId);

            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.JwtService.RefreshAsync(tokens.RefreshToken));
        }

        [Fact]
        public async Task Refreshing_after_the_session_expires_fails()
        {
            var user = _harness.AddUser();
            var tokens = await _harness.JwtService.CreateSessionAsync(user);

            _harness.TimeProvider.Advance(
                TimeSpan.FromMinutes(_harness.JwtSettings.RefreshExpirationTimeInMinutes + 1));

            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.JwtService.RefreshAsync(tokens.RefreshToken));
        }

        [Fact]
        public async Task Access_token_carries_the_username_once_it_is_set()
        {
            var user = _harness.AddUser(userName: "yigit");
            var tokens = await _harness.JwtService.CreateSessionAsync(user);

            var principal = Validate(tokens.AccessToken);

            Assert.Equal("yigit", principal.FindFirst(JwtService.UsernameClaim)?.Value);
            Assert.Equal(user.Id, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        }

        [Fact]
        public async Task Missing_optional_profile_fields_do_not_break_token_issuing()
        {
            // FullName/GivenName/Picture are all null here - the old code
            // dereferenced them with ! and threw.
            var user = _harness.AddUser();

            var tokens = await _harness.JwtService.CreateSessionAsync(user);

            Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        }
    }
}
