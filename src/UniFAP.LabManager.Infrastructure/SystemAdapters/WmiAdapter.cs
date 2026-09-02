using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Principal;
using Microsoft.Win32;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Infrastructure.SystemAdapters;

public class WmiAdapter
{
    private readonly ILogService _logger;

    public WmiAdapter(ILogService logger)
    {
        _logger = logger;
    }

    public SystemInfo CollectSystemInfo()
    {
        var info = new SystemInfo();

        try
        {
            info.ComputerName = Environment.MachineName;

            // 1. Privilégio de Administrador
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            info.IsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);

            // 2. Win32_OperatingSystem
            using (var osSearcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber, OSArchitecture, FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in osSearcher.Get())
                {
                    info.OperatingSystem = obj["Caption"]?.ToString() ?? "Windows 11";
                    info.OsVersion = obj["Version"]?.ToString() ?? "";
                    info.BuildNumber = obj["BuildNumber"]?.ToString() ?? "";
                    info.Architecture = obj["OSArchitecture"]?.ToString() ?? (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");

                    if (ulong.TryParse(obj["TotalVisibleMemorySize"]?.ToString(), out ulong totalKb))
                    {
                        info.TotalMemoryGb = Math.Round(totalKb / 1024.0 / 1024.0, 1);
                    }
                    if (ulong.TryParse(obj["FreePhysicalMemory"]?.ToString(), out ulong freeKb))
                    {
                        info.FreeMemoryGb = Math.Round(freeKb / 1024.0 / 1024.0, 1);
                    }
                }
            }

            // 3. Win32_Processor
            using (var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor"))
            {
                foreach (ManagementObject obj in cpuSearcher.Get())
                {
                    info.ProcessorName = obj["Name"]?.ToString()?.Trim() ?? "Processador";
                    if (int.TryParse(obj["NumberOfCores"]?.ToString(), out int cores))
                    {
                        info.ProcessorCores = cores;
                    }
                    break;
                }
            }

            // 4. Win32_ComputerSystem
            using (var csSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, Domain, PartOfDomain FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in csSearcher.Get())
                {
                    info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "Fabricante";
                    info.Model = obj["Model"]?.ToString() ?? "Modelo";
                    info.CurrentDomain = obj["Domain"]?.ToString() ?? "WORKGROUP";
                    info.IsDomainJoined = Convert.ToBoolean(obj["PartOfDomain"] ?? false);
                }
            }

            // 5. Disco do Sistema (C:)
            var driveC = new DriveInfo("C");
            if (driveC.IsReady)
            {
                info.SystemDiskTotalGb = Math.Round(driveC.TotalSize / 1024.0 / 1024.0 / 1024.0, 1);
                info.SystemDiskFreeGb = Math.Round(driveC.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
            }

            // 6. Rede e Detecção Precisa de Placa Cabeada (Ethernet) e Múltiplos Adaptadores
            info.IsNetworkConnected = NetworkInterface.GetIsNetworkAvailable();
            var candidateAdapters = new List<NetworkAdapterInfo>();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var ipProps = ni.GetIPProperties();
                var unicast = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (unicast == null) continue;

                string ip = unicast.Address.ToString();
                if (ip.StartsWith("127.") || ip.StartsWith("169.254.")) continue;

                string nameLower = (ni.Name + " " + ni.Description).ToLowerInvariant();
                bool isVirtual = nameLower.Contains("virtual") || 
                                 nameLower.Contains("vethernet") || 
                                 nameLower.Contains("hyper-v") || 
                                 nameLower.Contains("vmware") || 
                                 nameLower.Contains("virtualbox") || 
                                 nameLower.Contains("wsl") || 
                                 nameLower.Contains("tap") || 
                                 nameLower.Contains("vpn") ||
                                 nameLower.Contains("npcap");

                bool isEthernet = ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                  ni.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                                  ni.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT ||
                                  nameLower.Contains("ethernet") || 
                                  nameLower.Contains("cabeada") ||
                                  nameLower.Contains("realtek pcie") ||
                                  nameLower.Contains("intel(r) ethernet");

                var gateway = ipProps.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                string gatewayStr = gateway?.Address.ToString() ?? "";

                var adapterInfo = new NetworkAdapterInfo
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    Type = isEthernet ? "Ethernet (Cabeada)" : (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : ni.NetworkInterfaceType.ToString()),
                    IpAddress = ip,
                    DefaultGateway = gatewayStr,
                    IsPhysical = !isVirtual,
                    IsEthernet = isEthernet && !isVirtual,
                    IsUp = true
                };

                candidateAdapters.Add(adapterInfo);

                foreach (var dns in ipProps.DnsAddresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    if (!info.DnsServers.Contains(dns.ToString()))
                    {
                        info.DnsServers.Add(dns.ToString());
                    }
                }
            }

            info.ConnectedAdapters = candidateAdapters;

            // Priorização: Placa Cabeada Física com Gateway -> Placa Wi-Fi Física -> Qualquer Físico -> Primeiro
            var primaryAdapter = candidateAdapters.FirstOrDefault(a => a.IsEthernet && a.IsPhysical && !string.IsNullOrEmpty(a.DefaultGateway))
                                ?? candidateAdapters.FirstOrDefault(a => a.IsEthernet && a.IsPhysical)
                                ?? candidateAdapters.FirstOrDefault(a => a.IsPhysical && !string.IsNullOrEmpty(a.DefaultGateway))
                                ?? candidateAdapters.FirstOrDefault(a => a.IsPhysical)
                                ?? candidateAdapters.FirstOrDefault();

            if (primaryAdapter != null)
            {
                info.IpAddress = primaryAdapter.IpAddress;
                info.DefaultGateway = primaryAdapter.DefaultGateway;
            }

            var eth = candidateAdapters.FirstOrDefault(a => a.IsEthernet && a.IsPhysical);
            info.EthernetIpAddress = eth != null ? eth.IpAddress : "Desconectada";

            var summaryList = candidateAdapters
                .Where(a => a.IsPhysical || candidateAdapters.Count <= 2)
                .Select(a => $"{a.Type}: {a.IpAddress}")
                .ToList();
            info.AllIpAddressesSummary = summaryList.Count > 0 ? string.Join(" | ", summaryList) : info.IpAddress;

            // 7. Pendência de Reinicialização (Registry Check)
            info.HasPendingReboot = CheckPendingRebootRegistry();

            // 8. Winget Check
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string wingetPath = Path.Combine(localAppData, @"Microsoft\WindowsApps\winget.exe");
            info.IsWingetAvailable = File.Exists(wingetPath) || IsExecutableInPath("winget.exe");
        }
        catch (Exception ex)
        {
            _logger.LogError("WmiAdapter", "Erro ao coletar dados de hardware e WMI", ex);
        }

        return info;
    }

    private bool CheckPendingRebootRegistry()
    {
        try
        {
            using var cbsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            if (cbsKey != null) return true;

            using var wuKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (wuKey != null) return true;

            using var sessionKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (sessionKey != null && sessionKey.GetValue("PendingFileRenameOperations") != null) return true;
        }
        catch { }
        return false;
    }

    private bool IsExecutableInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv == null) return false;

        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir.Trim(), exeName)))
                    return true;
            }
            catch { }
        }
        return false;
    }
}
