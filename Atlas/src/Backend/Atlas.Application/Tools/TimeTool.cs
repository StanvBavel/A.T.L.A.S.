using System;
using System.Threading.Tasks;
using Atlas.Core;

namespace Atlas.Application.Tools
{
    public class TimeTool : IAtlasTool
    {
        public string Name => "Time";
        public string Description => "Returns the current local time.";
        public PermissionLevel RequiredPermission => PermissionLevel.Safe;

        public Task<string> ExecuteAsync(string arguments)
        {
            return Task.FromResult($"De huidige tijd is {DateTime.Now:HH:mm}.");
        }
    }
}
