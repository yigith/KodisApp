using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Google.Apis.Auth.GoogleJsonWebSignature;

namespace KodisApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly JwtService _jwtService;

        private string GoogleClientId => _configuration["Google:ClientId"]!;

        public AccountController(IConfiguration configuration, JwtService jwtService)
        {
            _configuration = configuration;
            _jwtService = jwtService;
        }

        [Authorize, HttpPost("Check")]
        public IActionResult Get()
        {
            return Ok();
        }


        [HttpPost("GoogleSigninByGoogleOneTap")]
        public async Task<IActionResult> GoogleSigninByGoogleOneTap(GoogleOneTapCredentialResponse dto)
        {
            Payload validPayload = await GoogleJsonWebSignature.ValidateAsync(dto.Credential);

            if (validPayload == null || validPayload.Audience.ToString() != GoogleClientId)
            {
                return Unauthorized();
            }

            var token = _jwtService.GenerateJwtToken(validPayload.ToNotebookUser());

            return Ok(new { token });
        }

        [HttpPost("GoogleSigninByTokenResponse")]
        public async Task<IActionResult> GoogleSigninByTokenResponse(GoogleTokenResponse dto)
        {
            var userinfo = await GetUserInfo(dto.Access_Token);

            if (userinfo == null)
            {
                return Unauthorized();
            }

            var token = _jwtService.GenerateJwtToken(userinfo.ToNotebookUser());

            return Ok(new { token });
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
