namespace ESD.UI.Services;

public class DatabaseService
{
    // Database provider is intentionally abstract in this starter project.
    // Add SQL Server/MySQL/PostgreSQL implementation after the host provider is confirmed.
    public bool IsAvailable => true;

    public Task SaveEventAsync(object eventData) => Task.CompletedTask;
    public Task SaveWorkingSessionAsync(object sessionData) => Task.CompletedTask;
}
