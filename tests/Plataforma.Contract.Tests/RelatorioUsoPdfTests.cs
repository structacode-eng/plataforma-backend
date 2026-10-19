using System.Reflection;
using System.Text;
using Plataforma.Application.Telemetry;
using Plataforma.Infrastructure.Reporting;

namespace Plataforma.Contract.Tests;

/// <summary>
/// O PDF do relatório de uso, gerado direto (sem banco), com os casos que o
/// teste de contrato não alcança porque o banco de teste tem pouco uso.
///
/// <para><b>Por que existem:</b> em 19/10/2026 o painel passou a responder 500
/// ao gerar o PDF ("Não foi possível gerar o PDF"). Estes testes prendem o
/// relatório com uso de verdade: uma pessoa que abriu muitas ferramentas no
/// período e os rótulos com símbolo ("Curva 45° para Baixo", "Alinhar
/// Ramal+").</para>
/// </summary>
public sealed class RelatorioUsoPdfTests
{
    static RelatorioUsoPdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    /// <summary>Todos os slugs que a tabela de nomes conhece.</summary>
    private static IReadOnlyList<string> TodosOsSlugs()
    {
        var campo = typeof(FerramentaNomes).GetField("Mapa", BindingFlags.NonPublic | BindingFlags.Static)!;
        var mapa = (IReadOnlyDictionary<string, string>)campo.GetValue(null)!;
        return mapa.Keys.ToList();
    }

    private static UsoRelatorioDto Relatorio(IReadOnlyList<(string Email, string Comando, int Total)> uso)
    {
        var porPessoa = uso.Select(u => new UsoPessoaDto
        {
            Email = u.Email,
            Comando = u.Comando,
            Total = u.Total,
            UltimoDia = "2026-10-19",
        }).ToList();

        var ferramentas = uso.GroupBy(u => u.Comando).Select(g => new FerramentaDto
        {
            Comando = g.Key,
            Total = g.Sum(x => x.Total),
            Pessoas = g.Select(x => x.Email).Distinct().Count(),
        }).OrderByDescending(f => f.Total).ToList();

        return new UsoRelatorioDto
        {
            De = "2026-09-19",
            Ate = "2026-10-19",
            Dias = 31,
            TotalGeral = uso.Sum(u => u.Total),
            PessoasAtivas = uso.Select(u => u.Email).Distinct().Count(),
            Ferramentas = ferramentas,
            PorPessoa = porPessoa,
        };
    }

    private static void AssertPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Pessoa_que_usou_todas_as_ferramentas_gera_o_pdf()
    {
        // Uma pessoa com uma linha por ferramenta da toolbar inteira: a tabela
        // dela não cabe numa página só.
        var slugs = TodosOsSlugs();
        var uso = slugs.Select((s, i) => ("pesada@escritorio.com", s, i + 1)).ToList();
        uso.Add(("leve@escritorio.com", "curva_para_baixo45", 3));

        AssertPdf(new UsageReportPdf().Gerar(Relatorio(uso)));
    }

    [Fact]
    public void Pessoa_com_mais_linhas_que_tres_paginas_gera_o_pdf()
    {
        // Slugs que a tabela não conhece (comando removido do plugin) também
        // entram no relatório; 150 linhas passam de várias páginas.
        var uso = Enumerable.Range(1, 150)
            .Select(i => ("muitas@escritorio.com", "comando_antigo_" + i, i))
            .ToList();

        AssertPdf(new UsageReportPdf().Gerar(Relatorio(uso)));
    }

    [Fact]
    public void Varias_pessoas_com_os_rotulos_com_simbolo_geram_o_pdf()
    {
        var slugs = new[] { "curva_para_baixo45", "alinhar_ramal_mais", "alinhar_em3d", "brotar_trechos", "pro_sheets" };
        var uso = Enumerable.Range(1, 25)
            .SelectMany(p => slugs.Select((s, i) => ($"pessoa{p}@escritorio.com", s, p + i)))
            .ToList();

        AssertPdf(new UsageReportPdf().Gerar(Relatorio(uso)));
    }
}
