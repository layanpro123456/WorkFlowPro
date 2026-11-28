using Microsoft.AspNetCore.DataProtection;
using WorkFlowPro.Application.Common.Interfaces;

namespace WorkFlowPro.Infrastructure.Git;

public class GitSecretProtector : IGitSecretProtector
{
    private readonly IDataProtector _protector;

    public GitSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("WorkFlowPro.GitCredentials.v1");
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return string.Empty;
        }
        return _protector.Unprotect(ciphertext);
    }
}
