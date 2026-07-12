namespace Plataforma.Application.Abstractions;

/// <summary>Confirma as alterações pendentes numa única transação.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
