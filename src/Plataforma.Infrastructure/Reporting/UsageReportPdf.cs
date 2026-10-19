using System.Globalization;
using System.Reflection;
using Plataforma.Application.Abstractions;
using Plataforma.Application.Telemetry;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Plataforma.Infrastructure.Reporting;

/// <summary>
/// Relatório de uso em PDF, no modelo "dashboard" aprovado em 19/10/2026
/// (Relatorio_Uso_Filippon_2026-09-28_a_2026-10-19.pdf): faixa da Structa
/// Studio, título, período, quatro indicadores e dois gráficos de barras
/// horizontais, um por ferramenta e outro por pessoa.
///
/// <para>Todas as contas entram em "Uso por pessoa", inclusive quem não abriu
/// nada no período (barra vazia, no fim). As pessoas aparecem pelo nome, tirado
/// do e-mail (<see cref="NomePessoa"/>).</para>
///
/// <para>A granularidade é a da tabela usage_daily, contagem por dia: o documento
/// vai até "quantas vezes" e "último dia de uso".</para>
/// </summary>
public sealed class UsageReportPdf : IUsageReportPdf
{
    private static readonly CultureInfo Br = new("pt-BR");

    // Cores do modelo aprovado.
    private const string Navy = "#1c2757";
    private const string Laranja = "#ee7b39";
    private const string LaranjaClaro = "#f6bc98";
    private const string Creme = "#f4f1ec";
    private const string Borda = "#e3e0da";
    private const string Tinta = "#222222";
    private const string Cinza = "#6b6b6b";
    private const string CinzaFaixa = "#d6dae8";

    private const string Fonte = "Liberation Sans";
    private const string Produto = "Toolbar Filippon Engenharia";

    // Larguras dos gráficos, em pontos (A4 com 2 cm de margem = ~482 pt úteis).
    private const float RotuloFerramenta = 165, BarraFerramenta = 235;
    private const float RotuloPessoa = 80, BarraPessoa = 210;

    private static readonly byte[] Logo;

    /// <summary>A fonte do modelo (Liberation Sans, licença SIL OFL) e o logo vão
    /// dentro da DLL: o servidor Linux do Railway não tem fontes instaladas, e sem
    /// isto o PDF sairia com a fonte padrão do QuestPDF.</summary>
    static UsageReportPdf()
    {
        var asm = typeof(UsageReportPdf).Assembly;
        foreach (var nome in new[] { "Relatorio.LiberationSans-Regular.ttf", "Relatorio.LiberationSans-Bold.ttf" })
        {
            using var s = asm.GetManifestResourceStream(nome);
            if (s != null) FontManager.RegisterFont(s);
        }
        Logo = Recurso(asm, "Relatorio.logo-structa-horizontal.png");
    }

    private static byte[] Recurso(Assembly asm, string nome)
    {
        using var s = asm.GetManifestResourceStream(nome);
        if (s == null) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    // O rótulo vem da tabela compartilhada, para o PDF dizer o mesmo nome que a
    // pessoa lê no botão do Revit, e o mesmo que o painel mostra na tela.
    private static string NomeFerramenta(string slug) => FerramentaNomes.Exibir(slug);

    private static string DataBr(string iso) =>
        DateOnly.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy", Br)
            : (string.IsNullOrWhiteSpace(iso) ? "-" : iso);

    private static string DataCurta(string iso) =>
        DateOnly.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM", Br)
            : iso;

    private static string Num(int n) => n.ToString("N0", Br);

    private static string Plural(int n, string um, string varios) => $"{Num(n)} {(n == 1 ? um : varios)}";

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

    private static string RotuloProduto(string? produto) => produto?.Trim().ToLowerInvariant() switch
    {
        null or "" => "Todos os produtos",
        "revit-plugin" => "Plugin Revit",
        "solutions" => "Filippon Solutions",
        var p => p,
    };

    /// <summary>Uma linha do gráfico de pessoas: quem usou e quem não usou.</summary>
    private sealed record LinhaPessoa(string Nome, int Total, int Ferramentas, string UltimoDia,
                                      bool SemUso, bool Ativa, DateTime? UltimoAcessoUtc);

    public byte[] Gerar(UsoRelatorioDto r)
    {
        var comUso = r.PorPessoa
            .GroupBy(p => p.Email)
            .Select(g => new LinhaPessoa(
                NomePessoa.DoEmail(g.Key),
                g.Sum(x => x.Total),
                g.Count(),
                g.Max(x => x.UltimoDia) ?? "",   // ISO: a ordem de texto é a de data
                false, true, null))
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.Nome, StringComparer.Create(Br, true));

        var semUso = r.SemUso
            .Select(s => new LinhaPessoa(NomePessoa.DoEmail(s.Email), 0, 0, "", true, s.Ativa, s.UltimoAcessoUtc))
            .OrderBy(p => p.Nome, StringComparer.Create(Br, true));

        var pessoas = comUso.Concat(semUso).ToList();
        var emitido = AgoraBrasilia(DateTime.UtcNow);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2, Unit.Centimetre);
                page.MarginTop(1.6f, Unit.Centimetre);
                page.MarginBottom(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily(Fonte).FontSize(8.5f).FontColor(Tinta));

                page.Header().Element(c => Cabecalho(c, r, emitido));
                page.Content().Element(c => Conteudo(c, r, pessoas));
                page.Footer().Element(Rodape);
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = "Relatório de Uso · " + Produto,
            Author = "Structa Studio",
            Subject = $"Uso de {DataBr(r.De)} a {DataBr(r.Ate)}",
        })
        .GeneratePdf();
    }

    // ------------------------------------------------------------------
    // Cabeçalho e rodapé
    // ------------------------------------------------------------------

    /// <summary>Primeira página: faixa da marca, título e barra do período. Nas
    /// outras, uma linha discreta com o período.</summary>
    private static void Cabecalho(IContainer container, UsoRelatorioDto r, DateTime emitido)
    {
        container.Column(col =>
        {
            col.Item().ShowOnce().Column(topo =>
            {
                topo.Item().Background(Navy).CornerRadius(6).PaddingVertical(21).PaddingHorizontal(22).Row(faixa =>
                {
                    if (Logo.Length > 0)
                        faixa.RelativeItem().AlignMiddle().Height(22).AlignLeft().Image(Logo).FitHeight();
                    else
                        faixa.RelativeItem().AlignMiddle().Text(t =>
                        {
                            t.Span("Structa").FontSize(19).Bold().FontColor(Colors.White);
                            t.Span("Studio").FontSize(19).FontColor(Colors.White);
                        });
                    faixa.AutoItem().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignRight().Text(Produto).FontSize(8.5f).FontColor(CinzaFaixa);
                        c.Item().PaddingTop(2).AlignRight().Text(RotuloProduto(r.Produto)).FontSize(8.5f).FontColor(CinzaFaixa);
                    });
                });

                topo.Item().PaddingTop(18).Text("Relatório de Uso").FontSize(24).Bold();
                topo.Item().PaddingTop(2).Text(Produto).FontSize(13).Bold().FontColor(Laranja);

                topo.Item().PaddingTop(12).Background(Creme).CornerRadius(4).PaddingVertical(8).PaddingHorizontal(12).Row(linha =>
                {
                    linha.RelativeItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(9).FontColor(Cinza));
                        t.Span("Período ");
                        t.Span($"{DataBr(r.De)} a {DataBr(r.Ate)}").Bold().FontColor(Tinta);
                        t.Span($" · {Plural(r.Dias, "dia", "dias")}");
                    });
                    linha.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(9).FontColor(Cinza));
                        t.Span("Emitido em ");
                        t.Span(emitido.ToString("dd/MM/yyyy HH:mm", Br)).Bold().FontColor(Tinta);
                    });
                });
            });

            col.Item().SkipOnce().PaddingBottom(14)
                .Text($"Relatório de Uso · {Produto} · {DataBr(r.De)} a {DataBr(r.Ate)}")
                .FontSize(7.5f).FontColor(Cinza);
        });
    }

    private static void Rodape(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.8f).LineColor(Laranja);
            col.Item().PaddingTop(6).Row(linha =>
            {
                linha.RelativeItem();
                linha.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Cinza));
                    t.Span("Structa Studio").Bold().FontColor(Tinta);
                    t.Span("  •  Filippon Engenharia");
                });
                linha.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Cinza));
                    t.Span("página ");
                    t.CurrentPageNumber();
                });
            });
        });
    }

    // ------------------------------------------------------------------
    // Conteúdo
    // ------------------------------------------------------------------

    private static void Conteudo(IContainer container, UsoRelatorioDto r, IReadOnlyList<LinhaPessoa> pessoas)
    {
        container.PaddingTop(16).Column(col =>
        {
            col.Spacing(22);

            int media = r.PessoasAtivas > 0
                ? (int)Math.Round((double)r.TotalGeral / r.PessoasAtivas, MidpointRounding.AwayFromZero)
                : 0;
            string contas = r.TotalContas > 0
                ? $"com uso, de {Plural(r.TotalContas, "conta", "contas")}"
                : "com ao menos um uso";

            col.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => Kpi(c, "ABERTURAS", Num(r.TotalGeral), "total de aberturas no período"));
                row.RelativeItem().Element(c => Kpi(c, "PESSOAS ATIVAS", Num(r.PessoasAtivas), contas));
                row.RelativeItem().Element(c => Kpi(c, "FERRAMENTAS USADAS", Num(r.Ferramentas.Count), "abertas ao menos uma vez"));
                row.RelativeItem().Element(c => Kpi(c, "MÉDIA POR PESSOA", Num(media), "aberturas por pessoa ativa"));
            });

            var periodo = $"de {DataCurta(r.De)} a {DataBr(r.Ate)}";

            col.Item().EnsureSpace(90).Element(c => Secao(c, "Uso por ferramenta",
                $"Aberturas por ferramenta {periodo} · ao lado, quantas pessoas usaram cada uma",
                lista =>
                {
                    if (r.Ferramentas.Count == 0)
                    {
                        lista.Item().Text("Nenhuma abertura registrada no período.").FontSize(9).FontColor(Cinza);
                        return;
                    }
                    int max = r.Ferramentas.Max(f => f.Total);
                    for (int i = 0; i < r.Ferramentas.Count; i++)
                    {
                        var f = r.Ferramentas[i];
                        lista.Item().Element(c => Barra(c, NomeFerramenta(f.Comando), RotuloFerramenta, BarraFerramenta,
                            f.Total, max, destaque: i < 3, rotuloApagado: false,
                            complemento: $"  · {Plural(f.Pessoas, "pessoa", "pessoas")}"));
                    }
                }));

            col.Item().EnsureSpace(90).Element(c => Secao(c, "Uso por pessoa",
                $"Total de aberturas por pessoa {periodo} · ferramentas usadas e último uso ao lado",
                lista =>
                {
                    if (pessoas.Count == 0)
                    {
                        lista.Item().Text("Nenhuma conta cadastrada.").FontSize(9).FontColor(Cinza);
                        return;
                    }
                    int max = Math.Max(1, pessoas.Max(p => p.Total));
                    for (int i = 0; i < pessoas.Count; i++)
                    {
                        var p = pessoas[i];
                        lista.Item().Element(c => Barra(c, p.Nome, RotuloPessoa, BarraPessoa,
                            p.Total, max, destaque: !p.SemUso && i < 3, rotuloApagado: p.SemUso,
                            complemento: ComplementoPessoa(p)));
                    }
                }));
        });
    }

    private static string ComplementoPessoa(LinhaPessoa p)
    {
        if (!p.SemUso)
            return $"  · {Plural(p.Ferramentas, "ferramenta", "ferramentas")} · último uso {DataBr(p.UltimoDia)}";

        var acesso = p.UltimoAcessoUtc is { } u
            ? "último acesso " + AgoraBrasilia(DateTime.SpecifyKind(u, DateTimeKind.Utc)).ToString("dd/MM/yyyy", Br)
            : "nunca entrou no plugin";
        return $"  · sem uso no período · {acesso}{(p.Ativa ? "" : " · conta desativada")}";
    }

    /// <summary>Seção com título sublinhado em laranja. Se a lista passar para a
    /// página seguinte, o título se repete lá com "continuação" no lugar do
    /// subtítulo, como no modelo.</summary>
    private static void Secao(IContainer container, string titulo, string subtitulo, Action<ColumnDescriptor> conteudo)
    {
        container.Decoration(d =>
        {
            d.Before().PaddingBottom(10).Column(cab =>
            {
                cab.Item().Text(titulo).FontSize(15).Bold();
                cab.Item().PaddingTop(3).LineHorizontal(1.2f).LineColor(Laranja);
                cab.Item().ShowOnce().PaddingTop(6).Text(subtitulo).FontSize(8.5f).FontColor(Cinza);
                cab.Item().SkipOnce().PaddingTop(6).Text("continuação").FontSize(8.5f).FontColor(Cinza);
            });
            d.Content().Column(conteudo);
        });
    }

    /// <summary>Uma barra horizontal: rótulo à direita da coluna de rótulos, a barra
    /// proporcional ao maior valor e, logo depois dela, o número em negrito e o
    /// complemento em cinza. As três primeiras em laranja forte.</summary>
    private static void Barra(IContainer container, string rotulo, float largRotulo, float largBarra,
                              int valor, int max, bool destaque, bool rotuloApagado, string complemento)
    {
        float comprimento = valor <= 0 ? 0 : Math.Max(2f, largBarra * valor / Math.Max(1, max));

        container.PaddingVertical(2.6f).Row(linha =>
        {
            linha.ConstantItem(largRotulo).AlignRight().AlignMiddle()
                .Text(rotulo).FontSize(8.5f).FontColor(rotuloApagado ? Cinza : Tinta);
            linha.ConstantItem(8);
            linha.RelativeItem().AlignMiddle().Row(b =>
            {
                if (comprimento > 0)
                {
                    b.ConstantItem(comprimento).AlignMiddle().Height(9)
                        .Background(destaque ? Laranja : LaranjaClaro).CornerRadius(2);
                    b.ConstantItem(6);
                }
                b.RelativeItem().AlignMiddle().Text(t =>
                {
                    t.Span(Num(valor)).FontSize(8).Bold().FontColor(rotuloApagado ? Cinza : Tinta);
                    t.Span(complemento).FontSize(8).FontColor(Cinza);
                });
            });
        });
    }

    private static void Kpi(IContainer container, string rotulo, string valor, string legenda)
    {
        container.Border(1).BorderColor(Borda).Column(col =>
        {
            col.Item().Height(3).Background(Laranja);
            col.Item().PaddingHorizontal(10).PaddingTop(9).PaddingBottom(10).Column(c =>
            {
                c.Item().Text(rotulo).FontSize(7).Bold().FontColor(Cinza);
                c.Item().PaddingTop(4).Text(valor).FontSize(20).Bold();
                c.Item().PaddingTop(5).Text(legenda).FontSize(6.8f).FontColor(Cinza);
            });
        });
    }
}
