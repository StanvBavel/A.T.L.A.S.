using System.Threading.Tasks;

namespace Atlas.Core
{
    public interface IAiProvider
    {
        Task<string> GenerateResponseAsync(string prompt);
    }
}
