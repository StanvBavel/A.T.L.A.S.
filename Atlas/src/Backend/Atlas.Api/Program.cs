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
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Atlas.Api
{
    public class AtlasHub : Hub
    {
        private readonly IAiProvider _aiProvider;
        private readonly IToolDispatcher _toolDispatcher;
        private readonly IHologramGenerationService _hologramService;
        private readonly ILogger<AtlasHub> _logger;

        public AtlasHub(IAiProvider aiProvider, IToolDispatcher toolDispatcher, IHologramGenerationService hologramService, ILogger<AtlasHub> logger)
        {
            _aiProvider = aiProvider;
            _toolDispatcher = toolDispatcher;
            _hologramService = hologramService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            var greeting = "Welcome Sir. Shall I initialize the system diagnostics?";
            await Clients.Caller.SendAsync("ReceiveMessage", greeting);
        }

        public async Task SendText(string text)
        {
            await Clients.Caller.SendAsync("UpdateCoreState", "THINKING");

            // Pass the raw user input directly to the LLM.
            // The LLM will decide via Function Calling whether to return standard text, or an internal command string (e.g. "/tool ImageSearch")
            var response = await _aiProvider.GenerateResponseAsync(text);

            // Process LLM routing outcomes
            if (response.StartsWith("/tool "))
            {
                var parts = response.Substring(6).Split(' ', 2);
                var toolName = parts[0];
                var args = parts.Length > 1 ? parts[1] : "";

                var (success, result, permission) = await _toolDispatcher.TryExecuteToolAsync(toolName, args);

                if (permission >= PermissionLevel.Dangerous)
                {
                    await Clients.Caller.SendAsync("RequireUserConsent", toolName, args);
                    await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
                    return;
                }

                await HandleToolResultAsync(result);
                return;
            }

            if (response.StartsWith("/hologram start "))
            {
                var objectName = response.Substring(16).Trim();
                _logger.LogInformation("LLM triggered holographic generation for: {ObjectName}", objectName);

                await Clients.Caller.SendAsync("ReceiveMessage", $"Accessing processing cluster. Generating 3D model of {objectName} now, Sir.");
                await Clients.Caller.SendAsync("HologramGenerationStarted", objectName);
                await Clients.Caller.SendAsync("UpdateCoreState", "PROCESSING");

                try
                {
                    var modelUrl = await _hologramService.GenerateHologramAsync(objectName);
                    await Clients.Caller.SendAsync("HologramReady", modelUrl);
                    await Clients.Caller.SendAsync("ReceiveMessage", "Holographic projection initialized. You can now use hand gestures to interact, Sir.");
                }
                catch (Exception)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "I encountered a critical error while synthesizing the spatial mesh, Sir.");
                    await Clients.Caller.SendAsync("DeactivateHologramMode");
                }
                finally
                {
                    await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
                }
                return;
            }

            if (response == "/hologram stop")
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Deactivating hologram mode, Sir.");
                await Clients.Caller.SendAsync("DeactivateHologramMode");
                await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
                return;
            }

            // Normal text response
            await Clients.Caller.SendAsync("ReceiveMessage", response);
            await Clients.Caller.SendAsync("UpdateCoreState", "STANDBY");
        }

        public async Task ProcessCameraFrame(string base64Image)
        {
            await Task.CompletedTask;
        }

        public async Task GrantPermission(string toolName, string arguments)
        {
            await Clients.Caller.SendAsync("UpdateCoreState", "PROCESSING");

            var (success, result, permission) = await _toolDispatcher.TryExecuteToolAsync(toolName, arguments);
            await HandleToolResultAsync(result);
        }

        private async Task HandleToolResultAsync(string result)
        {
            if (result.StartsWith("IMAGE_FOUND|"))
            {
                var parts = result.Split('|', 3);
                var url = parts[1];
                var msg = parts.Length > 2 ? parts[2] : "Image retrieved, Sir.";

                await Clients.Caller.SendAsync("DisplayImages", new[] { url });
                await Clients.Caller.SendAsync("ReceiveMessage", msg);
            }
            else if (result.StartsWith("IMAGE_NOT_FOUND|") || result.StartsWith("IMAGE_ERROR|"))
            {
                var msg = result.Split('|', 2)[1];
                await Clients.Caller.SendAsync("ReceiveMessage", msg);
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", result);
            }
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
            builder.Services.AddSignalR(options => {
                options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
            });

            builder.Services.AddHttpClient<IAiProvider, OllamaAiProvider>();
            builder.Services.AddHttpClient<IAtlasTool, ImageSearchTool>();
            builder.Services.AddHttpClient<IHologramGenerationService, HologramGenerationService>();

            builder.Services.AddScoped<IMemoryRepository, MemoryRepository>();
            builder.Services.AddTransient<IAtlasTool, MemoryTool>();

            builder.Services.AddTransient<IAtlasTool, TimeTool>();
            builder.Services.AddTransient<IAtlasTool, SystemControlTool>();

            builder.Services.AddSingleton<PluginLoader>();

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
