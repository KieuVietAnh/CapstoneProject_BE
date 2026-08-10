namespace UrbanService.BLL.Services;

public sealed class ZaloTokenRefreshLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
