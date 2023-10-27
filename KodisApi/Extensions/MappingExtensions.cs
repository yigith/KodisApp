
using Google.Apis.Oauth2.v2.Data;
using static Google.Apis.Auth.GoogleJsonWebSignature;

namespace KodisApi.Extensions
{
    public static class MappingExtensions
    {
        public static NotebookUser ToNotebookUser(this Payload payload)
        {
            return new NotebookUser
            {
                Email = payload.Email,
                FullName = payload.Name,
                Picture = payload.Picture,
                Locale = payload.Locale,
                FamilyName = payload.FamilyName,
                GivenName = payload.GivenName,
                Sub = payload.Subject,
                EmailVerified = payload.EmailVerified,
                LoginMethod = LoginMethod.Google
            };
        }

        public static NotebookUser ToNotebookUser(this Userinfo userinfo)
        {
            return new NotebookUser
            {
                Email = userinfo.Email,
                FullName = userinfo.Name,
                Picture = userinfo.Picture,
                Locale = userinfo.Locale,
                FamilyName = userinfo.FamilyName,
                GivenName = userinfo.GivenName,
                Sub = userinfo.Id,
                EmailVerified = userinfo.VerifiedEmail ?? false,
                LoginMethod = LoginMethod.Google
            };
        }
    }
}
