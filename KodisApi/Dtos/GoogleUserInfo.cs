namespace KodisApi.Dtos
{
    /// <summary>
    /// Result of validating a Google credential, normalised across the
    /// One Tap (ID token) and OAuth access-token flows.
    /// </summary>
    public record GoogleUserInfo(
        string Subject,
        string Email,
        bool EmailVerified,
        string? FullName,
        string? GivenName,
        string? FamilyName,
        string? Picture,
        string? Locale);
}
