using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace OptimalyAI.Controllers;

public class LogsController : Controller
{
    private readonly ILogger<LogsController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public LogsController(ILogger<LogsController> logger, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        ViewBag.LogFiles = GetAvailableLogFiles();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(string? fileName = null, int lines = 100, string? level = null, string? search = null)
    {
        try
        {
            var logPath = Path.Combine(_environment.ContentRootPath, "logs");
            
            // If no filename specified, get the latest log file
            if (string.IsNullOrEmpty(fileName))
            {
                var files = Directory.GetFiles(logPath, "*.log")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToList();
                
                if (!files.Any())
                {
                    return Json(new { success = false, error = "No log files found" });
                }
                
                fileName = Path.GetFileName(files.First());
            }

            var fullPath = Path.Combine(logPath, fileName);
            
            // Security check - ensure file is within logs directory
            if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(logPath)))
            {
                return Json(new { success = false, error = "Invalid file path" });
            }

            if (!System.IO.File.Exists(fullPath))
            {
                return Json(new { success = false, error = "Log file not found" });
            }

            // Read file with sharing enabled (so it can be read while being written to)
            var allLines = new List<string>();
            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                string? line;
                while ((line = await streamReader.ReadLineAsync()) != null)
                {
                    allLines.Add(line);
                }
            }

            // Apply filters
            var filteredLines = allLines;
            
            if (!string.IsNullOrEmpty(level) && level != "ALL")
            {
                filteredLines = filteredLines.Where(l => l.Contains($"level={level}", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            if (!string.IsNullOrEmpty(search))
            {
                filteredLines = filteredLines.Where(l => l.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Get last N lines
            var resultLines = filteredLines.TakeLast(lines).ToList();

            return Json(new { 
                success = true, 
                logs = resultLines,
                fileName = fileName,
                totalLines = allLines.Count,
                filteredLines = filteredLines.Count,
                displayedLines = resultLines.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading log file");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetLogFiles()
    {
        try
        {
            var files = GetAvailableLogFiles();
            return Json(new { success = true, files });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log files");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult ClearLogs(string fileName)
    {
        try
        {
            if (_environment.IsDevelopment())
            {
                var logPath = Path.Combine(_environment.ContentRootPath, "logs");
                var fullPath = Path.Combine(logPath, fileName);
                
                // Security check
                if (!Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(logPath)))
                {
                    return Json(new { success = false, error = "Invalid file path" });
                }

                if (System.IO.File.Exists(fullPath))
                {
                    // Create backup before clearing
                    var backupPath = fullPath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                    System.IO.File.Copy(fullPath, backupPath);
                    
                    // Clear the file
                    System.IO.File.WriteAllText(fullPath, string.Empty);
                    
                    _logger.LogInformation("Cleared log file {FileName}, backup created at {BackupPath}", fileName, backupPath);
                    return Json(new { success = true, message = "Log file cleared and backed up" });
                }
                
                return Json(new { success = false, error = "File not found" });
            }
            
            return Json(new { success = false, error = "Log clearing is only allowed in development mode" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing log file");
            return Json(new { success = false, error = ex.Message });
        }
    }

    private List<LogFileInfo> GetAvailableLogFiles()
    {
        var logPath = Path.Combine(_environment.ContentRootPath, "logs");
        
        if (!Directory.Exists(logPath))
        {
            return new List<LogFileInfo>();
        }

        return Directory.GetFiles(logPath, "*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new LogFileInfo
            {
                Name = f.Name,
                Size = FormatFileSize(f.Length),
                LastModified = f.LastWriteTime,
                IsActive = f.LastWriteTime > DateTime.Now.AddMinutes(-5) // Consider active if written to in last 5 minutes
            })
            .ToList();
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    public class LogFileInfo
    {
        public required string Name { get; set; }
        public required string Size { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsActive { get; set; }
    }
}