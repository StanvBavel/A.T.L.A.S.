using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Core;

namespace Atlas.Plugins
{
    // A mock plugin to demonstrate the dynamic loading structure
    public class SpotifyPlugin : IAtlasPlugin
    {
        public string PluginName => "SpotifyController";
        public string Version => "1.0.0";

        public void Initialize()
        {
            // E.g., authenticate with Spotify API here
            Console.WriteLine($"[PLUGIN] {PluginName} initialized.");
        }

        public IEnumerable<IAtlasTool> GetPluginTools()
        {
            return new List<IAtlasTool>
            {
                new SpotifyPlayTool()
            };
        }
    }

    public class SpotifyPlayTool : IAtlasTool
    {
        public string Name => "SpotifyPlay";
        public string Description => "Plays a song on Spotify.";
        public PermissionLevel RequiredPermission => PermissionLevel.Normal;

        public Task<string> ExecuteAsync(string arguments)
        {
            return Task.FromResult($"[Spotify] Nu aan het afspelen: {arguments}");
        }
    }
}
