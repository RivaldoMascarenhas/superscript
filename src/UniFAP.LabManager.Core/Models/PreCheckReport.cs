using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class PreCheckItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Geral";
    public PreCheckStatus Status { get; set; } = PreCheckStatus.Passed;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool IsBlocking { get; set; } = false;
}

public class PreCheckReport
{
    public DateTime EvaluatedAt { get; set; } = DateTime.Now;
    public bool IsReady => Items.All(i => !i.IsBlocking || i.Status != PreCheckStatus.Failed);
    public string Summary { get; set; } = "STATUS: PRONTO";
    public List<PreCheckItem> Items { get; set; } = new();
}
