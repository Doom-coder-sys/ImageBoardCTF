using ImageBoardCTF.Data;
using ImageBoardCTF.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

public class UserController : Controller
{
    private readonly Database _database;
    private readonly AuthService _auth;

    public UserController(Database database, AuthService auth)
    {
        _database = database;
        _auth = auth;
    }

    [HttpGet("/user/create-post")]
    public IActionResult CreatePost()
    {
        if (!_auth.HasRole("user", "moderator", "admin")) return RedirectToAction("Login", "Auth");
        ViewBag.MyPosts = _database.GetPostsForUser(_auth.UserId!.Value);
        return View();
    }

    [HttpPost("/user/create-post")]
    public IActionResult CreatePostPost(string? title, string? body, string? imageUrl)
    {
        if (!_auth.HasRole("user", "moderator", "admin")) return RedirectToAction("Login", "Auth");

        title = (title ?? "").Trim();
        body = body ?? "";
        imageUrl = (imageUrl ?? "").Trim();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            ViewBag.Error = "Title and body are required.";
            ViewBag.MyPosts = _database.GetPostsForUser(_auth.UserId!.Value);
            return View("CreatePost");
        }

        _database.CreatePost(_auth.UserId!.Value, title, body, imageUrl, true);
        return RedirectToAction("CreatePost");
    }
}
