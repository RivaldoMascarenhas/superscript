namespace UniFAP.LabManager.Core.Models;

public class LaboratoryProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool JoinDomain { get; set; } = false;
    public List<string> Software { get; set; } = new();
}
