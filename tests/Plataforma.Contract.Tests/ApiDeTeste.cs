using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Plataforma.Infrastructure.Persistence;

namespace Plataforma.Contract.Tests;

/// <summary>
/// Sobe a API inteira EM MEMÓRIA para os testes de contrato: sem porta, sem
/// rede, sem Postgres, sem Railway. O pipeline real roda - roteamento,
/// autenticação, serialização - então o JSON verificado é o mesmo que sai em
/// produção.
///
/// <para>O banco é trocado por um provedor em memória. Isto NÃO enfraquece os
/// testes: o que está sob verificação aqui é o formato da resposta HTTP, não o
/// SQL. Consultas continuam passando pelo mesmo EF Core e pelos mesmos
/// repositórios.</para>
/// </summary>
public sealed class ApiDeTeste : WebApplicationFactory<Plataforma.Api.Controllers.HealthController>
{
    /// <summary>Credenciais do Owner semeado no boot (DataSeeder).</summary>
    public const string OwnerEmail = "owner.contrato@teste.local";
    public const string OwnerSenha = "SenhaDeContrato123";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Configuração mínima exigida no boot. A connection string precisa
        // existir (AddInfrastructure recusa sem ela), mas nunca é usada: o
        // provedor é substituído logo abaixo.
        builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=nao-usado;Database=nao-usado;Username=x;Password=x",
            ["Jwt:Key"] = "chave-de-teste-com-tamanho-suficiente-para-hmac-sha256-nao-reclamar",
            ["Jwt:Issuer"] = "plataforma-plugins",
            ["Jwt:Audience"] = "plataforma-clients",
            ["Seed:Owner:Email"] = OwnerEmail,
            ["Seed:Owner:Password"] = OwnerSenha,
        }));

        builder.ConfigureServices(services =>
        {
            // Remove o Npgsql e põe o provedor em memória no lugar. Base com
            // nome único por fábrica: um teste não enxerga o dado do outro.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            var nomeBase = $"contrato-{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>(o =>
            {
                o.UseInMemoryDatabase(nomeBase);
                // O provedor em memória avisa que não sabe fazer transação.
                // Não é defeito aqui - os endpoints seguem funcionando.
                o.ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });

        return base.CreateHost(builder);
    }
}
