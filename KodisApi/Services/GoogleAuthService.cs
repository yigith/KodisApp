using Google.Apis.Auth;
using KodisApi.Exceptions;
using KodisApi.Settings;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KodisApi.Services
{
    /// <summary>
    /// Validates Google credentials for both supported sign-in flows and
    /// normalises them into a <see cref="GoogleUserInfo"/>.
    /// </summary>
    public sealed class GoogleAuthService
    {
        public const string HttpClientName = "Google";

        private const string TokenInfoUrl = "https://oauth2.googleapis.com/tokeninfo?access_token=";
        private const string UserInfoUrl = "https://openidconnect.googleapis.com/v1/userinfo";

        private readonly GoogleSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(
            IOptions<GoogleSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleAuthService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Verifies a Google One Tap ID token. The audience is checked by the
        /// library itself, so a token minted for a different application is
        /// rejected before we look at any of its claims.
        /// </summary>
        public async Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { _settings.ClientId }
                    });
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogInformation(ex, "Google ID token rejected.");
                throw new UnauthorizedException("The Google credential could not be verified.");
            }

            RequireVerifiedEmail(payload.Email, payload.EmailVerified);

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.Name,
                payload.GivenName,
                payload.FamilyName,
                payload.Picture,
                payload.Locale);
        }

        /// <summary>
        /// Verifies an OAuth access token. Google's tokeninfo endpoint is the
        /// only thing that reveals which client the token was issued to, so it
        /// is consulted first: without that check any application's token would
        /// be accepted here.
        /// </summary>
        public async Task<GoogleUserInfo> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var tokenInfoResponse = await client.GetAsync(
                TokenInfoUrl + Uri.EscapeDataString(accessToken), cancellationToken);

            if (!tokenInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Google tokeninfo returned {Status} for an access token.", tokenInfoResponse.StatusCode);
                throw new UnauthorizedException("The Google access token could not be verified.");
            }

            using var tokenInfo = JsonDocument.Parse(
                await tokenInfoResponse.Content.ReadAsStringAsync(cancellationToken));

            var audience = GetString(tokenInfo.RootElement, "aud");

            if (!string.Equals(audience, _settings.ClientId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Rejected a Google access token issued to a foreign audience {Audience}.", audience);
                throw new UnauthorizedException("The Google access token was issued to a different application.");
            }

            var subject = GetString(tokenInfo.RootElement, "sub");
            var email = GetString(tokenInfo.RootElement, "email");
            var emailVerified = GetBoolean(tokenInfo.RootElement, "email_verified");

            if (string.IsNullOrEmpty(subject))
            {
                throw new UnauthorizedException("The Google access token is missing a subject.");
            }

            // Profile details are a nice-to-have; the identity above is what matters.
            var profile = await TryGetProfileAsync(client, accessToken, cancellationToken);

            if (profile is not null)
            {
                email ??= GetString(profile.Value, "email");
                emailVerified |= GetBoolean(profile.Value, "email_verified");
            }

            RequireVerifiedEmail(email, emailVerified);

            return new GoogleUserInfo(
                subject,
                email!,
                emailVerified,
                profile is null ? null : GetString(profile.Value, "name"),
                profile is null ? null : GetString(profile.Value, "given_name"),
                profile is null ? null : GetString(profile.Value, "family_name"),
                profile is null ? null : GetString(profile.Value, "picture"),
                profile is null ? null : GetString(profile.Value, "locale"));
        }

        private async Task<JsonElement?> TryGetProfileAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                // Clone so the element outlives the JsonDocument we are disposing.
                using var document = JsonDocument.Parse(payload);
                return document.RootElement.Clone();
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                _logger.LogInformation(ex, "Could not read the Google profile; continuing without it.");
                return null;
            }
        }

        private static void RequireVerifiedEmail(string? email, bool emailVerified)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new UnauthorizedException("The Google account does not expose an email address.");
            }

            // Accounts are matched on email, so an unverified one would let
            // anybody claim somebody else's address.
            if (!emailVerified)
            {
                throw new UnauthorizedException("The Google account's email address is not verified.");
            }
        }

        private static string? GetString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>
        /// tokeninfo reports booleans as the strings "true"/"false" while the
        /// userinfo endpoint uses real JSON booleans.
        /// </summary>
        private static bool GetBoolean(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return false;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                _ => false
            };
        }
    }
}
