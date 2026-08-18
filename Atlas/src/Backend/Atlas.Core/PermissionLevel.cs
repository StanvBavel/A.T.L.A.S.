namespace Atlas.Core
{
    public enum PermissionLevel
    {
        Safe = 0,         // Read-only, no side effects (Weather, Time)
        Normal = 1,       // Minor side effects (Set Timer)
        Sensitive = 2,    // Read private data (Email, Agenda)
        Dangerous = 3,    // Modifies system (Delete files, Start unknown app)
        Critical = 4      // Shutdown, Format, modify registry
    }
}
