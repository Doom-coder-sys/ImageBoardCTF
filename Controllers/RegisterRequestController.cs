using ImageBoardCTF.Data;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

public class RegisterRequestController : Controller
{
    private readonly Database _database;

    public RegisterRequestController(Database database)
    {
        _database = database;
    }

    [HttpGet("/register-request")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost("/register-request")]
    public IActionResult CreatePost(string? username, string? password, string? displayName, string? note, string? requestedRole)
    {
        var cleanUsername = (username ?? "").Trim();
        var cleanPassword = password ?? "";
        var cleanDisplayName = string.IsNullOrWhiteSpace(displayName) ? cleanUsername : displayName.Trim();
        var cleanNote = note ?? "";

        if (string.IsNullOrWhiteSpace(cleanUsername) || string.IsNullOrWhiteSpace(cleanPassword))
        {
            ViewBag.Error = "Username and password are required.";
            return View("Create");
        }

        if (_database.UsernameExists(cleanUsername))
        {
            ViewBag.Error = "That username is already taken.";
            return View("Create");
        }

        var role = string.IsNullOrWhiteSpace(requestedRole) ? "user" : requestedRole.Trim().ToLowerInvariant();
        _database.CreateRegistrationRequest(cleanUsername, cleanPassword, cleanDisplayName, cleanNote, role);
        TempData["Message"] = "Request saved. A moderator will review it later.";
        return RedirectToAction("Create");
    }
}
