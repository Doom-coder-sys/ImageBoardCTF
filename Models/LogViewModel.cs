namespace ImageBoardCTF.Models;

public class LogViewModel
{
    public List<LogRecord> Records { get; set; } = new();
    public string? SelectedFile { get; set; }
    public string? FileContent { get; set; }
    public string? Error { get; set; }
}
