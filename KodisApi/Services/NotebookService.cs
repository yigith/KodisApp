using Sqids;

namespace KodisApi.Services
{
    public class NotebookService
    {
        private readonly SqidsEncoder<int> _sqidsEncoder;

        public NotebookService(SqidsEncoder<int> sqidsEncoder)
        {
            _sqidsEncoder = sqidsEncoder;
        }


        public string GenerateSlugFromId(int id)
        {
            return _sqidsEncoder.Encode(id);
        }
    }
}
