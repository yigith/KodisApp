namespace KodisApi.Infrastructure
{
    public static class RateLimitPolicies
    {
        /// <summary>Sign-in and refresh: cheap to call, expensive to brute force.</summary>
        public const string Authentication = "auth";

        /// <summary>Slug lookups, which are otherwise guessable one request at a time.</summary>
        public const string NotebookRead = "notebook-read";

        /// <summary>Notebook creation and edits.</summary>
        public const string NotebookWrite = "notebook-write";
    }
}
