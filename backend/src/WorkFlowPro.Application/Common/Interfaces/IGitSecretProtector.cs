namespace WorkFlowPro.Application.Common.Interfaces;

public interface IGitSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
