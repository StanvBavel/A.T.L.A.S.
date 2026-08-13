using System;
using System.Threading.Tasks;
using Atlas.Core;

namespace Atlas.Application.Tools
{
    public class MemoryTool : IAtlasTool
    {
        private readonly IMemoryRepository _memoryRepository;

        public MemoryTool(IMemoryRepository memoryRepository)
        {
            _memoryRepository = memoryRepository;
        }

        public string Name => "Memory";
        public string Description => "Saves or retrieves long-term memory fragments. Usage: save <cat> <key> <val> OR get <cat> <key>";
        public PermissionLevel RequiredPermission => PermissionLevel.Normal;

        public async Task<string> ExecuteAsync(string arguments)
        {
            var parts = arguments.Split(' ', 4);
            if (parts.Length < 3) return "Invalid arguments. Use: save <cat> <key> <val> OR get <cat> <key>";

            var action = parts[0].ToLower();
            var category = parts[1];
            var key = parts[2];

            if (action == "save" && parts.Length == 4)
            {
                var value = parts[3];
                await _memoryRepository.SaveMemoryAsync(category, key, value);
                return $"Memory saved: [{category}] {key} = {value}";
            }
            else if (action == "get")
            {
                var value = await _memoryRepository.GetMemoryAsync(category, key);
                return value != null ? $"Memory found: {value}" : "Memory not found.";
            }

            return "Unknown memory action.";
        }
    }
}
