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

    // Mesma transformação que o painel faz em nomeFerramenta(): o banco guarda
    // slug ("cotas_furacao") e as duas telas mostram "Cotas Furacao".
    private static string NomeFerramenta(string slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? "—"
            : Br.TextInfo.ToTitleCase(slug.Replace('_', ' '));

    private static string DataBr(string iso) =>
        DateOnly.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy", Br)
            : (string.IsNullOrWhiteSpace(iso) ? "—" : iso);

    private static string Num(int n) => n.ToString("N0", Br);

    public byte[] Gerar(UsoRelatorioDto r)
    {
        var pessoas = r.PorPessoa
            .GroupBy(p => p.Email)
            .Select(g => (
                Email: g.Key,
                Total: g.Sum(x => x.Total),
                Ferramentas: (IReadOnlyList<UsoPessoaDto>)g.OrderByDescending(x => x.Total).ToList()))
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

    private static void Cabecalho(IContainer container, UsoRelatorioDto r)
    {
        var emitido = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Br);
        var produto = string.IsNullOrWhiteSpace(r.Produto) ? "Todos os produtos" : r.Produto;

        container.PaddingBottom(14).Column(col =>
        {
            col.Item().Text("FILIPPON SOLUTIONS")
                .FontSize(8).FontColor("#6b7783");

            col.Item().PaddingTop(2).Text("Relatório de uso")
                .FontSize(19).SemiBold();

            col.Item().PaddingTop(4)
                .Text($"Período de {DataBr(r.De)} a {DataBr(r.Ate)} · {r.Dias} dia(s)")
                .FontSize(10).FontColor("#3d4650");

            col.Item().Text($"{produto} · emitido em {emitido}")
                .FontSize(9).FontColor("#6b7783");

            col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#e1e5ea");
        });
    }

    private static void Conteudo(
        IContainer container,
        UsoRelatorioDto r,
        IReadOnlyList<(string Email, int Total, IReadOnlyList<UsoPessoaDto> Ferramentas)> pessoas)
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

            col.Item().Column(bloco =>
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

                    foreach (var f in r.Ferramentas)
                    {
                        tabela.Cell().Element(Td).Text(NomeFerramenta(f.Comando));
                        tabela.Cell().Element(Td).AlignRight().Text(Num(f.Total));
                        tabela.Cell().Element(Td).AlignRight().Text(Num(f.Pessoas));
                    }
                });
            });

            col.Item().Column(bloco =>
            {
                bloco.Item().Text("Detalhamento por pessoa").FontSize(12).SemiBold();

                foreach (var p in pessoas)
                {
                    // Uma pessoa não deve começar no fim de uma página e
                    // continuar na seguinte com a tabela órfã do nome.
                    bloco.Item().PaddingTop(10).ShowEntire().Column(pessoa =>
                    {
                        pessoa.Item().Row(linha =>
                        {
                            linha.RelativeItem().Text(p.Email).SemiBold().FontSize(11);
                            linha.ConstantItem(130).AlignRight()
                                .Text($"{Num(p.Total)} abertura(s)")
                                .FontSize(10).FontColor("#6b7783");
                        });

                        pessoa.Item().PaddingTop(4).Table(tabela =>
                        {
                            tabela.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.ConstantColumn(80);
                                c.ConstantColumn(90);
                            });

                            tabela.Header(h =>
                            {
                                h.Cell().Element(Th).Text("Ferramenta");
                                h.Cell().Element(Th).AlignRight().Text("Aberturas");
                                h.Cell().Element(Th).AlignRight().Text("Último uso");
                            });

                            foreach (var f in p.Ferramentas)
                            {
                                tabela.Cell().Element(Td).Text(NomeFerramenta(f.Comando));
                                tabela.Cell().Element(Td).AlignRight().Text(Num(f.Total));
                                tabela.Cell().Element(Td).AlignRight().Text(DataBr(f.UltimoDia));
                            }
                        });
                    });
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
}
