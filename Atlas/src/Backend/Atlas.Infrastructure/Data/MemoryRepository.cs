using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Atlas.Core;

namespace Atlas.Infrastructure.Data
{
    public class MemoryRepository : IMemoryRepository
    {
        private readonly AtlasDbContext _context;

        public MemoryRepository(AtlasDbContext context)
        {
            _context = context;
        }

        public async Task SaveMemoryAsync(string category, string key, string value)
        {
            var existing = await _context.MemoryFragments
                .FirstOrDefaultAsync(m => m.Category == category && m.Key == key);

            if (existing != null)
            {
                existing.Value = value;
                existing.DateLearned = DateTime.UtcNow;
            }
            else
            {
                _context.MemoryFragments.Add(new MemoryFragment
                {
                    Category = category,
                    Key = key,
                    Value = value,
                    DateLearned = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<string?> GetMemoryAsync(string category, string key)
        {
            var memory = await _context.MemoryFragments
                .FirstOrDefaultAsync(m => m.Category == category && m.Key == key);

            return memory?.Value;
        }

        public async Task<IEnumerable<MemoryFragment>> GetAllMemoriesAsync()
        {
            return await _context.MemoryFragments.ToListAsync();
        }
    }
}
