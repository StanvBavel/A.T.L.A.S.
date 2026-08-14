using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Atlas.Core;

namespace Atlas.Application.Tools
{
    public class SystemControlTool : IAtlasTool
    {
        public string Name => "SystemControl";
        public string Description => "Opens allowed applications or performs system tasks.";
        public PermissionLevel RequiredPermission => PermissionLevel.Dangerous;

        public Task<string> ExecuteAsync(string arguments)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(arguments))
                {
                    return Task.FromResult("Error: No command specified for SystemControl.");
                }

                if (arguments.Equals("notepad", StringComparison.OrdinalIgnoreCase))
                {
                    // Simulated registry lookup: In a real scenario, this reads from AllowedApplications DB table.
                    Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
                    return Task.FromResult("Notepad geopend.");
                }

                if (arguments.Equals("calc", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true });
                    return Task.FromResult("Rekenmachine geopend.");
                }

                return Task.FromResult($"Command '{arguments}' is not allowed or not found in application registry.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to execute command: {ex.Message}");
            }
        }
    }
}
