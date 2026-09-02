namespace UniFAP.LabManager.Core.Models;

public class CatalogSourceConfig
{
    public string WinutilSourceUrl { get; set; } = "https://raw.githubusercontent.com/ChrisTitusTech/winutil/main/config/applications.json";
    public string FallbackLocalFile { get; set; } = "config/winutil-applications.json";
    public DateTime? LastSyncUtc { get; set; }
    public int TotalUniFapItems { get; set; }
    public int TotalWinUtilItems { get; set; }
    public int MergedItems { get; set; }
    public List<string> Categories { get; set; } = new()
    {
        "Browsers",
        "Development",
        "Document",
        "Education",
        "Games",
        "Multimedia",
        "Networking",
        "Utilities",
        "Microsoft Tools",
        "Pro Tools",
        "Communication",
        "Productivity",
        "Security",
        "Other"
    };
}

public class WinUtilAppEntry
{
    public string? Content { get; set; } // Nome de exibição
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Winget { get; set; }
    public string? Choco { get; set; }
    public string? Link { get; set; }
    public bool Foss { get; set; } = false;
}

public class CatalogSyncResult
{
    public bool Success { get; set; }
    public bool UsedLocalFallback { get; set; }
    public int UniFapItemCount { get; set; }
    public int WinUtilImportedCount { get; set; }
    public int MergedCount { get; set; }
    public int TotalFinalCount { get; set; }
    public DateTime SyncTimestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
    public List<string> AddedSoftwareNames { get; set; } = new();
    public List<string> MergedSoftwareNames { get; set; } = new();
}
