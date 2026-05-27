namespace CoffeeShopApi.Services;

public sealed class InMemoryTokenBlacklist : ITokenBlacklist
{
    private readonly Dictionary<string, DateTime> _revoked = new();
    private readonly object _lock = new();

    public void Revoke(string jti, DateTime expiry)
    {
        lock (_lock)
        {
            _revoked[jti] = expiry;
            // Purge entries that have already expired to keep the dictionary small
            var now = DateTime.UtcNow;
            foreach (var key in _revoked.Keys.Where(k => _revoked[k] < now).ToList())
                _revoked.Remove(key);
        }
    }

    public bool IsRevoked(string jti)
    {
        lock (_lock)
            return _revoked.ContainsKey(jti);
    }
}
