namespace UrbanService.BLL.Interfaces;

public interface IZaloAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
