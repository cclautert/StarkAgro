# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AgripeWebAPI.csproj", "."]
RUN dotnet restore "AgripeWebAPI.csproj"
COPY . .
RUN dotnet build "AgripeWebAPI.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "AgripeWebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# Install Kerberos dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends libgssapi-krb5-2 libkrb5-3 krb5-user && \
    rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgripeWebAPI.dll"]