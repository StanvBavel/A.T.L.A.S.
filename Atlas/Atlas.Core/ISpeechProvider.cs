using System.Threading.Tasks;

namespace Atlas.Core
{
    public interface ISpeechProvider
    {
        Task<byte[]> GenerateSpeechAsync(string text);
    }
}
