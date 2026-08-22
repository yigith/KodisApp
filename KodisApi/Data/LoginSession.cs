namespace KodisApi.Data
{
    /// <summary>
    /// One long-lived login. The session id travels in the "sid" claim of both
    /// tokens; <see cref="RefreshTokenId"/> is the jti of the single refresh
    /// token that is currently valid for this session and is rotated on every
    /// refresh so that a replayed token can be detected.
    /// </summary>
    public class LoginSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string NotebookUserId { get; set; } = null!;

        /// <summary>
        /// jti of the only refresh token accepted for this session right now.
        /// </summary>
        public string RefreshTokenId { get; set; } = Guid.NewGuid().ToString("N");

        public DateTimeOffset Expires { get; set; }

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset RefreshedDate { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Set on logout, or when a rotated-away refresh token is replayed.
        /// </summary>
        public DateTimeOffset? RevokedDate { get; set; }

        public bool IsActive(DateTimeOffset now) => RevokedDate == null && Expires > now;


        public NotebookUser NotebookUser { get; set; } = null!;
    }
}
