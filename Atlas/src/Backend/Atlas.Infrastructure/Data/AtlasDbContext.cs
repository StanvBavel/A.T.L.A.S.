using Microsoft.EntityFrameworkCore;
using Atlas.Core;

namespace Atlas.Infrastructure.Data
{
    public class AtlasDbContext : DbContext
    {
        public AtlasDbContext(DbContextOptions<AtlasDbContext> options) : base(options)
        {
        }

        public DbSet<MemoryFragment> MemoryFragments { get; set; }
    }
}
