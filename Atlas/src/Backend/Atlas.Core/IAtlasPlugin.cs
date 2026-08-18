using System.Collections.Generic;

namespace Atlas.Core
{
    public interface IAtlasPlugin
    {
        string PluginName { get; }
        string Version { get; }

        void Initialize();
        IEnumerable<IAtlasTool> GetPluginTools();
    }
}
