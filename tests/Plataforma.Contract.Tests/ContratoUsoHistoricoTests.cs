using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Plataforma.Application.Telemetry;

namespace Plataforma.Contract.Tests;

/// <summary>
/// Contrato do histórico de uso — <c>GET /v1/admin/usage</c> por intervalo e
/// <c>GET /v1/admin/usage/pdf</c>.
///
/// <para><b>Por que estes testes existem:</b> o registro de uso nunca foi
/// apagado do banco, mas até aqui só dava para pedir "últimos N dias contados
/// de hoje". Virado o mês, o mês anterior ficava inalcançável pelo painel e
/// parecia perdido. O que estes testes fixam é a porta que passou a existir:
/// pedir um intervalo de datas e receber exatamente aquele intervalo.</para>
///
/// <para>O modo antigo (<c>dias</c>) continua coberto porque o painel e o
/// endpoint são usados em produção agora — quebrá-lo seria trocar um problema
/// por outro.</para>
/// </summary>
public sealed class ContratoUsoHistoricoTests
{
    private const string Ferramenta = "cotas_furacao";

    private static async Task<JsonElement> CorpoAsync(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Cliente já autenticado como Owner.</summary>
    private static async Task<HttpClient> OwnerAsync(ApiDeTeste api)
    {
        var c = api.CreateClient();
        var r = await c.PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        var token = (await CorpoAsync(r)).GetProperty("access_token").GetString()!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>Grava aberturas num dia específico, pelo mesmo caminho do plugin.</summary>
    private static async Task SemearAsync(HttpClient owner, DateOnly dia, int quantidade)
    {
        var r = await owner.PostAsJsonAsync("/v1/telemetry", new
        {
            eventos = new[] { new { comando = Ferramenta, quantidade, dia = Iso(dia) } },
        });
        r.EnsureSuccessStatusCode();
        Assert.Equal(1, (await CorpoAsync(r)).GetProperty("aceitos").GetInt32());
    }

    // ── intervalo ───────────────────────────────────────────────────────

    [Fact]
    public async Task Intervalo_traz_o_que_esta_dentro_e_deixa_de_fora_o_resto()
    {
        // O caso que motivou tudo: consultar um período fechado, no passado,
        // sem que o uso recente vaze para dentro dele.
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var antigo = Hoje.AddDays(-40);
        await SemearAsync(owner, antigo, 7);
        await SemearAsync(owner, Hoje.AddDays(-2), 99);

        var de = antigo.AddDays(-3);
        var ate = antigo.AddDays(3);
        var j = await CorpoAsync(await owner.GetAsync($"/v1/admin/usage?de={Iso(de)}&ate={Iso(ate)}"));

        Assert.Equal(Iso(de), j.GetProperty("de").GetString());
        Assert.Equal(Iso(ate), j.GetProperty("ate").GetString());
        Assert.Equal(7, j.GetProperty("totalGeral").GetInt32());   // o 99 ficou fora
        Assert.Equal(7, j.GetProperty("dias").GetInt32());          // inclusivo nas duas pontas
    }

    [Fact]
    public async Task Intervalo_detalha_por_pessoa_e_por_ferramenta()
    {
        // O PDF e a tabela do painel se apoiam nestes dois blocos; se um deles
        // vier vazio num período com uso, o relatório sai em branco.
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var dia = Hoje.AddDays(-10);
        await SemearAsync(owner, dia, 4);

        var j = await CorpoAsync(await owner.GetAsync($"/v1/admin/usage?de={Iso(dia)}&ate={Iso(dia)}"));

        var ferramenta = Assert.Single(j.GetProperty("ferramentas").EnumerateArray());
        Assert.Equal(Ferramenta, ferramenta.GetProperty("comando").GetString());
        Assert.Equal(4, ferramenta.GetProperty("total").GetInt32());

        var pessoa = Assert.Single(j.GetProperty("porPessoa").EnumerateArray());
        Assert.Equal(ApiDeTeste.OwnerEmail, pessoa.GetProperty("email").GetString());
        Assert.Equal(1, j.GetProperty("pessoasAtivas").GetInt32());
    }

    [Fact]
    public async Task Datas_trocadas_sao_corrigidas_em_vez_de_recusadas()
    {
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var de = Hoje.AddDays(-5);
        var j = await CorpoAsync(
            await owner.GetAsync($"/v1/admin/usage?de={Iso(Hoje)}&ate={Iso(de)}"));

        Assert.Equal(Iso(de), j.GetProperty("de").GetString());
        Assert.Equal(Iso(Hoje), j.GetProperty("ate").GetString());
    }

    [Fact]
    public async Task Fim_no_futuro_e_cortado_em_hoje()
    {
        // Escolher "este mês" no painel manda um fim que ainda não chegou.
        // Recusar seria hostil; o servidor corta e diz o que usou.
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var j = await CorpoAsync(await owner.GetAsync(
            $"/v1/admin/usage?de={Iso(Hoje.AddDays(-3))}&ate={Iso(Hoje.AddDays(60))}"));

        Assert.Equal(Iso(Hoje), j.GetProperty("ate").GetString());
    }

    [Fact]
    public async Task Antes_de_12_meses_e_recusado_com_400()
    {
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var r = await owner.GetAsync(
            $"/v1/admin/usage?de={Iso(Hoje.AddMonths(-13))}&ate={Iso(Hoje)}");

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("periodo_invalido", (await CorpoAsync(r)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sem_intervalo_a_janela_de_dias_continua_valendo()
    {
        // O painel em produção ainda chama assim. Este teste é o que impede a
        // mudança de quebrar a tela que já funcionava.
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var j = await CorpoAsync(await owner.GetAsync("/v1/admin/usage?dias=30"));

        Assert.Equal(30, j.GetProperty("dias").GetInt32());
        Assert.Equal(Iso(Hoje), j.GetProperty("ate").GetString());
        Assert.Equal(Iso(Hoje.AddDays(-29)), j.GetProperty("de").GetString());
    }

    // ── PDF ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pdf_vem_como_arquivo_pdf_de_verdade()
    {
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var dia = Hoje.AddDays(-7);
        await SemearAsync(owner, dia, 3);

        var r = await owner.GetAsync($"/v1/admin/usage/pdf?de={Iso(dia)}&ate={Iso(dia)}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/pdf", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", r.Content.Headers.ContentDisposition?.DispositionType);

        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1000, $"PDF suspeito de vazio: {bytes.Length} bytes");
    }

    [Fact]
    public async Task Pdf_de_periodo_sem_uso_ainda_e_um_pdf()
    {
        // Página em branco deixaria a dúvida entre "ninguém usou" e "quebrou".
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        var r = await owner.GetAsync(
            $"/v1/admin/usage/pdf?de={Iso(Hoje.AddDays(-3))}&ate={Iso(Hoje)}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    // ── autorização ─────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_token_nenhum_dos_dois_responde()
    {
        using var api = new ApiDeTeste();
        var anonimo = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/v1/admin/usage?dias=30")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/v1/admin/usage/pdf?dias=30")).StatusCode);
    }

    [Fact]
    public async Task Conta_comum_nao_ve_o_uso_nominal_de_ninguem()
    {
        // O relatório diz quem abriu o quê. Continua restrito a Owner, também
        // no caminho novo do PDF.
        using var api = new ApiDeTeste();
        var owner = await OwnerAsync(api);

        const string email = "comum.contrato@teste.local";
        const string senha = "SenhaComum123456";
        var criada = await owner.PostAsJsonAsync("/v1/admin/users", new { email, password = senha });
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);

        var c = api.CreateClient();
        var login = await c.PostAsJsonAsync("/auth/login", new { email, password = senha });
        login.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await CorpoAsync(login)).GetProperty("access_token").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await c.GetAsync("/v1/admin/usage?dias=30")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await c.GetAsync("/v1/admin/usage/pdf?dias=30")).StatusCode);
    }

    // ── validação que não dá para alcançar por HTTP ──────────────────────

    [Fact]
    public void Periodo_que_comeca_depois_de_hoje_e_recusado()
    {
        var r = UsageQueryService.ResolverPeriodo(Hoje.AddDays(5), Hoje.AddDays(9), 0);

        Assert.False(r.Success);
        Assert.Equal("periodo_invalido", r.Code);
    }
}
