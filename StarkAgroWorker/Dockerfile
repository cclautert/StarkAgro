# Build stage (context: repo root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["StarkAgroAPI/StarkAgroAPI.csproj", "StarkAgroAPI/"]
COPY ["StarkAgroWorker/StarkAgroWorker.csproj", "StarkAgroWorker/"]
RUN dotnet restore "StarkAgroWorker/StarkAgroWorker.csproj"
COPY ["StarkAgroAPI/", "StarkAgroAPI/"]
COPY ["StarkAgroWorker/", "StarkAgroWorker/"]
RUN dotnet publish "StarkAgroWorker/StarkAgroWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StarkAgroWorker.dll"]
