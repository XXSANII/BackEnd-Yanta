using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using YantaBackEnd.Options;

namespace YantaBackEnd.Services;

/// <summary>
/// token ของลูกค้า = {sessionId}.{HMAC(sessionId)}
/// คนที่สแกน QR ของโต๊ะเดียวกันได้ token เดิม จึงใช้ร่วมกันหลายเครื่องได้
/// </summary>
public sealed class TableSessionTokenService
{
    private readonly byte[] _key;

    public TableSessionTokenService(IOptions<TableSessionOptions> options)
    {
        _key = Encoding.UTF8.GetBytes(options.Value.Secret);
    }

    public string Create(Guid sessionId)
    {
        var id = sessionId.ToString("N");
        return $"{id}.{Sign(id)}";
    }

    public bool TryParse(string? token, out Guid sessionId)
    {
        sessionId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');

        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var id))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(parts[0]));
        var actual = Encoding.ASCII.GetBytes(parts[1]);

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return false;
        }

        sessionId = id;
        return true;
    }

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private string Sign(string value)
    {
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
