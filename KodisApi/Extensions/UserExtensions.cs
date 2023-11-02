using System.Security.Claims;

namespace KodisApi.Extensions
{
    public static class UserExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
