namespace OptimalyAI.ViewModels;

public abstract class BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BreadcrumbTitle { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}