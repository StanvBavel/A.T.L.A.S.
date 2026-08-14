using Atlas.Infrastructure.Data;
using Atlas.Infrastructure;
using Atlas.Core;
using Atlas.Application;
using Atlas.Application.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;

namespace Atlas.Api
{
    public class AtlasHub : Hub
    {
        private readonly IAiProvider _aiProvider;
        private readonly IToolDispatcher _toolDispatcher;

        public AtlasHub(IAiProvider aiProvider, IToolDispatcher toolDispatcher)
        {
            _aiProvider = aiProvider;
            _toolDispatcher = toolDispatcher;
        }

        public async Task SendText(string text)
        {
            await Clients.Caller.SendAsync("UpdateCoreState", "THINKING");

            if (text.StartsWith("/tool "))
            {
                var parts = text.Substring(6).Split(' ', 2);
                var toolName = parts[0];
                var args = parts.Length > 1 ? parts[1] : "";

                var (success, result, permission) = await _toolDispatcher.TryExecuteToolAsync(toolName, args);

                if (permission >= PermissionLevel.Dangerous)
                {
                    await Clients.Caller.SendAsync("RequireUserConsent", toolName, args);
                    await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
                    return;
                }

                await Clients.Caller.SendAsync("ReceiveMessage", result);
                await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
                return;
            }

            var response = await _aiProvider.GenerateResponseAsync(text);

            await Clients.Caller.SendAsync("ReceiveMessage", response);
            await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
        }

        public async Task GrantPermission(string toolName, string arguments)
        {
            await Clients.Caller.SendAsync("UpdateCoreState", "PROCESSING");

            var (success, result, permission) = await _toolDispatcher.TryExecuteToolAsync(toolName, arguments);
            await Clients.Caller.SendAsync("ReceiveMessage", result);

            await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
        }

        public async Task RequestTelemetry()
        {
            var process = Process.GetCurrentProcess();
            var ramUsageMb = process.WorkingSet64 / (1024 * 1024);
            var fakeCpu = new Random().Next(2, 15);
            await Clients.Caller.SendAsync("ReceiveTelemetry", $"{fakeCpu} %", $"{ramUsageMb} MB");
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddSignalR();

            builder.Services.AddHttpClient<IAiProvider, OllamaAiProvider>();

            builder.Services.AddScoped<IMemoryRepository, MemoryRepository>();
            builder.Services.AddTransient<IAtlasTool, MemoryTool>();

            builder.Services.AddTransient<IAtlasTool, TimeTool>();
            builder.Services.AddTransient<IAtlasTool, SystemControlTool>();

            // Plugin Loader setup
            builder.Services.AddSingleton<PluginLoader>();

            // Factory to combine built-in tools + plugin tools
            builder.Services.AddTransient<IToolDispatcher>(sp =>
            {
                var builtInTools = sp.GetServices<IAtlasTool>().ToList();
                var pluginLoader = sp.GetRequiredService<PluginLoader>();

                var plugins = pluginLoader.LoadPlugins();
                foreach (var plugin in plugins)
                {
                    builtInTools.AddRange(plugin.GetPluginTools());
                }

                return new ToolDispatcher(builtInTools);
            });

            builder.Services.AddDbContext<AtlasDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=atlas.sqlite"));

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<AtlasHub>("/hubs/atlas");

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AtlasDbContext>();
                db.Database.EnsureCreated();
            }

            app.Run();
        }
    }
}
