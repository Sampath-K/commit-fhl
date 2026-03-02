FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CommitApi.csproj", "."]
RUN dotnet restore "CommitApi.csproj"
COPY . .
# Exclude test project from API build
RUN dotnet build "CommitApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CommitApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Non-root user for security (P-05) — Debian syntax (dotnet/aspnet:10.0 is Debian-based)
RUN groupadd --system --gid 1001 appgroup && \
    useradd --system --uid 1001 --gid appgroup appuser
USER appuser

ENTRYPOINT ["dotnet", "CommitApi.dll"]
