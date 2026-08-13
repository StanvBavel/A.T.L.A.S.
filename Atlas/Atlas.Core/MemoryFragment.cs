using System;

namespace Atlas.Core
{
    public class MemoryFragment
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime DateLearned { get; set; }
    }
}
