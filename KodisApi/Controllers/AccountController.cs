using KodisApi.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KodisApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class AccountController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly GoogleAuthService _googleAuthService;
        private readonly NotebookService _notebookService;
        private readonly ApplicationDbContext _db;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            JwtService jwtService,
            GoogleAuthService googleAuthService,
            NotebookService notebookService,
            ApplicationDbContext db,
            TimeProvider timeProvider,
            ILogger<AccountController> logger)
        {
            _jwtService = jwtService;
            _googleAuthService = googleAuthService;
            _notebookService = notebookService;
            _db = db;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>Cheap probe the client uses to see whether its access token still works.</summary>
        [Authorize]
        [HttpPost("Check")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult CheckLogin() => Ok(new { userId = User.GetUserId() });

        [Authorize]
        [HttpPost("SetUsername")]
        [ProducesResponseType(typeof(TokensDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<TokensDto>> SetUsername(SetUsernameDto dto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var sessionId = User.GetSessionId();

            if (userId is null || sessionId is null)
            {
                throw new UnauthorizedException("The access token is missing required claims.");
            }

            var user = await _db.NotebookUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new UnauthorizedException("The signed-in user no longer exists.");

            if (string.Equals(user.UserName, dto.Username, StringComparison.Ordinal))
            {
                // Already owns this handle - make the call idempotent rather
                // than reporting the user's own name as taken.
                await _notebookService.EnsureMainNotebookAsync(user, cancellationToken);
                return Ok(await _jwtService.ReissueForSessionAsync(sessionId, cancellationToken));
            }

            // ToLower() translates to SQL; ToLowerInvariant() does not.
            var taken = await _db.NotebookUsers
                .AnyAsync(x => x.UserName != null && x.UserName.ToLower() == dto.Username, cancellationToken);

            if (taken)
            {
                throw new ConflictException("Username is already taken.");
            }

            user.UserName = dto.Username;
            user.ModifiedDate = _timeProvider.GetUtcNow();

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                await _notebookService.EnsureMainNotebookAsync(user, cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Two requests raced for the same handle; the unique index is
                // the authority, so translate its violation into a 409.
                _logger.LogInformation(ex, "Username {Username} lost a race.", dto.Username);
                throw new ConflictException("Username is already taken.");
            }

            // The username is baked into the access token, so a fresh pair is
            // needed for the client to see it.
            return Ok(await _jwtService.ReissueForSessionAsync(sessionId, cancellationToken));
        }

        [Authorize]
        [HttpPost("Logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var sessionId = User.GetSessionId();

            if (sessionId is not null)
            {
                await _jwtService.RevokeSessionAsync(sessionId, cancellationToken);
            }

            return NoContent();
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Authentication)]
        [HttpPost("GoogleSigninByGoogleOneTap")]
        [ProducesResponseType(typeof(TokensDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TokensDto>> GoogleSigninByGoogleOneTap(
            GoogleOneTapCredentialResponse dto, CancellationToken cancellationToken)
        {
            var info = await _googleAuthService.ValidateIdTokenAsync(dto.Credential, cancellationToken);
            var user = await CreateOrUpdateUserAsync(info, cancellationToken);

            return Ok(await _jwtService.CreateSessionAsync(user, cancellationToken));
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Authentication)]
        [HttpPost("GoogleSigninByTokenResponse")]
        [ProducesResponseType(typeof(TokensDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TokensDto>> GoogleSigninByTokenResponse(
            GoogleTokenResponse dto, CancellationToken cancellationToken)
        {
            var info = await _googleAuthService.ValidateAccessTokenAsync(dto.Access_Token, cancellationToken);
            var user = await CreateOrUpdateUserAsync(info, cancellationToken);

            return Ok(await _jwtService.CreateSessionAsync(user, cancellationToken));
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Authentication)]
        [HttpPost("RefreshLogin")]
        [ProducesResponseType(typeof(TokensDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TokensDto>> RefreshLogin(RefreshLoginDto dto, CancellationToken cancellationToken) =>
            Ok(await _jwtService.RefreshAsync(dto.RefreshToken, cancellationToken));

        /// <summary>
        /// Looks the account up by provider subject first and only falls back
        /// to the email address for rows created before subjects were indexed.
        /// Matching on email alone would let a second provider hand out an
        /// existing account to whoever controls that address.
        /// </summary>
        private async Task<NotebookUser> CreateOrUpdateUserAsync(GoogleUserInfo info, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();

            var user = await _db.NotebookUsers.FirstOrDefaultAsync(
                x => x.LoginMethod == LoginMethod.Google && x.Sub == info.Subject, cancellationToken);

            user ??= await _db.NotebookUsers.FirstOrDefaultAsync(
                x => x.LoginMethod == LoginMethod.Google && x.Email == info.Email, cancellationToken);

            if (user is null)
            {
                user = info.ToNotebookUser(now);
                _db.NotebookUsers.Add(user);
            }
            else
            {
                user.ApplyProfile(info, now);
            }

            await _db.SaveChangesAsync(cancellationToken);

            return user;
        }
    }
}
