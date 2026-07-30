namespace Plataforma.Application.Abstractions;

/// <summary>Hashing e verificação de senha. Implementação concreta (Argon2id) vive na Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);

    /// <summary>Executa um hash de custo IDÊNTICO ao <see cref="Verify"/> e descarta o
    /// resultado. Usado no login quando o e-mail NÃO existe, para o tempo de resposta
    /// não revelar se a conta existe (anti-enumeração por timing).</summary>
    void BurnDummy(string password);
}
