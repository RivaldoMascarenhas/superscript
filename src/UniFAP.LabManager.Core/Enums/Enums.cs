namespace UniFAP.LabManager.Core.Enums;

public enum ComputerType
{
    Administrative,
    Laboratory
}

public enum JobStatus
{
    Pending,
    Running,
    Succeeded,
    Warning,
    Failed,
    Cancelled
}

public enum StepStatus
{
    Pending,
    Running,
    Succeeded,
    Warning,
    Failed,
    Skipped
}

public enum StepType
{
    PreCheck,
    ComputerName,
    Windows,
    Users,
    Branding,
    Performance,
    Networking,
    Software,
    ActiveDirectory,
    Reboot,
    Validation,
    Report
}

public enum SoftwareType
{
    Winget,
    Local,
    Msi,
    Exe,
    Script,
    Legacy
}

public enum SoftwareSeverity
{
    Critical,
    Warning,
    Optional
}

public enum SoftwareInstallStatus
{
    Pending,
    Installing,
    Installed,
    Warning,
    Failed,
    Skipped
}

public enum HealthStatus
{
    Good,
    Warning,
    Critical,
    Unknown
}

public enum PreCheckStatus
{
    Passed,
    Warning,
    Failed
}
