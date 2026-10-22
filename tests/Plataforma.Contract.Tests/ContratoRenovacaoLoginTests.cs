using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;
using Plataforma.Infrastructure.Persistence;
using Plataforma.Infrastructure.Persistence.Repositories;

namespace Plataforma.Contract.Tests;

/// <summary>
/// Renovação do login do plugin (21/10/2026). O login passou a valer 30 dias e o
/// /auth/me entrega um token novo ao plugin do Revit quando o atual já não está com a
/// validade cheia; assim o login só vence para quem passa 30 dias sem usar.
///
/// <para>O que NÃO pode mudar (plugin 2.7.1 instalado e o Solutions): o /auth/me de
/// um token recente, e o de qualquer chamada que não venha do plugin do Revit,
/// continua só com email/name/active.</para>
/// </summary>
public sealed class ContratoRenovacaoLoginTests : IClassFixture<ApiDeTeste>
{
    private const string Produto = "X-Filippon-Product";
    private readonly ApiDeTeste _api;
    public ContratoRenovacaoLoginTests(ApiDeTeste api) => _api = api;

    private static async Task<JsonElement> CorpoAsync(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<string> LoginAsync()
    {
        var r = await _api.CreateClient().PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await CorpoAsync(r)).GetProperty("access_token").GetString()!;
    }

    /// <summary>Token do mesmo usuário, assinado com a chave da API, que vence em
    /// <paramref name="diasRestantes"/> dias: simula um login feito há tempos.</summary>
    private static string TokenQueVenceEm(string tokenDoLogin, double diasRestantes)
    {
        var sub = new JwtSecurityTokenHandler().ReadJwtToken(tokenDoLogin).Subject;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiDeTeste.ChaveJwt));
        var token = new JwtSecurityToken(
            issuer: ApiDeTeste.EmissorJwt,
            audience: ApiDeTeste.PublicoJwt,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, sub), new Claim(JwtRegisteredClaimNames.Email, ApiDeTeste.OwnerEmail) },
            expires: DateTime.UtcNow.AddDays(diasRestantes),
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpResponseMessage> MeAsync(string token, string? produto)
    {
        var c = _api.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (produto is not null) c.DefaultRequestHeaders.Add(Produto, produto);
        return await c.GetAsync("/auth/me");
    }

    [Fact]
    public async Task Login_vale_30_dias()
    {
        var r = await _api.CreateClient().PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        var expira = DateTime.Parse((await CorpoAsync(r)).GetProperty("expires_at").GetString()!).ToUniversalTime();

        var dias = (expira - DateTime.UtcNow).TotalDays;
        Assert.InRange(dias, 29.9, 30.1);
    }

    [Fact]
    public async Task Me_com_login_recente_nao_renova()
    {
        var r = await MeAsync(await LoginAsync(), "revit-plugin");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var j = await CorpoAsync(r);
        Assert.False(j.TryGetProperty("access_token", out _));
        Assert.False(j.TryGetProperty("expires_at", out _));
    }

    [Fact]
    public async Task Me_com_login_antigo_entrega_token_novo_ao_plugin()
    {
        var antigo = TokenQueVenceEm(await LoginAsync(), diasRestantes: 10);

        var r = await MeAsync(antigo, "revit-plugin");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await CorpoAsync(r);

        // Os campos de sempre continuam lá.
        Assert.Equal(ApiDeTeste.OwnerEmail, j.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.True, j.GetProperty("active").ValueKind);

        // E vem o token novo, com os mesmos nomes do /auth/login.
        var novo = j.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(novo));
        Assert.Equal("Bearer", j.GetProperty("token_type").GetString());
        var expira = DateTime.Parse(j.GetProperty("expires_at").GetString()!).ToUniversalTime();
        Assert.InRange((expira - DateTime.UtcNow).TotalDays, 29.9, 30.1);

        // O token novo vale como qualquer token de login.
        var r2 = await MeAsync(novo!, "revit-plugin");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("solutions")]
    public async Task Me_fora_do_plugin_do_revit_continua_como_era(string? produto)
    {
        var antigo = TokenQueVenceEm(await LoginAsync(), diasRestantes: 10);

        var r = await MeAsync(antigo, produto);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var j = await CorpoAsync(r);
        Assert.False(j.TryGetProperty("access_token", out _));
        Assert.Equal(ApiDeTeste.OwnerEmail, j.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_com_token_vencido_continua_401()
    {
        var vencido = TokenQueVenceEm(await LoginAsync(), diasRestantes: -1);

        var r = await MeAsync(vencido, "revit-plugin");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}

/// <summary>API de teste em que a gravação do sinal de vida do /auth/me falha quando
/// <see cref="Falhar"/> está ligado (como o banco acordando ou duas validações
/// colidindo).</summary>
public sealed class ApiComFalhaNoSinalDeVida : ApiDeTeste
{
    public volatile bool Falhar;

    protected override void ServicosExtras(IServiceCollection services)
    {
        services.RemoveAll<IUserProductAccessRepository>();
        services.AddScoped<IUserProductAccessRepository>(sp =>
            new AcessosQueFalham(new UserProductAccessRepository(sp.GetRequiredService<AppDbContext>()), () => Falhar));
    }

    private sealed class AcessosQueFalham : IUserProductAccessRepository
    {
        private readonly IUserProductAccessRepository _real;
        private readonly Func<bool> _falhar;

        public AcessosQueFalham(IUserProductAccessRepository real, Func<bool> falhar)
        {
            _real = real;
            _falhar = falhar;
        }

        public Task<UserProductAccess?> GetAsync(Guid userId, string product, CancellationToken ct = default)
            => _falhar() ? throw new InvalidOperationException("falha simulada no banco") : _real.GetAsync(userId, product, ct);

        public Task<IReadOnlyList<UserProductAccess>> ListByUserAsync(Guid userId, CancellationToken ct = default)
            => _real.ListByUserAsync(userId, ct);

        public Task<IReadOnlyList<UserProductAccess>> ListByUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
            => _real.ListByUsersAsync(userIds, ct);

        public Task AddAsync(UserProductAccess acesso, CancellationToken ct = default)
            => _real.AddAsync(acesso, ct);
    }
}

public sealed class SinalDeVidaTests : IClassFixture<ApiComFalhaNoSinalDeVida>
{
    private readonly ApiComFalhaNoSinalDeVida _api;
    public SinalDeVidaTests(ApiComFalhaNoSinalDeVida api) => _api = api;

    [Fact]
    public async Task Falha_ao_gravar_o_sinal_de_vida_nao_derruba_a_validacao()
    {
        var c = _api.CreateClient();
        var login = await c.PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("access_token").GetString()!;

        // Último sinal de vida há 10 min: o /auth/me vai tentar gravar um novo.
        using (var escopo = _api.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = db.Users.Single(u => u.Email == ApiDeTeste.OwnerEmail);
            db.Entry(user).Property(u => u.LastSeenAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-10);
            db.SaveChanges();
        }

        _api.Falhar = true;
        try
        {
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            c.DefaultRequestHeaders.Add("X-Filippon-Product", "revit-plugin");
            var r = await c.GetAsync("/auth/me");

            // Antes: 500, e o plugin pedia a senha com o login válido.
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(JsonValueKind.True, j.GetProperty("active").ValueKind);
        }
        finally
        {
            _api.Falhar = false;
        }
    }
}
