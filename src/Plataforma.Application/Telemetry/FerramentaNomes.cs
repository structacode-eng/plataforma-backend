using System.Globalization;

namespace Plataforma.Application.Telemetry;

/// <summary>
/// Nome de exibição de cada ferramenta, igual ao rótulo do botão na toolbar do
/// Revit.
///
/// <para>A telemetria carrega o slug derivado do nome da classe do comando
/// (<c>AjustarElevacaoCommand</c> → <c>ajustar_elevacao</c>), que não bate com o
/// que a pessoa vê no Revit: o relatório mostrava "Pro Sheets" para o botão
/// "Exportar Arquivo" e "About" para o botão "Filippon".</para>
///
/// <para><b>Origem:</b> extraído de <c>src/Filippon/UI/Ribbon/RibbonBuilder.cs</c>
/// no repositório do plugin — os <c>Btn</c> ali carregam o texto do botão e a
/// classe do comando. Ao acrescentar ou renomear botão no plugin, atualize esta
/// tabela e a gêmea em <c>wwwroot/admin/index.html</c> (função
/// <c>nomeFerramenta</c>).</para>
///
/// <para>Onde dois comandos distintos dividem o mesmo rótulo na ribbon (Custo
/// SINAPI existe em Hidrossanitário e em Elétrica), a disciplina entra entre
/// parênteses: sem isso o relatório traria duas linhas com o mesmo nome e
/// números diferentes.</para>
/// </summary>
public static class FerramentaNomes
{
    private static readonly IReadOnlyDictionary<string, string> Mapa = new Dictionary<string, string>
    {
        ["about"]                      = "Filippon",
        ["ajustar_elevacao"]           = "Ajustar Elevação",
        ["ajuste_altura"]              = "Ajustar Altura",
        ["alinhar_grelha_nivel"]       = "Caixas S. Ralos",
        ["alinhar_ramal3d"]            = "Alinhar Ramal",
        ["auditar_bim"]                = "Audita BIM",
        ["auto_ramal"]                 = "Auto Ramal",
        ["buscar_comando"]             = "Buscar Comando",
        ["colorir_abas"]               = "Colorir Abas",
        ["colorir_eletroduto"]         = "Colorir Eletroduto",
        ["colorir_filtros"]            = "Colorir Filtros",
        ["comprimento_trecho"]         = "C. Trecho P. Carga",
        ["config_corte"]               = "Config. Corte",
        ["copiar_parametro"]           = "Rotina Pav. Elemento (Hidrossanitário)",
        ["copiar_parametro_eletrico"]  = "Rotina Pav. Elemento (Elétrica)",
        ["cotas_elt"]                  = "Cotas ELT",
        ["criar_solicitacao"]          = "Criar Solicitação",
        ["custo_sinapi"]               = "Custo SINAPI (Hidrossanitário)",
        ["custo_sinapi_elt"]           = "Custo SINAPI (Elétrica)",
        ["desconectar"]                = "Desconectar Elementos",
        ["desvio_calha_eletroduto"]    = "Desvio Calha e Eletroduto",
        ["desvio_hidro"]               = "Desvio Hidráulico",
        ["dimensionamento_cge"]        = "Dimensionar CGE",
        ["excluir_system"]             = "Excluir Sistema",
        ["explodir_vista"]             = "Explodir Vista",
        ["exportar_tabelas"]           = "Exportar Tabelas",
        ["family_browser"]             = "Buscador de Famílias",
        ["flex_pipe"]                  = "Flex Pipe por Line",
        ["gerar_vistas"]               = "Gerar Vistas",
        ["gestor_de_clashes"]          = "Gestor de Clashes",
        ["girar_encaixe"]              = "Girar Encaixe",
        ["identificar_id"]             = "Identificar ID",
        ["inverter_luvas"]             = "Inverter Luvas",
        ["isometricos_ampliacoes"]     = "Criar Isométricos",
        ["luvas_por_nivel"]            = "Luvas por Nivel",
        ["magic_section"]              = "Corte Structa",
        ["mover_conectar_alinhar"]     = "Mover, Alinhar e Conectar (Hidrossanitário)",
        ["mover_conectar_alinhar_elt"] = "Mover, Alinhar e Conectar (Elétrica)",
        ["mudar_nivel"]                = "Mudar Nível",
        ["open_docs"]                  = "Docs",
        ["open_instagram"]             = "Instagram",
        ["open_site"]                  = "Site",
        ["pro_sheets"]                 = "Exportar Arquivo",
        ["quantitativo_eletrico"]      = "Rotina Quantitativo (Elétrica)",
        ["quantitativo_geral"]         = "Gerar Quantitativo",
        ["quantitativo_hidraulico"]    = "Rotina Quantitativo (Hidrossanitário)",
        ["rotear_conduites"]           = "Rotear Conduítes",
        ["rotina_unifilar"]            = "Rotina Unifilar",
        ["selecoes"]                   = "Seleções",
        ["simbolos_tomada"]            = "Símbolos de Tomadas",
        ["tutoriais"]                  = "Tutoriais",
        ["workset"]                    = "Rotina Workset",
        ["zerar_slope"]                = "Zerar Slope",
    };

    /// <summary>
    /// Rótulo da ferramenta. Slug desconhecido — comando removido do plugin ou
    /// mais novo que esta tabela — cai no formato antigo, legível, em vez de
    /// sumir do relatório.
    /// </summary>
    public static string Exibir(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "—";
        if (Mapa.TryGetValue(slug, out var nome)) return nome;
        return CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(slug.Replace('_', ' '));
    }

    /// <summary>Quantos rótulos a tabela conhece. Usado nos testes.</summary>
    public static int Total => Mapa.Count;

    /// <summary>Consulta crua, sem o fallback. Usado nos testes.</summary>
    public static bool Conhece(string slug) => Mapa.ContainsKey(slug);
}
