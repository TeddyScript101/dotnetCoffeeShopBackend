namespace CoffeeShopApi.Services;

public interface ITokenBlacklist
{
    void Revoke(string jti, DateTime expiry);
    bool IsRevoked(string jti);
}
