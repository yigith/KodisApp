using KodisApi.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace KodisApi.Services
{
    public class JwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ApplicationDbContext _db;
        private readonly IOptionsMonitor<JwtBearerOptions> _jwtBearerOptionsMonitor;

        public JwtService(IOptions<JwtSettings> jwtSettings, ApplicationDbContext db, IOptionsMonitor<JwtBearerOptions> jwtBearerOptionsMonitor)
        {
            _jwtSettings = jwtSettings.Value;
            _db = db;
            _jwtBearerOptionsMonitor = jwtBearerOptionsMonitor;
        }

        public TokensDto GenerateJwtToken(NotebookUser user, LoginSession loginSession = null!)
        {
            if (loginSession == null)
            {
                loginSession = new LoginSession()
                {
                    NotebookUserId = user.Id,
                    Expires = DateTime.Now.AddMinutes(_jwtSettings.RefreshExpirationTimeInMinutes),
                    CreatedDate = DateTimeOffset.Now,
                    RefreshedDate = DateTimeOffset.Now
                };
                _db.LoginSessions.Add(loginSession);
                _db.SaveChanges();
            }

            var sidClaim = new Claim(JwtRegisteredClaimNames.Sid, loginSession.Id);
            
            var claims = new[]
            {
                sidClaim,
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName!),
                new Claim(JwtRegisteredClaimNames.GivenName, user.GivenName!),
                new Claim(JwtRegisteredClaimNames.FamilyName, user.FamilyName!),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim("username", user.UserName!),
                new Claim("picture", user.Picture!),
                new Claim("locale", user.Locale!)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var accessToken = new JwtSecurityToken(
                _jwtSettings.Issuer,
                _jwtSettings.Audience,
                claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.AccessExpirationTimeInMinutes),
                signingCredentials: creds);

            var refreshToken = new JwtSecurityToken(
                _jwtSettings.Issuer,
                _jwtSettings.Audience,
                new[] { sidClaim },
                expires: loginSession.Expires,
                signingCredentials: creds);

            var tokenHandler = new JwtSecurityTokenHandler();

            return new TokensDto()
            {
                AccessToken = tokenHandler.WriteToken(accessToken),
                RefreshToken = tokenHandler.WriteToken(refreshToken)
            };
        }

        public TokensDto RefreshJwtToken(string refreshToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtBearerOptions = _jwtBearerOptionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
            var validationParameters = jwtBearerOptions.TokenValidationParameters;  

            var claimsPrincipal = tokenHandler.ValidateToken(refreshToken, validationParameters, out var validatedToken);
            var sidClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
            if (sidClaim == null)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }
            var loginSession = _db.LoginSessions.Find(sidClaim.Value);
            if (loginSession == null)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }
            if (loginSession.Expires < DateTime.Now)
            {
                throw new SecurityTokenException("Refresh token expired");
            }
            loginSession.RefreshedDate = DateTimeOffset.Now;
            loginSession.Expires = DateTime.Now.AddMinutes(_jwtSettings.RefreshExpirationTimeInMinutes);
            _db.SaveChanges();
            var user = _db.NotebookUsers.Find(loginSession.NotebookUserId);
            if (user == null)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }
            return GenerateJwtToken(user, loginSession);
        }
    }
}
