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

            // A seção só começa se couberem o título e as primeiras linhas; senão,
            // vai para a página seguinte (sem isto, o título podia ficar sozinho no
            // pé da folha).
            col.Item().EnsureSpace(90).Element(c => UsoPorFerramenta(c, r.Ferramentas));

            // "Uso por pessoa" sempre começa numa página nova (pedido de
            // 19/10/2026): a parte de cada pessoa fica separada do resumo.
            col.Item().PageBreak();
            col.Item().Element(c => UsoPorPessoa(c, pessoas));
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
    /// Uma tabela para cada pessoa, com espaço entre uma e outra (pedido de
    /// 19/10/2026). O nome e os totais da pessoa ficam no cabeçalho da tabela dela,
    /// junto dos títulos das colunas: se a lista passar para a página seguinte, o
    /// cabeçalho se repete lá em cima e dá para saber de quem é.
    ///
    /// <para>A tabela pode quebrar em qualquer linha, então ninguém precisa caber
    /// numa página (exigir isso era a causa do 500 de 19/10/2026). Ela só começa se
    /// couberem o cabeçalho e as primeiras linhas; senão, vai para a página
    /// seguinte.</para>
    /// </summary>
    private static void UsoPorPessoa(IContainer container, IReadOnlyList<Pessoa> pessoas)
    {
        container.Column(bloco =>
        {
            bloco.Item().Text("Uso por pessoa").FontSize(12).SemiBold();

            bloco.Item().PaddingTop(10).Column(lista =>
            {
                lista.Spacing(24);   // o espaço entre uma pessoa e outra
                foreach (var p in pessoas)
                    lista.Item().EnsureSpace(110).Element(c => TabelaPessoa(c, p));
            });
        });
    }

    private static void TabelaPessoa(IContainer container, Pessoa p)
    {
        container.Border(1).BorderColor("#dfe3e8").Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(70);
                c.ConstantColumn(80);
            });

            tabela.Header(h =>
            {
                h.Cell().ColumnSpan(3).Background("#f1f3f6").PaddingVertical(7).PaddingHorizontal(10).Row(linha =>
                {
                    linha.RelativeItem().Text(p.Email).SemiBold().FontSize(10.5f);
                    linha.AutoItem().AlignMiddle()
                        .Text($"{Num(p.Total)} abertura(s) · {Num(p.Ferramentas.Count)} ferramenta(s) · último uso {DataBr(p.UltimoDia)}")
                        .FontSize(8).FontColor("#5b6670");
                });
                h.Cell().Element(Th).PaddingLeft(10).Text("Ferramenta");
                h.Cell().Element(Th).AlignRight().Text("Aberturas");
                h.Cell().Element(Th).AlignRight().PaddingRight(10).Text("Último uso");
            });

            foreach (var f in p.Ferramentas)
            {
                tabela.Cell().Element(TdPessoa).PaddingLeft(10).Text(NomeFerramenta(f.Comando));
                tabela.Cell().Element(TdPessoa).AlignRight().Text(Num(f.Total));
                tabela.Cell().Element(TdPessoa).AlignRight().PaddingRight(10).Text(DataBr(f.UltimoDia));
            }
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

    // Linha da tabela de uma pessoa: um pouco mais compacta que a do uso por
    // ferramenta, porque a soma das pessoas é a parte longa do documento.
    private static IContainer TdPessoa(IContainer container) =>
        container
            .BorderBottom(1).BorderColor("#edf0f3")
            .PaddingVertical(4)
            .DefaultTextStyle(t => t.FontSize(9.5f));
}
