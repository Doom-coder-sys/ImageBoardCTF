namespace ImageBoardCTF.Models;

public class RegistrationRequest
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Note { get; set; } = "";
    public string RequestedRole { get; set; } = "user";
    public string Status { get; set; } = "pending";
    public string CreatedAt { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
}
