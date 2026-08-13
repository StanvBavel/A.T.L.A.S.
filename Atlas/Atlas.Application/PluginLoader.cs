using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Atlas.Core;
using Microsoft.Extensions.Logging;

namespace Atlas.Application
{
    public class PluginLoader
    {
        private readonly ILogger<PluginLoader> _logger;
        private readonly string _pluginsDirectory;

        public PluginLoader(ILogger<PluginLoader> logger)
        {
            _logger = logger;
            _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        }

        public IEnumerable<IAtlasPlugin> LoadPlugins()
        {
            var plugins = new List<IAtlasPlugin>();

            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
                _logger.LogInformation("Created Plugins directory at {Path}", _pluginsDirectory);
                return plugins;
            }

            var dllFiles = Directory.GetFiles(_pluginsDirectory, "*.dll");

            foreach (var file in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IAtlasPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is IAtlasPlugin plugin)
                        {
                            plugin.Initialize();
                            plugins.Add(plugin);
                            _logger.LogInformation("Loaded plugin: {PluginName} v{Version}", plugin.PluginName, plugin.Version);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin from {File}", file);
                }
            }

            return plugins;
        }
    }
}
