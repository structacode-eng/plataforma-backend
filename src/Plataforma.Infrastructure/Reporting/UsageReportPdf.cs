using System.Globalization;
using Plataforma.Application.Abstractions;
using Plataforma.Application.Telemetry;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Plataforma.Infrastructure.Reporting;

/// <summary>
/// Relatório de uso em PDF: resumo do período e detalhamento por pessoa.
///
/// A granularidade é a mesma da tabela usage_daily — contagem por dia. Não há
/// hora de abertura para mostrar, então o documento vai até "quantas vezes" e
/// "último dia de uso", e não além disso.
/// </summary>
public sealed class UsageReportPdf : IUsageReportPdf
{
    private static readonly CultureInfo Br = new("pt-BR");

    // O rótulo vem da tabela compartilhada, para o PDF dizer o mesmo nome que a
    // pessoa lê no botão do Revit — e o mesmo que o painel mostra na tela.
    private static string NomeFerramenta(string slug) => FerramentaNomes.Exibir(slug);

    private static string DataBr(string iso) =>
        DateOnly.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy", Br)
            : (string.IsNullOrWhiteSpace(iso) ? "—" : iso);

    private static string Num(int n) => n.ToString("N0", Br);

    /// <summary>
    /// Agora no horário de Brasília. O servidor (Railway) roda em UTC, e o
    /// DateTime.Now dele imprimia "emitido em" 3 h adiantado. Pelo fuso
    /// America/Sao_Paulo (a imagem Debian traz a base de fusos); se ele não
    /// existir na máquina, UTC-3 fixo, que é o mesmo desde o fim do horário de
    /// verão, em 2019.
    /// </summary>
    public static DateTime AgoraBrasilia(DateTime utcAgora)
    {
        try
        {
            var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeFromUtc(utcAgora, fuso);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcAgora.AddHours(-3);
        }
    }

    /// <summary>Uma pessoa no relatório: os totais e as ferramentas, da mais usada
    /// para a menos usada.</summary>
    private sealed record Pessoa(string Email, int Total, string UltimoDia, IReadOnlyList<UsoPessoaDto> Ferramentas);

    public byte[] Gerar(UsoRelatorioDto r)
    {
        var pessoas = r.PorPessoa
            .GroupBy(p => p.Email)
            .Select(g => new Pessoa(
                g.Key,
                g.Sum(x => x.Total),
                g.Max(x => x.UltimoDia) ?? "",   // ISO: a ordem de texto é a de data
                g.OrderByDescending(x => x.Total).ThenBy(x => NomeFerramenta(x.Comando)).ToList()))
            .OrderByDescending(p => p.Total)
            .ToList();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1b2128"));

                page.Header().Element(c => Cabecalho(c, r));
                page.Content().Element(c => Conteudo(c, r, pessoas));

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor("#6b7783"));
                    t.Span("página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Cabeçalho completo só na primeira página; nas outras, uma linha
    /// com o período, para não gastar um quarto de cada folha repetindo o
    /// título.</summary>
    private static void Cabecalho(IContainer container, UsoRelatorioDto r)
    {
        var emitido = AgoraBrasilia(DateTime.UtcNow).ToString("dd/MM/yyyy HH:mm", Br) + " (horário de Brasília)";
        var produto = string.IsNullOrWhiteSpace(r.Produto) ? "Todos os produtos" : r.Produto;

        container.Column(col =>
        {
            col.Item().ShowOnce().PaddingBottom(14).Column(topo =>
            {
                topo.Item().Text("FILIPPON SOLUTIONS")
                    .FontSize(8).FontColor("#6b7783");

                topo.Item().PaddingTop(2).Text("Relatório de uso")
                    .FontSize(19).SemiBold();

                topo.Item().PaddingTop(4)
                    .Text($"Período de {DataBr(r.De)} a {DataBr(r.Ate)} · {r.Dias} dia(s)")
                    .FontSize(10).FontColor("#3d4650");

                topo.Item().Text($"{produto} · emitido em {emitido}")
                    .FontSize(9).FontColor("#6b7783");

                topo.Item().PaddingTop(10).LineHorizontal(1).LineColor("#e1e5ea");
            });

            col.Item().SkipOnce().PaddingBottom(10).Column(fino =>
            {
                fino.Item().Text($"Relatório de uso · {DataBr(r.De)} a {DataBr(r.Ate)} · {produto}")
                    .FontSize(8).FontColor("#6b7783");
                fino.Item().PaddingTop(4).LineHorizontal(1).LineColor("#e1e5ea");
            });
        });
    }

    private static void Conteudo(IContainer container, UsoRelatorioDto r, IReadOnlyList<Pessoa> pessoas)
    {
        container.PaddingTop(14).Column(col =>
        {
            col.Spacing(18);

            // Um relatório vazio precisa dizer que está vazio. Uma folha em
            // branco deixa a dúvida entre "ninguém usou" e "o PDF quebrou".
            if (r.TotalGeral == 0)
            {
                col.Item().Text("Nenhuma abertura registrada no período.")
                    .FontSize(11).FontColor("#6b7783");
                return;
            }

            col.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => Kpi(c, "ABERTURAS", Num(r.TotalGeral)));
                row.RelativeItem().Element(c => Kpi(c, "PESSOAS ATIVAS", Num(r.PessoasAtivas)));
                row.RelativeItem().Element(c => Kpi(c, "FERRAMENTAS USADAS", Num(r.Ferramentas.Count)));
            });

            // Cada seção só começa se couberem o título e as primeiras linhas;
            // senão, vai para a página seguinte (sem isto, o título podia ficar
            // sozinho no pé da folha).
            col.Item().EnsureSpace(90).Element(c => UsoPorFerramenta(c, r.Ferramentas));
            col.Item().EnsureSpace(90).Element(c => UsoPorPessoa(c, pessoas));
        });
    }

    private static void UsoPorFerramenta(IContainer container, IReadOnlyList<FerramentaDto> ferramentas)
    {
        container.Column(bloco =>
        {
            bloco.Item().Text("Uso por ferramenta").FontSize(12).SemiBold();

            bloco.Item().PaddingTop(6).Table(tabela =>
            {
                tabela.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.ConstantColumn(80);
                    c.ConstantColumn(70);
                });

                tabela.Header(h =>
                {
                    h.Cell().Element(Th).Text("Ferramenta");
                    h.Cell().Element(Th).AlignRight().Text("Aberturas");
                    h.Cell().Element(Th).AlignRight().Text("Pessoas");
                });

                foreach (var f in ferramentas)
                {
                    tabela.Cell().Element(Td).Text(NomeFerramenta(f.Comando));
                    tabela.Cell().Element(Td).AlignRight().Text(Num(f.Total));
                    tabela.Cell().Element(Td).AlignRight().Text(Num(f.Pessoas));
                }
            });
        });
    }

    /// <summary>
    /// Uma tabela só para todas as pessoas, agrupada: a pessoa aparece uma vez, na
    /// primeira linha do grupo (com os totais dela logo abaixo, na mesma coluna), e
    /// as ferramentas dela seguem linha a linha, da mais usada para a menos usada.
    ///
    /// <para>Substitui uma tabela por pessoa, cada uma com título e espaço próprios,
    /// que deixava o documento comprido (8 páginas para 10 pessoas). A tabela pode
    /// quebrar em qualquer linha, com o cabeçalho repetido, então ninguém precisa
    /// caber numa página (a causa do 500 de 19/10/2026).</para>
    /// </summary>
    private static void UsoPorPessoa(IContainer container, IReadOnlyList<Pessoa> pessoas)
    {
        container.Column(bloco =>
        {
            bloco.Item().Text("Uso por pessoa").FontSize(12).SemiBold();

            bloco.Item().PaddingTop(6).Table(tabela =>
            {
                tabela.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(5);
                    c.ConstantColumn(56);
                    c.ConstantColumn(62);
                });

                tabela.Header(h =>
                {
                    h.Cell().Element(Th).Text("Pessoa");
                    h.Cell().Element(Th).Text("Ferramenta");
                    h.Cell().Element(Th).AlignRight().Text("Aberturas");
                    h.Cell().Element(Th).AlignRight().Text("Último uso");
                });

                foreach (var p in pessoas)
                {
                    for (int i = 0; i < p.Ferramentas.Count; i++)
                    {
                        var f = p.Ferramentas[i];
                        bool inicio = i == 0;

                        // Coluna da pessoa: nome na 1ª linha, totais nas seguintes,
                        // e sem linha entre as células, para ler como um grupo.
                        var pessoa = tabela.Cell().Element(c => CelulaPessoa(c, inicio));
                        if (i == 0) pessoa.Text(p.Email).SemiBold();
                        else if (i == 1) pessoa.Text($"{Num(p.Total)} abertura(s)").FontSize(8).FontColor("#6b7783");
                        else if (i == 2) pessoa.Text($"{Num(p.Ferramentas.Count)} ferramenta(s)").FontSize(8).FontColor("#6b7783");
                        else pessoa.Text("");

                        tabela.Cell().Element(c => CelulaGrupo(c, inicio)).Text(NomeFerramenta(f.Comando));
                        tabela.Cell().Element(c => CelulaGrupo(c, inicio)).AlignRight().Text(Num(f.Total));
                        tabela.Cell().Element(c => CelulaGrupo(c, inicio)).AlignRight().Text(DataBr(f.UltimoDia));
                    }
                }
            });
        });
    }

    private static void Kpi(IContainer container, string rotulo, string valor)
    {
        container
            .Border(1).BorderColor("#e1e5ea").CornerRadius(6)
            .Padding(10)
            .Column(col =>
            {
                col.Item().Text(rotulo).FontSize(7).FontColor("#6b7783");
                col.Item().PaddingTop(3).Text(valor).FontSize(16).SemiBold();
            });
    }

    private static IContainer Th(IContainer container) =>
        container
            .BorderBottom(1).BorderColor("#c9ced5")
            .PaddingVertical(5)
            .DefaultTextStyle(t => t.FontSize(8).SemiBold().FontColor("#3d4650"));

    private static IContainer Td(IContainer container) =>
        container
            .BorderBottom(1).BorderColor("#edf0f3")
            .PaddingVertical(5)
            .DefaultTextStyle(t => t.FontSize(10));

    // Linha que abre o grupo de uma pessoa: traço mais forte em cima, separando
    // uma pessoa da outra.
    private static IContainer InicioGrupo(IContainer container, bool inicio) =>
        inicio ? container.BorderTop(1).BorderColor("#9aa3ad") : container;

    private static IContainer CelulaPessoa(IContainer container, bool inicio) =>
        InicioGrupo(container, inicio)
            .PaddingVertical(3).PaddingRight(8)
            .DefaultTextStyle(t => t.FontSize(9));

    private static IContainer CelulaGrupo(IContainer container, bool inicio) =>
        InicioGrupo(container, inicio)
            .BorderBottom(1).BorderColor("#edf0f3")
            .PaddingVertical(3)
            .DefaultTextStyle(t => t.FontSize(9));
}
