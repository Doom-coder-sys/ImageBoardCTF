namespace ImageBoardCTF.Models;

public class LogRecord
{
    public int Id { get; set; }
    public string Level { get; set; } = "info";
    public string Area { get; set; } = "app";
    public string Message { get; set; } = "";
    public string FileName { get; set; } = "app.log";
    public string CreatedAt { get; set; } = "";
}
