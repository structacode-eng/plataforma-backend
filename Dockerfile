# ============================================================
#  Backend Plataforma — imagem de produção (multi-stage)
#  O Railway constrói isto no servidor dele (não precisa de
#  Docker na sua máquina). Publica só a API; o banco é o Neon.
# ============================================================

# ---- Estágio 1: build (SDK completo) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia só os .csproj primeiro para aproveitar o cache de restore.
COPY Plataforma.sln ./
COPY src/Plataforma.Domain/Plataforma.Domain.csproj                 src/Plataforma.Domain/
COPY src/Plataforma.Application/Plataforma.Application.csproj         src/Plataforma.Application/
COPY src/Plataforma.Infrastructure/Plataforma.Infrastructure.csproj  src/Plataforma.Infrastructure/
COPY src/Plataforma.Api/Plataforma.Api.csproj                        src/Plataforma.Api/
RUN dotnet restore src/Plataforma.Api/Plataforma.Api.csproj

# Copia o resto do código e publica em Release.
COPY . .
RUN dotnet publish src/Plataforma.Api/Plataforma.Api.csproj -c Release -o /app /p:UseAppHost=false

# ---- Estágio 2: runtime (só o ASP.NET, imagem menor) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# O relatório de uso em PDF (QuestPDF) desenha via SkiaSharp, que no Linux não
# inicializa sem o fontconfig. A imagem do ASP.NET não o traz, e a falta dele só
# aparece quando alguém clica em "Baixar PDF" — nunca no build.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Produção: sem Swagger, carrega config de variáveis de ambiente.
ENV ASPNETCORE_ENVIRONMENT=Production
# O Railway injeta a porta via variável PORT (o Program.cs faz o bind).
EXPOSE 8080

ENTRYPOINT ["dotnet", "Plataforma.Api.dll"]
