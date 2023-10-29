using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using static Google.Apis.Auth.GoogleJsonWebSignature;

namespace KodisApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly JwtService _jwtService;
        private readonly ApplicationDbContext _db;

        private string GoogleClientId => _configuration["Google:ClientId"]!;

        public AccountController(IConfiguration configuration, JwtService jwtService, ApplicationDbContext db)
        {
            _configuration = configuration;
            _jwtService = jwtService;
            _db = db;
        }

        [Authorize, HttpPost("Check")]
        public IActionResult CheckLogin()
        {
            var isLoggedIn = User.Identity?.IsAuthenticated ?? false;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Ok();
        }


        [Authorize, HttpPost("Logout")]
        public IActionResult Logout()
        {
            var sid = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;

            if (sid != null)
            {
                var loginSession = _db.LoginSessions.Find(sid);

                if (loginSession != null)
                {
                    _db.LoginSessions.Remove(loginSession);
                    _db.SaveChanges();
                }
            }

            return Ok();
        }


        [HttpPost("GoogleSigninByGoogleOneTap")]
        public async Task<ActionResult<TokensDto>> GoogleSigninByGoogleOneTap(GoogleOneTapCredentialResponse dto)
        {
            Payload validPayload = await GoogleJsonWebSignature.ValidateAsync(dto.Credential);

            if (validPayload == null || validPayload.Audience.ToString() != GoogleClientId)
            {
                return Unauthorized();
            }

            var user = CreateOrUpdateUser(validPayload.ToNotebookUser());
            return _jwtService.GenerateJwtToken(user);
        }

        [HttpPost("GoogleSigninByTokenResponse")]
        public async Task<ActionResult<TokensDto>> GoogleSigninByTokenResponse(GoogleTokenResponse dto)
        {
            var userinfo = await GetUserInfo(dto.Access_Token);

            if (userinfo == null)
            {
                return Unauthorized();
            }

            var user = CreateOrUpdateUser(userinfo.ToNotebookUser());
            return _jwtService.GenerateJwtToken(user);
        }

        [HttpPost("RefreshLogin")]
        public async Task<ActionResult<TokensDto>> RefreshLogin(RefreshLoginDto dto)
        {
            try
            {
                return _jwtService.RefreshJwtToken(dto.RefreshToken);

            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }   

        private NotebookUser CreateOrUpdateUser(NotebookUser user)
        {
            var userDb = _db.NotebookUsers.FirstOrDefault(u => u.Email == user.Email);

            if (userDb == null)
            {
                _db.NotebookUsers.Add(user);
                _db.SaveChanges();
                return user;
            }

            userDb.Sub = user.Sub;
            userDb.FamilyName = user.FamilyName;
            userDb.GivenName = user.GivenName;
            userDb.Locale = user.Locale;
            userDb.FullName = user.FullName;
            userDb.Picture = user.Picture;
            userDb.EmailVerified = user.EmailVerified;
            userDb.LoginMethod = user.LoginMethod;
            userDb.ModifiedDate = DateTimeOffset.Now;
            userDb.LastLoginDate = DateTimeOffset.Now;
            _db.SaveChanges();
            return userDb;
        }

        private async Task<Userinfo?> GetUserInfo(string accessToken)
        {
            try
            {
                // Create a Google credential using the access token
                var credential = GoogleCredential.FromAccessToken(accessToken);

                // Create an OAuth2 service with the credential
                var oauth2Service = new Oauth2Service(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential
                });

                // Retrieve the user information
                Userinfo userInfo = await oauth2Service.Userinfo.Get().ExecuteAsync();

                // Return the user information
                return userInfo;
            }
            catch (Exception ex)
            {
                // Handle any exceptions here
                Console.WriteLine($"Error fetching user info: {ex.Message}");
                return null;
            }
        }
    }
}
