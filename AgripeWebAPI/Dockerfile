# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AgripeWebAPI.csproj", "."]
RUN dotnet restore "AgripeWebAPI.csproj"
COPY . .
RUN dotnet build "AgripeWebAPI.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "AgripeWebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && \
    apt-get install -y --no-install-recommends libgssapi-krb5-2 libkrb5-3 krb5-user && \
    rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgripeWebAPI.dll"]