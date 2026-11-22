using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Anomaly.Server;

/// <summary>
/// D335: the hub's game token, which the launcher hands the client and the client shows in its <c>Hello</c>. An ES256 JWT
/// (<c>header.payload.signature</c>, base64url) that names the account (<c>sub</c>, <c>name</c>) and the one server it is for
/// (<c>aud</c>, "host:port" in lower case) and lives two minutes. The hub makes it (the launcher's repository, AnomalyLauncher,
/// <c>src/Contracts/Signing.cs</c>, whose <c>GameToken.Verify</c> this mirrors); a game server only checks it, with the hub's public key.
/// </summary>
public static class LoginToken
{
    private static readonly string Header = Base64Url.EncodeToString("{\"alg\":\"ES256\",\"typ\":\"JWT\"}"u8);

    /// <summary>
    /// True when <paramref name="token"/> was signed by <paramref name="hubKey"/> for one of <paramref name="audiences"/> and is valid at
    /// <paramref name="nowSeconds"/> (Unix time); <paramref name="name"/> is then the account's name and <paramref name="expires"/> the token's end.
    /// </summary>
    public static bool TryVerify(ECDsa hubKey, string token, IReadOnlyCollection<string> audiences, long nowSeconds, out string name, out long expires)
    {
        name = null;
        expires = 0;
        string[] parts = (token ?? "").Split('.');
        if (parts.Length != 3 || parts[0] != Header) // the one algorithm: the header is compared, never obeyed
            return false;
        try
        {
            if (!hubKey.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256))
                return false;
            using JsonDocument claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            JsonElement root = claims.RootElement;
            string account = root.GetProperty("sub").GetString(), audience = root.GetProperty("aud").GetString(), named = root.GetProperty("name").GetString();
            long issued = root.GetProperty("iat").GetInt64();
            expires = root.GetProperty("exp").GetInt64();
            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(named) || audience == null || !Contains(audiences, audience)
                || nowSeconds >= expires || issued > nowSeconds + 60)
                return false;
            name = named;
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException or CryptographicException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool Contains(IReadOnlyCollection<string> audiences, string audience)
    {
        foreach (string one in audiences)
            if (string.Equals(one, audience, StringComparison.Ordinal)) return true;
        return false;
    }
}
