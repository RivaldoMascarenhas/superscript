using System.IO;
using System.Security.Principal;
using System.Text.RegularExpressions;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Infrastructure.Security;

public class SecurityService : ISecurityService
{
    private static readonly (Regex Pattern, string Replacement)[] SanitizationRules = new[]
    {
        (new Regex(@"-EncodedCommand\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "-EncodedCommand [REDACTED]"),
        (new Regex(@"(password|pass|senha|secret|credential|pwd)\s*[:=]\s*([^\s,;""']+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "$1: [REDACTED]"),
        (new Regex(@"-String\s+['""][^'""]+['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled), "-String '[REDACTED]'"),
        (new Regex(@"-(SupportPassword|StudentPassword|SecurePassword|Password)\s+['""]?[^'""]+['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled), "-$1 [REDACTED]"),
        (new Regex(@"ConvertTo-SecureString\s+(?:-String\s+)?['""][^'""]+['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ConvertTo-SecureString -String '[REDACTED]'")
    };

    public bool IsElevatedAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public bool ValidatePathSafety(string relativeOrAbsolutePath, string allowedBaseDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
                return false;

            string fullBasePath = Path.GetFullPath(allowedBaseDirectory);
            string targetPath = Path.IsPathRooted(relativeOrAbsolutePath)
                ? Path.GetFullPath(relativeOrAbsolutePath)
                : Path.GetFullPath(Path.Combine(fullBasePath, relativeOrAbsolutePath));

            string relative = Path.GetRelativePath(fullBasePath, targetPath);
            return !Path.IsPathRooted(relative) && relative != ".." &&
                   !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public string SanitizeLogString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string sanitized = input;
        foreach (var (pattern, replacement) in SanitizationRules)
        {
            sanitized = pattern.Replace(sanitized, replacement);
        }

        return sanitized;
    }
}
