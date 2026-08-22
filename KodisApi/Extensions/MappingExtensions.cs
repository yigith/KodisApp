namespace KodisApi.Extensions
{
    public static class MappingExtensions
    {
        public static NotebookUser ToNotebookUser(this GoogleUserInfo info, DateTimeOffset now) =>
            new()
            {
                Email = info.Email,
                EmailVerified = info.EmailVerified,
                FullName = info.FullName,
                Picture = info.Picture,
                Locale = info.Locale,
                FamilyName = info.FamilyName,
                GivenName = info.GivenName,
                Sub = info.Subject,
                LoginMethod = LoginMethod.Google,
                CreatedDate = now,
                ModifiedDate = now,
                LastLoginDate = now
            };

        /// <summary>Refreshes the profile fields we mirror from the provider.</summary>
        public static void ApplyProfile(this NotebookUser user, GoogleUserInfo info, DateTimeOffset now)
        {
            user.Sub = info.Subject;
            user.Email = info.Email;
            user.EmailVerified = info.EmailVerified;
            user.FullName = info.FullName;
            user.GivenName = info.GivenName;
            user.FamilyName = info.FamilyName;
            user.Picture = info.Picture;
            user.Locale = info.Locale;
            user.LoginMethod = LoginMethod.Google;
            user.ModifiedDate = now;
            user.LastLoginDate = now;
        }
    }
}
