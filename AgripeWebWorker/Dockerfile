# Build stage (context: repo root)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AgripeWebAPI/AgripeWebAPI.csproj", "AgripeWebAPI/"]
COPY ["AgripeWebWorker/AgripeWebWorker.csproj", "AgripeWebWorker/"]
RUN dotnet restore "AgripeWebWorker/AgripeWebWorker.csproj"
COPY ["AgripeWebAPI/", "AgripeWebAPI/"]
COPY ["AgripeWebWorker/", "AgripeWebWorker/"]
RUN dotnet publish "AgripeWebWorker/AgripeWebWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AgripeWebWorker.dll"]
