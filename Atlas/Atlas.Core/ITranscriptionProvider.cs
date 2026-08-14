using System.Threading.Tasks;

namespace Atlas.Core
{
    public interface ITranscriptionProvider
    {
        Task<string> TranscribeAudioAsync(byte[] audioData);
    }
}
