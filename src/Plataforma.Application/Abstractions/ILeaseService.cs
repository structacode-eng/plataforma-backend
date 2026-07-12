namespace Plataforma.Application.Abstractions;

/// <summary>Lease emitido: o token assinado, sua expiração e os módulos que ele concede.</summary>
public sealed record LeaseResult(string Token, DateTime ExpiresAtUtc, IReadOnlyList<string> Modules);

/// <summary>
/// Emite o "lease" de licença: uma declaração ASSINADA (ES256) de quais módulos estão
/// liberados para um usuário/dispositivo, com validade curta (ex.: 24h). O plugin verifica
/// a assinatura com a chave pública embutida — a autoridade permanece no servidor (Princípio 1).
/// </summary>
public interface ILeaseService
{
    LeaseResult Issue(Guid userId, string fingerprint, IReadOnlyCollection<string> modules);
}
