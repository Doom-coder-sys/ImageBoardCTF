using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageBoardCTF.Models;

namespace ImageBoardCTF.Services;

public class AuthService
{
    private const string JwtCookieName = "matrix_access";
    private const string JwtSecret = "matrix-dev-secret-2026";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ISession Session => _httpContextAccessor.HttpContext!.Session;

    public int? UserId => Session.GetInt32("UserId");
    public string Username => Session.GetString("Username") ?? "";
    public string Role => Session.GetString("Role") ?? "guest";
    public string DisplayName => Session.GetString("DisplayName") ?? Username;
    public string AvatarUrl => Session.GetString("AvatarUrl") ?? "/avatars/default.svg";
    public bool IsAuthenticated => UserId.HasValue;

    public void SignIn(User user)
    {
        Session.SetInt32("UserId", user.Id);
        Session.SetString("Username", user.Username);
        Session.SetString("Role", user.Role);
        Session.SetString("DisplayName", user.DisplayName);
        Session.SetString("AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/avatars/default.svg" : user.AvatarUrl);

        var token = CreateGatewayToken(user);
        _httpContextAccessor.HttpContext?.Response.Cookies.Append(JwtCookieName, token, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(6)
        });
    }

    public void SignOut()
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Delete(JwtCookieName);
        Session.Clear();
    }

    public bool HasRole(params string[] roles)
    {
        if (roles.Any(role => string.Equals(role, Role, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.Equals(Username, "silverhand", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Any(role => string.Equals(role, "moderator", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static string CreateGatewayToken(User user)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" })));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            sub = user.Id.ToString(),
            username = user.Username,
            displayName = user.DisplayName,
            role = user.Role,
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds(),
            iss = "matrix-gw-legacy"
        })));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(JwtSecret));
        var signature = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{header}.{payload}")));
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
