using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core;

namespace Atlas.Application
{
    public class ToolDispatcher : IToolDispatcher
    {
        private readonly IEnumerable<IAtlasTool> _tools;

        public ToolDispatcher(IEnumerable<IAtlasTool> tools)
        {
            _tools = tools;
        }

        public IEnumerable<IAtlasTool> GetAvailableTools()
        {
            return _tools;
        }

        public async Task<(bool success, string result, PermissionLevel requiredPermission)> TryExecuteToolAsync(string toolName, string arguments)
        {
            var tool = _tools.FirstOrDefault(t => t.Name.Equals(toolName, System.StringComparison.OrdinalIgnoreCase));
            if (tool == null)
            {
                return (false, $"Tool '{toolName}' not found.", PermissionLevel.Safe);
            }

            try
            {
                // Note: The actual permission checking (interrupting execution for user consent)
                // will be handled higher up in the flow (e.g. SignalR hub or a permission engine middleware)
                var result = await tool.ExecuteAsync(arguments);
                return (true, result, tool.RequiredPermission);
            }
            catch (System.Exception ex)
            {
                return (false, $"Error executing tool: {ex.Message}", tool.RequiredPermission);
            }
        }
    }
}
