using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Plataforma.Application.Abstractions;

namespace Plataforma.Infrastructure.Security;

/// <summary>
/// Hashing de senha com Argon2id (RNF-SEC-002). Parâmetros seguem um ponto de partida
/// recomendado pela OWASP (m=64MB, t=3, p=4). O hash guardado é autodescritivo:
/// <c>argon2id$m=..,t=..,p=..$saltBase64$hashBase64</c>, permitindo evoluir os
/// parâmetros no futuro sem quebrar hashes antigos.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemoryKb = 65536; // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt, HashSize);
        return $"argon2id$m={MemoryKb},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "argon2id") return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Compute(password, salt, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Compute(string password, byte[] salt, int size)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemoryKb,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };
        return argon2.GetBytes(size);
    }
}
