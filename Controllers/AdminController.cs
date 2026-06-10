using ImageBoardCTF.Data;
using ImageBoardCTF.Models;
using ImageBoardCTF.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageBoardCTF.Controllers;

public class AdminController : Controller
{
    private readonly Database _database;
    private readonly AuthService _auth;

    public AdminController(Database database, AuthService auth)
    {
        _database = database;
        _auth = auth;
    }

    [HttpGet("/admin/logs")]
    public IActionResult Logs(string? file)
    {
        if (!_auth.HasRole("admin")) return RedirectToAction("Login", "Auth");

        var model = new LogViewModel
        {
            Records = _database.GetLogRecords(),
            SelectedFile = file
        };

        if (!string.IsNullOrWhiteSpace(file))
        {
            var path = Path.Combine(_database.LogsDirectory, file);
            try
            {
                model.FileContent = System.IO.File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                model.Error = ex.Message;
            }
        }

        return View(model);
    }

    [HttpGet("/admin/create-admin")]
    public IActionResult CreateAdmin()
    {
        if (!_auth.HasRole("admin")) return RedirectToAction("Login", "Auth");
        return View();
    }

    [HttpPost("/admin/create-admin")]
    public IActionResult CreateAdminPost(string username, string password, string displayName)
    {
        if (!_auth.HasRole("admin")) return RedirectToAction("Login", "Auth");
        var created = _database.CreateUser(username.Trim(), password, "admin", displayName.Trim(), "created from admin console");
        ViewBag.Message = created ? "Admin account created." : "Username already exists.";
        return View("CreateAdmin");
    }
}
