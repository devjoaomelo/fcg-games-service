# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia csproj para restauração (contexto = raiz do repo)
COPY src/FCG.Games.Api/FCG.Games.Api.csproj FCG.Games.Api/
COPY src/FCG.Games.Application/FCG.Games.Application.csproj FCG.Games.Application/
COPY src/FCG.Games.Domain/FCG.Games.Domain.csproj FCG.Games.Domain/
COPY src/FCG.Games.Infra/FCG.Games.Infra.csproj FCG.Games.Infra/
RUN dotnet restore FCG.Games.Api/FCG.Games.Api.csproj

# Copia o restante do código e publica
COPY src/ ./
RUN dotnet publish FCG.Games.Api/FCG.Games.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FCG.Games.Api.dll"]