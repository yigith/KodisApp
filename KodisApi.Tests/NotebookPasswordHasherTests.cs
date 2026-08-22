namespace KodisApi.Tests
{
    public class NotebookPasswordHasherTests
    {
        private readonly NotebookPasswordHasher _hasher = new();

        [Fact]
        public void Hash_then_verify_round_trips()
        {
            var salt = _hasher.CreateSalt();
            var hash = _hasher.Hash("correct horse", salt);

            Assert.True(_hasher.Verify("correct horse", hash, salt));
        }

        [Fact]
        public void Verify_rejects_a_wrong_password()
        {
            var salt = _hasher.CreateSalt();
            var hash = _hasher.Hash("correct horse", salt);

            Assert.False(_hasher.Verify("battery staple", hash, salt));
        }

        [Fact]
        public void Verify_rejects_a_different_salt()
        {
            var hash = _hasher.Hash("correct horse", _hasher.CreateSalt());

            Assert.False(_hasher.Verify("correct horse", hash, _hasher.CreateSalt()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Verify_rejects_a_missing_password(string? password)
        {
            var salt = _hasher.CreateSalt();
            var hash = _hasher.Hash("correct horse", salt);

            Assert.False(_hasher.Verify(password, hash, salt));
        }

        [Fact]
        public void Verify_returns_false_when_the_notebook_has_no_password()
        {
            Assert.False(_hasher.Verify("anything", null, null));
        }

        [Fact]
        public void Verify_survives_a_corrupt_stored_hash()
        {
            Assert.False(_hasher.Verify("anything", "not base64 !!", _hasher.CreateSalt()));
        }

        [Fact]
        public void Salts_are_unique_per_call()
        {
            Assert.NotEqual(_hasher.CreateSalt(), _hasher.CreateSalt());
        }
    }
}
