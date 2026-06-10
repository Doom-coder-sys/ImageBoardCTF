using System.Text.Json;
using ImageBoardCTF.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace ImageBoardCTF.Controllers;

[ApiController]
[Route("api/posts")]
public class PostsApiController : ControllerBase
{
    private readonly Database _database;

    public PostsApiController(Database database)
    {
        _database = database;
    }

    [HttpPost("get")]
    public async Task<IActionResult> Get()
    {
        var id = await ReadInput("id");
        if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { error = "id required" });
        if (LooksLikeBatch(id))
        {
            return BadRequest(new { error = "legacy parser rejected batch input" });
        }

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();

        // Kept flexible for the old moderation widget, it sends ids as strings.
        command.CommandText = $"""
            SELECT p.Id, p.Title, p.Body, u.Username, p.CreatedAt
            FROM Posts p JOIN Users u ON u.Id = p.UserId
            WHERE p.Id = {id} AND p.IsPublic = 1
        """;

        try
        {
            using var reader = command.ExecuteReader();
            var rows = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["id"] = reader.GetValue(0),
                    ["title"] = reader.GetValue(1),
                    ["body"] = reader.GetValue(2),
                    ["username"] = reader.GetValue(3),
                    ["createdAt"] = reader.GetValue(4)
                });
            }
            return Ok(new { rows });
        }
        catch (SqliteException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static bool LooksLikeBatch(string input)
    {
        return input.Contains(';');
    }

    private async Task<string?> ReadInput(string name)
    {
        if (Request.HasFormContentType)
        {
            return Request.Form[name].ToString();
        }

        if (Request.ContentLength is > 0)
        {
            using var document = await JsonDocument.ParseAsync(Request.Body);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var value))
            {
                return value.ToString();
            }
        }

        return Request.Query[name].ToString();
    }
}
