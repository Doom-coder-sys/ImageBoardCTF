namespace ImageBoardCTF.Models;

public class Post
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool IsPublic { get; set; }
    public string CreatedAt { get; set; } = "";
}
