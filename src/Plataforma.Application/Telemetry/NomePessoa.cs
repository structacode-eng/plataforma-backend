using System.Globalization;

namespace Plataforma.Application.Telemetry;

/// <summary>
/// Nome de exibição de uma pessoa no relatório de uso, tirado do e-mail (as
/// contas não têm campo de nome). A parte antes do @ é quebrada em ".", "_",
/// "-" e "+", e cada pedaço ganha inicial maiúscula:
/// "bruno.cantini@..." vira "Bruno Cantini" e "henriquekasper@..." vira
/// "Henriquekasper". Partículas ("da", "de", "do", "dos", "das", "e") ficam em
/// minúscula no meio do nome: "maria.da.silva" vira "Maria da Silva".
///
/// <para>É a mesma regra do modelo de relatório aprovado em 19/10/2026.</para>
/// </summary>
public static class NomePessoa
{
    private static readonly CultureInfo Br = new("pt-BR");
    private static readonly HashSet<string> Particulas = new(StringComparer.OrdinalIgnoreCase)
    {
        "da", "de", "do", "das", "dos", "e",
    };

    public static string DoEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "";
        var local = email.Trim();
        int arroba = local.IndexOf('@');
        if (arroba < 0) return local;            // "(conta removida)" e afins passam como estão
        local = local[..arroba];

        var partes = local.Split(new[] { '.', '_', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return email.Trim();

        var nome = partes.Select((p, i) =>
            i > 0 && Particulas.Contains(p)
                ? p.ToLower(Br)
                : char.ToUpper(p[0], Br) + p[1..].ToLower(Br));
        return string.Join(" ", nome);
    }
}
