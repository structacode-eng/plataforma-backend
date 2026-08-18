using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Plataforma.Contract.Tests;

/// <summary>
/// Contrato dos endpoints que o PLUGIN DO REVIT JÁ INSTALADO consome.
///
/// <para>Os nomes de campo verificados aqui estão compilados dentro de uma DLL
/// que roda nas máquinas do escritório. Não há como corrigi-los remotamente:
/// renomear qualquer um derruba o login de todo mundo, e o código continua
/// compilando normalmente. Este arquivo é o que faz o build acusar.</para>
///
/// <para>Extraídos de AuthService.cs e UpdateService.cs do plugin:
/// access_token, token_type, expires_at, user.email, user.name, active,
/// version/url/sha256/notes/mandatory.</para>
///
/// <para>Regra ao mexer aqui: acrescentar campo pode; renomear ou remover, não.</para>
/// </summary>
public sealed class ContratoDesktopTests : IClassFixture<ApiDeTeste>
{
    private readonly ApiDeTeste _api;
    public ContratoDesktopTests(ApiDeTeste api) => _api = api;

    private HttpClient Cliente() => _api.CreateClient();

    private static async Task<JsonElement> CorpoAsync(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Login do Owner semeado, usado pelos testes que exigem token.</summary>
    private async Task<(HttpClient cliente, string token)> LogadoAsync()
    {
        var c = Cliente();
        var r = await c.PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        r.StatusCode.Should_Be(HttpStatusCode.OK);
        var token = (await CorpoAsync(r)).GetProperty("access_token").GetString()!;
        return (c, token);
    }

    // ── /auth/login ─────────────────────────────────────────────────────

    [Fact]
    public async Task Login_valido_devolve_o_formato_que_o_plugin_espera()
    {
        var (_, token) = await LogadoAsync();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var c = Cliente();
        var r = await c.PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        var j = await CorpoAsync(r);

        // Nomes lidos pelo AuthService.cs do plugin - não renomear.
        Assert.False(string.IsNullOrWhiteSpace(j.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", j.GetProperty("token_type").GetString());

        // expires_at precisa ser ISO 8601 parseável: o plugin faz DateTime.Parse.
        var expira = j.GetProperty("expires_at").GetString();
        Assert.True(DateTime.TryParse(expira, out _), $"expires_at não parseável: {expira}");

        var user = j.GetProperty("user");
        Assert.Equal(ApiDeTeste.OwnerEmail, user.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(user.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_401()
    {
        var r = await Cliente().PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = "senhaErrada999" });

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Login_de_email_inexistente_responde_igual_a_senha_errada()
    {
        // Anti-enumeração: a resposta não pode revelar quais contas existem.
        var inexistente = await Cliente().PostAsJsonAsync("/auth/login",
            new { email = "nao.existe@lugar.nenhum", password = "qualquerCoisa1" });
        var senhaErrada = await Cliente().PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = "senhaErrada999" });

        Assert.Equal(senhaErrada.StatusCode, inexistente.StatusCode);
    }

    // ── /auth/me ────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_com_token_valido_devolve_email_name_active()
    {
        var (c, token) = await LogadoAsync();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var r = await c.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var j = await CorpoAsync(r);
        Assert.Equal(ApiDeTeste.OwnerEmail, j.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(j.GetProperty("name").GetString()));
        // `active` é o que faz a revogação chegar ao cliente - precisa ser booleano.
        Assert.Equal(JsonValueKind.True, j.GetProperty("active").ValueKind);
    }

    [Fact]
    public async Task Me_sem_token_devolve_401()
    {
        var r = await Cliente().GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Me_com_token_adulterado_devolve_401()
    {
        var (c, token) = await LogadoAsync();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        var r = await c.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // ── /health ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_responde_ok()
    {
        var r = await Cliente().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("ok", (await CorpoAsync(r)).GetProperty("status").GetString());
    }
}

/// <summary>Açúcar mínimo para asserção de status, sem depender de biblioteca externa.</summary>
internal static class AssercaoHttp
{
    public static void Should_Be(this HttpStatusCode atual, HttpStatusCode esperado)
        => Assert.Equal(esperado, atual);
}
