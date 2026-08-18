using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Plataforma.Contract.Tests;

/// <summary>
/// Contrato do <c>GET /version</c> - o manifesto de auto-update.
///
/// <para>É o endpoint mais delicado da plataforma. Ele é público (sem auth), é
/// consultado no boot por toda máquina do escritório, e a resposta dele decide
/// qual instalador cada uma vai baixar e executar. Um erro aqui não dá tela de
/// erro: instala o programa errado.</para>
///
/// <para><b>Por que estes testes existem agora:</b> o manifesto hoje é único por
/// canal, sem noção de produto. Quando o Filippon Solutions passar a publicar
/// versões, será preciso acrescentar essa dimensão - e o risco é o plugin do
/// Revit começar a receber o manifesto do Solutions. Estes testes fixam o
/// comportamento atual para que a mudança seja verificável em vez de
/// esperançosa.</para>
/// </summary>
public sealed class ContratoVersaoTests : IClassFixture<ApiDeTeste>
{
    private readonly ApiDeTeste _api;
    public ContratoVersaoTests(ApiDeTeste api) => _api = api;

    private static async Task<JsonElement> CorpoAsync(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Version_e_publico_nao_exige_autenticacao()
    {
        // O plugin consulta no boot, ANTES de o usuário logar. Exigir token aqui
        // deixaria toda a frota sem conseguir se atualizar.
        var r = await _api.CreateClient().GetAsync("/version");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Version_devolve_os_cinco_campos_que_o_plugin_parseia()
    {
        var j = await CorpoAsync(await _api.CreateClient().GetAsync("/version"));

        // Nomes lidos pelo UpdateService.cs do plugin instalado - não renomear.
        Assert.Equal(JsonValueKind.String, j.GetProperty("latest").ValueKind);
        Assert.Equal(JsonValueKind.String, j.GetProperty("url").ValueKind);
        Assert.Equal(JsonValueKind.String, j.GetProperty("notes").ValueKind);
        Assert.Equal(JsonValueKind.String, j.GetProperty("sha256").ValueKind);

        var mandatory = j.GetProperty("mandatory").ValueKind;
        Assert.True(mandatory is JsonValueKind.True or JsonValueKind.False,
            $"mandatory precisa ser booleano, veio {mandatory}");
    }

    [Fact]
    public async Task Version_sem_release_publicada_devolve_0_0_0_e_nao_erro()
    {
        // Banco vazio é o estado de uma instalação nova. O plugin compara
        // versões: "0.0.0" nunca é maior que a instalada, então ele não tenta
        // baixar nada. Um 404 ou 500 aqui viraria erro visível no boot.
        //
        // Fábrica própria: os testes de isolamento publicam releases, e o banco
        // em memória é compartilhado pela classe. Sem instância separada, este
        // teste passaria ou falharia conforme a ordem de execução.
        using var vazia = new ApiDeTeste();
        var j = await CorpoAsync(await vazia.CreateClient().GetAsync("/version"));

        Assert.Equal("0.0.0", j.GetProperty("latest").GetString());
        Assert.Equal("", j.GetProperty("url").GetString());
    }

    [Theory]
    [InlineData("stable")]
    [InlineData("canary")]
    [InlineData("")]
    [InlineData("canal-que-nao-existe")]
    public async Task Version_aceita_qualquer_canal_sem_quebrar(string canal)
    {
        // Canal desconhecido não pode virar erro: a frota tem binários antigos
        // que podem mandar valores que nem existem mais.
        var r = await _api.CreateClient().GetAsync($"/version?channel={canal}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(JsonValueKind.String,
            (await CorpoAsync(r)).GetProperty("latest").ValueKind);
    }

    [Fact]
    public async Task Version_com_produto_desconhecido_cai_no_plugin()
    {
        // Binário antigo, cabeçalho digitado errado, cliente de terceiro: nada
        // disso pode virar erro nem vazar o manifesto de outro produto. Cai no
        // plugin, que é o default histórico.
        var c = _api.CreateClient();
        c.DefaultRequestHeaders.Add("X-Filippon-Product", "produto-inventado");

        var r = await c.GetAsync("/version");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(JsonValueKind.String, (await CorpoAsync(r)).GetProperty("latest").ValueKind);
    }

    /// <summary>
    /// O teste que justifica a coluna Product existir.
    ///
    /// <para>Antes dela o manifesto era único por canal: publicar o Solutions
    /// sobrescrevia o do plugin, e a frota do Revit baixaria o instalador do
    /// Solutions - com o SHA-256 conferindo, porque o arquivo não está
    /// corrompido, só é de outro produto. Nada acusaria.</para>
    /// </summary>
    [Fact]
    public async Task Publicar_no_Solutions_nao_toca_no_manifesto_do_plugin()
    {
        var (adm, _) = await ComoOwnerAsync();

        // 1. Publica uma versão para CADA produto, no estável.
        await PublicarAsync(adm, "revit-plugin", "stable", "2.9.9", "https://exemplo/plugin.exe");
        await PublicarAsync(adm, "solutions", "stable", "3.17.0", "https://exemplo/solutions.exe");

        // 2. O plugin em campo (SEM cabeçalho) recebe o dele.
        var semHeader = await CorpoAsync(await _api.CreateClient().GetAsync("/version?channel=stable"));
        Assert.Equal("2.9.9", semHeader.GetProperty("latest").GetString());
        Assert.Contains("plugin.exe", semHeader.GetProperty("url").GetString());

        // 3. O Solutions (COM cabeçalho) recebe o dele.
        var cs = _api.CreateClient();
        cs.DefaultRequestHeaders.Add("X-Filippon-Product", "solutions");
        var comHeader = await CorpoAsync(await cs.GetAsync("/version?channel=stable"));
        Assert.Equal("3.17.0", comHeader.GetProperty("latest").GetString());
        Assert.Contains("solutions.exe", comHeader.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Promover_o_Solutions_nao_promove_o_plugin()
    {
        var (adm, _) = await ComoOwnerAsync();

        await PublicarAsync(adm, "revit-plugin", "stable", "1.0.0", "https://exemplo/p-estavel.exe");
        await PublicarAsync(adm, "solutions", "stable", "1.0.0", "https://exemplo/s-estavel.exe");
        await PublicarAsync(adm, "solutions", "canary", "2.0.0", "https://exemplo/s-canario.exe");

        var r = await adm.PostAsync("/v1/admin/release/promote?product=solutions", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        // O plugin continua no 1.0.0 - a promoção não atravessou produtos.
        var plugin = await CorpoAsync(await _api.CreateClient().GetAsync("/version?channel=stable"));
        Assert.Equal("1.0.0", plugin.GetProperty("latest").GetString());

        var cs = _api.CreateClient();
        cs.DefaultRequestHeaders.Add("X-Filippon-Product", "solutions");
        var solutions = await CorpoAsync(await cs.GetAsync("/version?channel=stable"));
        Assert.Equal("2.0.0", solutions.GetProperty("latest").GetString());
    }

    // ── apoio ───────────────────────────────────────────────────────────

    private async Task<(HttpClient cliente, string token)> ComoOwnerAsync()
    {
        var c = _api.CreateClient();
        var r = await c.PostAsJsonAsync("/auth/login",
            new { email = ApiDeTeste.OwnerEmail, password = ApiDeTeste.OwnerSenha });
        var token = (await CorpoAsync(r)).GetProperty("access_token").GetString()!;
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (c, token);
    }

    private static async Task PublicarAsync(HttpClient adm, string produto, string canal, string versao, string url)
    {
        var r = await adm.PutAsJsonAsync(
            $"/v1/admin/release?product={produto}&channel={canal}",
            new { version = versao, url, notes = "teste", sha256 = (string?)null, mandatory = false });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }
}
