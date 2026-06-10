namespace ImageBoardCTF.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "user";
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}
