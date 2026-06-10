using ImageBoardCTF.Data;
using ImageBoardCTF.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

public class AuthController : Controller
{
    private readonly Database _database;
    private readonly AuthService _auth;

    public AuthController(Database database, AuthService auth)
    {
        _database = database;
        _auth = auth;
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        if (_auth.IsAuthenticated) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost("/login")]
    public IActionResult LoginPost(string? username, string? password)
    {
        var cleanUsername = (username ?? "").Trim();
        var cleanPassword = password ?? "";
        var user = _database.FindUser(cleanUsername, cleanPassword);
        if (user is null)
        {
            ViewBag.Error = "Invalid username or password";
            return View("Login");
        }

        _auth.SignIn(user);
        return RedirectToAction("Index", "Home", new { rain = "1" });
    }

    [HttpPost("/logout")]
    public IActionResult Logout()
    {
        _auth.SignOut();
        return RedirectToAction("Index", "Home");
    }
}
