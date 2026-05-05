namespace ObfusCal.Application.Interfaces;

public interface ISecretProvider
{
    string? GetSecret(string key);
}

