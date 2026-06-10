using ImageBoardCTF.Data;
using ImageBoardCTF.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

public class HomeController : Controller
{
    private readonly Database _database;
    private readonly AuthService _auth;

    public HomeController(Database database, AuthService auth)
    {
        _database = database;
        _auth = auth;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        ViewBag.Role = _auth.Role;
        ViewBag.Username = _auth.Username;
        return View(_database.GetPublicPosts());
    }

    [HttpGet("/post/{id:int}")]
    public IActionResult Post(int id)
    {
        ViewBag.Role = _auth.Role;
        ViewBag.Username = _auth.Username;
        var post = _database.GetPost(id);
        if (post is null) return NotFound();
        return View(post);
    }
}
