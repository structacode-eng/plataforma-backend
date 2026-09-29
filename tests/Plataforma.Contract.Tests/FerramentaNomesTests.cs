using Plataforma.Application.Telemetry;

namespace Plataforma.Contract.Tests;

/// <summary>
/// Os rótulos das ferramentas no relatório.
///
/// <para><b>Por que existem:</b> a telemetria carrega o slug derivado do nome da
/// classe do comando no plugin, que não é o que está escrito no botão do Revit.
/// O relatório mostrava "Pro Sheets" para o botão "Exportar Arquivo" e "About"
/// para o botão "Filippon" — quem lê o relatório não reconhecia a ferramenta.
/// </para>
///
/// <para>A tabela é uma cópia de <c>RibbonBuilder.cs</c> do plugin e, como toda
/// cópia, envelhece. Estes testes prendem os casos que mais confundiam e a
/// regra que impede a tabela de voltar a ficar ambígua.</para>
/// </summary>
public sealed class FerramentaNomesTests
{
    [Theory]
    // Os oito que apareciam errados no painel, com o nome real do botão.
    [InlineData("selecoes", "Seleções")]
    [InlineData("pro_sheets", "Exportar Arquivo")]
    [InlineData("about", "Filippon")]
    [InlineData("ajustar_elevacao", "Ajustar Elevação")]
    [InlineData("gerar_vistas", "Gerar Vistas")]
    [InlineData("desvio_calha_eletroduto", "Desvio Calha e Eletroduto")]
    // Acentuação e caixa que o title-case automático errava.
    [InlineData("rotear_conduites", "Rotear Conduítes")]
    [InlineData("auditar_bim", "Audita BIM")]
    [InlineData("cotas_elt", "Cotas ELT")]
    [InlineData("magic_section", "Corte Structa")]
    [InlineData("family_browser", "Buscador de Famílias")]
    public void Slug_conhecido_usa_o_nome_do_botao(string slug, string esperado)
        => Assert.Equal(esperado, FerramentaNomes.Exibir(slug));

    [Theory]
    // Mesmo rótulo na ribbon em disciplinas diferentes: sem a disciplina, o
    // relatório traria duas linhas iguais com números diferentes.
    [InlineData("custo_sinapi", "Custo SINAPI (Hidrossanitário)")]
    [InlineData("custo_sinapi_elt", "Custo SINAPI (Elétrica)")]
    [InlineData("mover_conectar_alinhar", "Mover, Alinhar e Conectar (Hidrossanitário)")]
    [InlineData("mover_conectar_alinhar_elt", "Mover, Alinhar e Conectar (Elétrica)")]
    public void Rotulo_repetido_ganha_a_disciplina(string slug, string esperado)
        => Assert.Equal(esperado, FerramentaNomes.Exibir(slug));

    [Fact]
    public void Nenhum_rotulo_se_repete()
    {
        // Guarda da regra acima: se alguém acrescentar um botão cujo nome já
        // existe e esquecer a disciplina, o relatório fica ambíguo de novo.
        var slugs = new[]
        {
            "selecoes", "pro_sheets", "about", "ajustar_elevacao", "gerar_vistas",
            "desvio_calha_eletroduto", "custo_sinapi", "custo_sinapi_elt",
            "mover_conectar_alinhar", "mover_conectar_alinhar_elt",
            "copiar_parametro", "copiar_parametro_eletrico",
            "quantitativo_eletrico", "quantitativo_hidraulico",
        };

        var nomes = slugs.Select(FerramentaNomes.Exibir).ToList();
        Assert.Equal(nomes.Count, nomes.Distinct().Count());
    }

    [Fact]
    public void Slug_desconhecido_nao_some_do_relatorio()
    {
        // Comando removido do plugin, ou mais novo que esta tabela: some do
        // mapa mas os números dele continuam no banco. Mostrar legível é
        // melhor que mostrar vazio.
        Assert.Equal("Comando Que Nao Existe", FerramentaNomes.Exibir("comando_que_nao_existe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Slug_vazio_vira_travessao(string? slug)
        => Assert.Equal("—", FerramentaNomes.Exibir(slug));

    [Fact]
    public void A_tabela_cobre_a_toolbar_inteira()
    {
        // A toolbar tinha 53 botões quando a tabela foi extraída. Se o plugin
        // ganhar botões e ninguém atualizar aqui, eles caem no fallback e
        // voltam a aparecer com o nome errado — este número é o lembrete.
        Assert.Equal(53, FerramentaNomes.Total);
    }
}
