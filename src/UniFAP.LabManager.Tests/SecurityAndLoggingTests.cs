using UniFAP.LabManager.Infrastructure.Security;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class SecurityAndLoggingTests
{
    private readonly SecurityService _securityService = new();

    [Theory]
    [InlineData("Executando com password: MySecretPassword123!", "Executando com password: [REDACTED]")]
    [InlineData("Parametro senha=SuperAdminPass2026", "Parametro senha: [REDACTED]")]
    [InlineData("Usuario suporte criado com credential: TempPass!", "Usuario suporte criado com credential: [REDACTED]")]
    [InlineData("Configuracao Password=Teste123", "Password: [REDACTED]")]
    [InlineData("ConvertTo-SecureString -String 'PlainTextSecret' -AsPlainText", "ConvertTo-SecureString -String '[REDACTED]'")]
    [InlineData("-SupportPassword AdminSuperPass2026!", "-SupportPassword [REDACTED]")]
    public void SanitizeLogString_ShouldMaskPasswordsAndCredentials(string input, string expectedSubstring)
    {
        string sanitized = _securityService.SanitizeLogString(input);
        Assert.DoesNotContain("MySecretPassword123!", sanitized);
        Assert.DoesNotContain("SuperAdminPass2026", sanitized);
        Assert.DoesNotContain("TempPass!", sanitized);
        Assert.DoesNotContain("Teste123", sanitized);
        Assert.DoesNotContain("PlainTextSecret", sanitized);
        Assert.DoesNotContain("AdminSuperPass2026!", sanitized);
        Assert.Contains(expectedSubstring, sanitized);
    }

    [Theory]
    [InlineData(@"C:\ProgramData\UniFAP\LabManager\software\Office", @"C:\ProgramData\UniFAP\LabManager\software", true)]
    [InlineData(@"software/Autodesk/AutoCAD", @"software", true)]
    [InlineData(@"..\..\evil.exe", @"C:\ProgramData\UniFAP\LabManager\software", false)]
    [InlineData(@"../../evil.exe", @"C:\ProgramData\UniFAP\LabManager\software", false)]
    [InlineData(@"C:\Windows\System32\cmd.exe", @"C:\ProgramData\UniFAP\LabManager\software", false)]
    [InlineData(@"\\server\share\evil.exe", @"C:\ProgramData\UniFAP\LabManager\software", false)]
    public void ValidatePathSafety_ShouldPreventDirectoryTraversal(string targetPath, string basePath, bool expectedValid)
    {
        bool isValid = _securityService.ValidatePathSafety(targetPath, basePath);
        Assert.Equal(expectedValid, isValid);
    }
}
