using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageBoardCTF.Data;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

[ApiController]
public class DecoyController : ControllerBase
{
    private const string JwtCookieName = "matrix_access";
    private const string JwtSecret = "matrix-dev-secret-2026";
    private readonly Database _database;

    public DecoyController(Database database)
    {
        _database = database;
    }

    [HttpGet("/.env")]
    public IActionResult Env()
    {
        var content = """
            ASPNETCORE_ENVIRONMENT=Production
            BOARD_NAME=code-rain
            JWT_SECRET=matrix-dev-secret-2026
            JWT_COOKIE=matrix_access
            JWT_ALG=HS256
            ADMIN_API=/api/admin/panel
            DEBUG_ENDPOINT=/api/debug/session
            BACKUP_URL=/backup/matrix-board-backup.zip
            LEGACY_ADMIN=MatrixRoot
            LEGACY_PASSWORD=0101010101001101
            """;
        return Content(content, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/backup/matrix-board-backup.zip")]
    public IActionResult Backup()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "backup", "matrix-board-backup.zip");
        if (!System.IO.File.Exists(path)) return NotFound(new { error = "backup not found" });
        return PhysicalFile(path, "application/zip", "matrix-board-backup.zip");
    }

    [HttpGet("/api/debug/session")]
    public IActionResult DebugSession()
    {
        var token = GetToken();
        var validation = ValidateToken(token);
        return Ok(new
        {
            session = new
            {
                hasJwtCookie = Request.Cookies.ContainsKey(JwtCookieName),
                cookieName = JwtCookieName,
                tokenAccepted = validation.Valid,
                validation.Role,
                validation.Username,
                validation.Error
            },
            gateway = new
            {
                name = "matrix-gw-legacy",
                alg = "HS256",
                adminApi = "/api/admin/panel",
                backup = "/backup/matrix-board-backup.zip"
            }
        });
    }

    [HttpGet("/api/debug/config")]
    public IActionResult DebugConfig()
    {
        return Ok(new
        {
            board = "Кодовый Дождь",
            auth = new
            {
                cookie = JwtCookieName,
                alg = "HS256",
                issuer = "matrix-gw-legacy",
                roleClaim = "role"
            },
            endpoints = new[]
            {
                "/.env",
                "/api/admin/panel",
                "/api/admin/users",
                "/api/admin/rotate-cache",
                "/api/profiles/internal?id=1"
            }
        });
    }

    [HttpGet("/api/admin/panel")]
    public IActionResult AdminPanel()
    {
        var validation = ValidateToken(GetToken());
        if (!validation.Valid) return Unauthorized(new { error = "missing or invalid matrix_access token", expected = "HS256" });
        if (!string.Equals(validation.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { error = "admin role required", role = validation.Role });
        }

        return Ok(new
        {
            ok = true,
            user = validation.Username,
            role = validation.Role,
            gateway = "matrix-gw-legacy",
            actions = new[] { "read-shadow-users", "rotate-cache", "restore-backup" },
            debug = "/api/debug/session",
            backup = "/backup/matrix-board-backup.zip"
        });
    }

    [HttpGet("/api/admin/users")]
    public IActionResult ShadowUsers()
    {
        var validation = ValidateToken(GetToken());
        if (!validation.Valid) return Unauthorized(new { error = "missing or invalid matrix_access token" });
        if (!string.Equals(validation.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { error = "admin role required", role = validation.Role });
        }

        return Ok(new[]
        {
            new { username = "MatrixRoot", role = "admin", source = "legacy-backup" },
            new { username = "AdminMaybe", role = "admin", source = "legacy-backup" },
            new { username = "ZionAdmin", role = "admin", source = "legacy-backup" }
        });
    }

    [HttpPost("/api/admin/rotate-cache")]
    public IActionResult RotateCache()
    {
        var validation = ValidateToken(GetToken());
        if (!validation.Valid) return Unauthorized(new { error = "missing or invalid matrix_access token" });
        if (!string.Equals(validation.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { error = "admin role required", role = validation.Role });
        }

        return Ok(new
        {
            ok = true,
            status = "queued",
            job = $"cache-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            message = "rotation accepted by gateway"
        });
    }

    [HttpGet("/api/profiles/internal")]
    public IActionResult InternalProfile(int id = 1)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, DisplayName, Role, Bio, CreatedAt FROM Users WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return NotFound(new { error = "profile not found" });

        var username = reader.GetString(1);
        return Ok(new
        {
            id = reader.GetInt32(0),
            username,
            displayName = reader.GetString(2),
            role = reader.GetString(3),
            bio = reader.GetString(4),
            createdAt = reader.GetString(5),
            privateNote = BuildRecoveryNote(username)
        });
    }

    private string? GetToken()
    {
        var auth = Request.Headers["Authorization"].ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return auth[7..].Trim();
        }
        return Request.Cookies.TryGetValue(JwtCookieName, out var cookie) ? cookie : null;
    }

    private static (bool Valid, string? Username, string? Role, string? Error) ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, null, null, "token missing");
        var parts = token.Split('.');
        if (parts.Length != 3) return (false, null, null, "token format");

        var signed = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(JwtSecret));
        var expected = Base64Url(hmac.ComputeHash(signed));
        if (parts[2].Length != expected.Length || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
        {
            return (false, null, null, "bad signature");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            var username = payload is not null && payload.TryGetValue("username", out var u) ? u.GetString() : null;
            var role = payload is not null && payload.TryGetValue("role", out var r) ? r.GetString() : null;
            return (true, username, role, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    private static string BuildRecoveryNote(string username) => username.ToLowerInvariant() switch
    {
        "matrixroot" => "restore source: nightly backup",
        "adminmaybe" => "legacy admin record exists in backup metadata",
        "zionadmin" => "gateway accepts hs256 token during maintenance window",
        "neo" => "operator alias migrated from old board",
        _ => "profile exported by internal directory sync"
    };

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
