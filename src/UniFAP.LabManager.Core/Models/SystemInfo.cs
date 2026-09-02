namespace UniFAP.LabManager.Core.Models;

public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "Ethernet";
    public string IpAddress { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public bool IsPhysical { get; set; } = true;
    public bool IsEthernet { get; set; } = false;
    public bool IsUp { get; set; } = false;
}

public class SystemInfo
{
    public string ComputerName { get; set; } = Environment.MachineName;
    public string Manufacturer { get; set; } = "Generic";
    public string Model { get; set; } = "Generic PC";
    public string OperatingSystem { get; set; } = "Windows 11";
    public string OsVersion { get; set; } = "24H2";
    public string BuildNumber { get; set; } = "";
    public string Architecture { get; set; } = "x64";
    public string ProcessorName { get; set; } = "Unknown CPU";
    public int ProcessorCores { get; set; } = 4;
    public double TotalMemoryGb { get; set; } = 16.0;
    public double FreeMemoryGb { get; set; } = 10.0;
    public double SystemDiskTotalGb { get; set; } = 256.0;
    public double SystemDiskFreeGb { get; set; } = 120.0;
    public bool IsAdministrator { get; set; } = false;
    public string AdministratorStatusDisplay => IsAdministrator ? "Sim (Elevado)" : "Não (Execute como Admin)";
    public bool IsDomainJoined { get; set; } = false;
    public string CurrentDomain { get; set; } = "WORKGROUP";
    public string DomainController { get; set; } = "";
    
    // REDE DETALHADA
    public string IpAddress { get; set; } = "127.0.0.1";
    public string EthernetIpAddress { get; set; } = "";
    public string DefaultGateway { get; set; } = "";
    public List<string> DnsServers { get; set; } = new();
    public List<NetworkAdapterInfo> ConnectedAdapters { get; set; } = new();
    public string AllIpAddressesSummary { get; set; } = "";

    public bool IsNetworkConnected { get; set; } = false;
    public bool IsInternetConnected { get; set; } = false;
    public bool IsWingetAvailable { get; set; } = false;
    public bool HasPendingReboot { get; set; } = false;
    public bool IsWindowsDefenderActive { get; set; } = true;
    public bool IsFirewallActive { get; set; } = true;
    public bool IsUacEnabled { get; set; } = true;
}
