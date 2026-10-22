using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Auth;

public sealed class DesktopSession
{
    public string Token { get; init; } = "";
    public DateTime ExpiresAtUtc { get; init; }
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class DesktopMe
{
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Active { get; init; }

    /// <summary>Token novo para o plugin do Revit guardar no lugar do atual (null quando
    /// não precisa renovar). O plugin 2.7.1 e anteriores ignoram.</summary>
    public string? RenewedToken { get; init; }
    public DateTime? RenewedExpiresAtUtc { get; init; }

    /// <summary>Falha ao gravar o sinal de vida. Não derruba a validação: só vai para o
    /// log do servidor.</summary>
    public Exception? HeartbeatFailure { get; init; }
}

/// <summary>
/// Autenticação para o cliente desktop no modelo "sessão" (RF-CLI-002), que o gate do
/// plugin já espera: login devolve um token de sessão longo; /me revalida periodicamente.
/// Revogação (RF-LIC-004): usuário inativo não loga e o /me passa a responder active=false.
/// </summary>
public sealed class DesktopAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _uow;
    private readonly IUserProductAccessRepository _acessos;

    public DesktopAuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IUnitOfWork uow,
        IUserProductAccessRepository acessos)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _uow = uow;
        _acessos = acessos;
    }

    /// <summary>
    /// Registra o uso por produto. O <see cref="User.LoginCount"/> continua
    /// existindo e contando tudo junto; esta linha é o que permite separar
    /// depois quem usou o quê — o plugin do Revit e o Solutions compartilham as
    /// mesmas credenciais e, sem isto, mexem nos mesmos dois campos.
    /// </summary>
    private async Task RegistrarUsoAsync(Guid userId, string? produto, bool ehLogin, CancellationToken ct)
    {
        var slug = UserProductAccess.Normalize(produto);
        var acesso = await _acessos.GetAsync(userId, slug, ct);

        if (acesso is null)
        {
            acesso = new UserProductAccess(userId, slug);
            if (ehLogin) acesso.RegisterLogin();
            await _acessos.AddAsync(acesso, ct);
            return;
        }

        if (ehLogin) acesso.RegisterLogin();
        else acesso.MarkSeen();
    }

    public async Task<Result<DesktopSession>> LoginAsync(
        string? email, string? password, string? produto = null, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(User.Normalize(email ?? ""), ct);
        if (user is null)
        {
            _hasher.BurnDummy(password ?? "");   // mesmo tempo de um login real (anti-enumeração)
            return Result<DesktopSession>.Fail("E-mail ou senha inválidos.", "invalid_credentials");
        }
        if (!_hasher.Verify(password ?? "", user.PasswordHash))
            return Result<DesktopSession>.Fail("E-mail ou senha inválidos.", "invalid_credentials");
        if (!user.IsActive)
            return Result<DesktopSession>.Fail("Acesso revogado.", "user_inactive");

        user.RegisterLogin();
        await RegistrarUsoAsync(user.Id, produto, ehLogin: true, ct);
        await _uow.SaveChangesAsync(ct);

        var (token, expires) = _jwt.CreateDesktopToken(user);
        return Result<DesktopSession>.Ok(new DesktopSession
        {
            Token = token,
            ExpiresAtUtc = expires,
            Email = user.Email,
            Name = NameFromEmail(user.Email)
        });
    }

    /// <summary>Produto que recebe o token renovado. Só o plugin do Revit lê os campos
    /// novos; para os demais o /auth/me continua exatamente como era.</summary>
    public const string ProdutoComRenovacao = "revit-plugin";

    /// <param name="tokenExpiresAtUtc">Vencimento do token que fez a chamada (claim
    /// <c>exp</c>). Faltando mais de um dia para completar a validade cheia, o plugin do
    /// Revit recebe um token novo: o login se renova com o uso e só vence para quem passa
    /// a validade inteira sem abrir o plugin.</param>
    public async Task<DesktopMe?> GetMeAsync(
        Guid userId, string? produto = null, DateTime? tokenExpiresAtUtc = null, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return null;

        // Heartbeat: marca atividade (throttle de 5 min para não escrever a cada chamada).
        // É informativo: se a gravação falhar (banco acordando, colisão de duas
        // validações ao mesmo tempo), a validação segue. Antes virava erro 500 e o
        // plugin pedia a senha de novo com o login válido.
        Exception? falhaHeartbeat = null;
        if (user.LastSeenAtUtc is null || (DateTime.UtcNow - user.LastSeenAtUtc.Value).TotalMinutes >= 5)
        {
            try
            {
                user.MarkSeen();
                await RegistrarUsoAsync(user.Id, produto, ehLogin: false, ct);
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhaHeartbeat = ex;
            }
        }

        string? renovado = null;
        DateTime? renovadoExpira = null;
        if (user.IsActive
            && tokenExpiresAtUtc is not null
            && UserProductAccess.Normalize(produto) == ProdutoComRenovacao
            && tokenExpiresAtUtc.Value - DateTime.UtcNow < _jwt.DesktopSessionLength - TimeSpan.FromDays(1))
        {
            var (token, expira) = _jwt.CreateDesktopToken(user);
            renovado = token;
            renovadoExpira = expira;
        }

        return new DesktopMe
        {
            Email = user.Email,
            Name = NameFromEmail(user.Email),
            Active = user.IsActive,
            RenewedToken = renovado,
            RenewedExpiresAtUtc = renovadoExpira,
            HeartbeatFailure = falhaHeartbeat,
        };
    }

    private static string NameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }
}
