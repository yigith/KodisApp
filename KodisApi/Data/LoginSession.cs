namespace KodisApi.Data
{
    public class LoginSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string NotebookUserId { get; set; } = null!;

        public DateTime Expires { get; set; }

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset RefreshedDate { get; set; } = DateTimeOffset.Now;


        public NotebookUser NotebookUser { get; set; } = null!;
    }
}
