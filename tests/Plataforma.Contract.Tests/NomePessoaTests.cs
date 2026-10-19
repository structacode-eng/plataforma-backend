using Plataforma.Application.Telemetry;

namespace Plataforma.Contract.Tests;

/// <summary>
/// O nome que o relatório de uso mostra no lugar do e-mail (as contas não têm
/// campo de nome). Mesma regra do modelo de relatório aprovado em 19/10/2026.
/// </summary>
public sealed class NomePessoaTests
{
    [Theory]
    [InlineData("bruno.cantini@filippon.com.br", "Bruno Cantini")]
    [InlineData("henriquekasper@hotmail.com", "Henriquekasper")]
    [InlineData("leonardomsehn@gmail.com", "Leonardomsehn")]
    [InlineData("maria.alice.matos@empresa.com", "Maria Alice Matos")]
    [InlineData("MARIA.DA.SILVA@empresa.com", "Maria da Silva")]      // partícula minúscula no meio
    [InlineData("jose_de-souza+obra@x.com", "Jose de Souza Obra")]   // . _ - + separam
    [InlineData("(conta removida)", "(conta removida)")]             // sem @: passa como está
    [InlineData("", "")]
    public void Nome_sai_do_email(string email, string esperado)
        => Assert.Equal(esperado, NomePessoa.DoEmail(email));
}
