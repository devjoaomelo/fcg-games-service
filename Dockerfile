# ============================================
# STAGE 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copiar .csproj e restaurar dependências
COPY src/FCG.Games.Api/FCG.Games.Api.csproj FCG.Games.Api/
COPY src/FCG.Games.Application/FCG.Games.Application.csproj FCG.Games.Application/
COPY src/FCG.Games.Domain/FCG.Games.Domain.csproj FCG.Games.Domain/
COPY src/FCG.Games.Infra/FCG.Games.Infra.csproj FCG.Games.Infra/
RUN dotnet restore FCG.Games.Api/FCG.Games.Api.csproj

# Copiar código e publicar
COPY src/ ./
RUN dotnet publish FCG.Games.Api/FCG.Games.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ============================================
# STAGE 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

# Instalar curl para healthcheck
RUN apk add --no-cache curl

WORKDIR /app
EXPOSE 8080

# Usuário não-root (segurança)
RUN adduser -u 1000 --disabled-password --gecos "" appuser && \
    chown -R appuser:appuser /app
USER appuser

# Copiar arquivos publicados
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FCG.Games.Api.dll"]
