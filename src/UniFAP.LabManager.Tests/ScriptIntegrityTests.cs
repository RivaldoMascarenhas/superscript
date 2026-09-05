using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class ScriptIntegrityTests
{
    private readonly string _repoRoot;

    public ScriptIntegrityTests()
    {
        string current = AppDomain.CurrentDomain.BaseDirectory;
        _repoRoot = current;

        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(current, "UniFAP.LabManager.sln")))
            {
                _repoRoot = current;
                break;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
    }

    [Theory]
    [InlineData("scripts/AD-Join.ps1")]
    [InlineData("scripts/Performance-Optimize.ps1")]
    [InlineData("scripts/Set-GlobalWallpaperAndShortcuts.ps1")]
    [InlineData("scripts/User-Provision.ps1")]
    [InlineData("scripts/Windows-Repair.ps1")]
    [InlineData("lab.ps1")]
    [InlineData("Build.ps1")]
    [InlineData("Run.ps1")]
    [InlineData("Test.ps1")]
    [InlineData("Publish.ps1")]
    [InlineData("build/build.ps1")]
    [InlineData("build/install.ps1")]
    public void PowerShellScripts_MustParseWithoutSyntaxErrors(string relativeScriptPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_repoRoot, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.True(File.Exists(fullPath), $"Script não encontrado no caminho: {fullPath}");

        string escapedPath = fullPath.Replace("'", "''");
        string psCommand = "$tokens = $null; $err = $null; $null = [System.Management.Automation.Language.Parser]::ParseFile('" + escapedPath + "', [ref]$tokens, [ref]$err); if ($err -and $err.Count -gt 0) { $err | ForEach-Object { Write-Error $_.Message }; exit 1 } else { exit 0 }";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(psCommand));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);

        Assert.True(process.ExitCode == 0, $"Erro de sintaxe detectado em {relativeScriptPath}:\n{stderr}\n{stdout}");
    }

    [Fact]
    public void ADJoinScript_SimulationMode_ReturnsSuccessJson()
    {
        string scriptPath = Path.GetFullPath(Path.Combine(_repoRoot, "scripts", "AD-Join.ps1"));
        string psCommand = "$sec = ConvertTo-SecureString 'dummy' -AsPlainText -Force; & '" + scriptPath.Replace("'", "''") + "' -WhatIf -Domain unifapce.edu.br -Username 'admin' -SecurePassword $sec -OUPath 'OU=Computers,DC=unifapce,DC=edu,DC=br'";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(psCommand));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("\"Success\":true", stdout);
        Assert.Contains("Simulacao", stdout);
    }

    [Fact]
    public void PerformanceOptimizeScript_RollbackMode_ExecutesSuccessfully()
    {
        string scriptPath = Path.GetFullPath(Path.Combine(_repoRoot, "scripts", "Performance-Optimize.ps1"));
        string psCommand = "& '" + scriptPath.Replace("'", "''") + "' -Rollback";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(psCommand));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        Assert.NotNull(process);

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("\"Success\":true", stdout);
        Assert.Contains("Reversao", stdout);
    }
}
