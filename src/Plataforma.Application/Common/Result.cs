namespace Plataforma.Application.Common;

/// <summary>
/// Resultado de caso de uso sem usar exceções para fluxo esperado
/// (e-mail já em uso, credencial inválida). O <c>Code</c> é estável e o
/// controller o traduz para o status HTTP correto.
/// </summary>
public sealed record Result<T>(bool Success, T? Value, string? Error, string? Code)
{
    public static Result<T> Ok(T value) => new(true, value, null, null);
    public static Result<T> Fail(string error, string code) => new(false, default, error, code);
}
