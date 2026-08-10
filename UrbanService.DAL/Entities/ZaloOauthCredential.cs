namespace UrbanService.DAL.Entities;

public class ZaloOauthCredential
{
    public string OaId { get; set; } = null!;

    public string AccessTokenCiphertext { get; set; } = null!;

    public string? RefreshTokenCiphertext { get; set; }

    public DateTime AccessTokenExpiresAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
