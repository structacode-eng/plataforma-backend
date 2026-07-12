namespace Plataforma.Application.Abstractions;

/// <summary>Hashing e verificação de senha. Implementação concreta (Argon2id) vive na Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}
