using ImageBoardCTF.Data;
using ImageBoardCTF.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

public class ModeratorController : Controller
{
    private readonly Database _database;
    private readonly AuthService _auth;

    public ModeratorController(Database database, AuthService auth)
    {
        _database = database;
        _auth = auth;
    }

    [HttpGet("/moderator/requests")]
    public IActionResult Requests()
    {
        if (!_auth.HasRole("moderator", "admin")) return RedirectToAction("Login", "Auth");
        ViewBag.Message = TempData["Message"];
        ViewBag.Error = TempData["Error"];
        return View(_database.GetRegistrationRequests());
    }

    [HttpPost("/moderator/approve")]
    public IActionResult Approve(int id, string? requestedRole)
    {
        if (!_auth.HasRole("moderator", "admin")) return RedirectToAction("Login", "Auth");

        var request = _database.GetRegistrationRequest(id);
        if (request is null)
        {
            TempData["Error"] = "Request not found.";
            return RedirectToAction("Requests");
        }

        if (_database.UsernameExists(request.Username))
        {
            TempData["Error"] = "Username already exists.";
            return RedirectToAction("Requests");
        }

        var finalRole = string.IsNullOrWhiteSpace(requestedRole)
            ? request.RequestedRole
            : requestedRole.Trim().ToLowerInvariant();

        _database.ApproveRegistrationRequest(id, finalRole, _auth.Username);
        TempData["Message"] = $"Request #{id} approved as {finalRole}.";
        return RedirectToAction("Requests");
    }

    [HttpPost("/moderator/create-user")]
    public IActionResult CreateUser(string username, string password, string role, string displayName)
    {
        if (!_auth.HasRole("moderator", "admin")) return RedirectToAction("Login", "Auth");

        role = role?.Trim().ToLowerInvariant() ?? "user";
        if (role != "user" && role != "moderator")
        {
            TempData["Error"] = "Moderators can only create users or moderators.";
            return RedirectToAction("Requests");
        }

        var created = _database.CreateUser(username.Trim(), password, role, displayName.Trim(), "created from moderator console");
        TempData[created ? "Message" : "Error"] = created ? "Account created." : "Username already exists.";
        return RedirectToAction("Requests");
    }
}
