using System.Threading.Tasks;

namespace Atlas.Core
{
    public interface IAtlasTool
    {
        string Name { get; }
        string Description { get; }
        PermissionLevel RequiredPermission { get; }

        Task<string> ExecuteAsync(string arguments);
    }
}
