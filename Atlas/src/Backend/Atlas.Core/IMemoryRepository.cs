using System.Collections.Generic;
using System.Threading.Tasks;

namespace Atlas.Core
{
    public interface IMemoryRepository
    {
        Task SaveMemoryAsync(string category, string key, string value);
        Task<string?> GetMemoryAsync(string category, string key);
        Task<IEnumerable<MemoryFragment>> GetAllMemoriesAsync();
    }
}
