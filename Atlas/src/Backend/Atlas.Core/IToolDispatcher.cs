using System.Collections.Generic;
using System.Threading.Tasks;

namespace Atlas.Core
{
    public interface IToolDispatcher
    {
        IEnumerable<IAtlasTool> GetAvailableTools();
        Task<(bool success, string result, PermissionLevel requiredPermission)> TryExecuteToolAsync(string toolName, string arguments);
    }
}
