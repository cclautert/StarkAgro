# Security Setup Guide

This document explains how to configure sensitive settings using environment variables.

## Environment Variables

All sensitive configuration values should be set via environment variables. The application will read these values and use them to replace placeholders in configuration files.

### Required Environment Variables

#### Database Connection Strings

**Development:**
- `DB_SERVER` - Database server name (e.g., "ZEUS")
- `DB_USER` - Database user ID (e.g., "sa")
- `DB_PASSWORD` - Database password
- `AZURE_DB_SERVER` - Azure SQL server (e.g., "tcp:agripewebdatabase.database.windows.net,1433")
- `AZURE_DB_NAME` - Azure database name
- `AZURE_DB_USER` - Azure database user
- `AZURE_DB_PASSWORD` - Azure database password

**Production:**
- `AWS_DB_SERVER` - AWS RDS server
- `AWS_DB_USER` - AWS database user
- `AWS_DB_PASSWORD` - AWS database password
- `AZURE_DB_SERVER` - Azure SQL server
- `AZURE_DB_NAME` - Azure database name
- `AZURE_DB_USER` - Azure database user
- `AZURE_DB_PASSWORD` - Azure database password

#### JWT Settings

- `JWT_SECRET_KEY` - Secret key for JWT token signing (must be at least 32 characters)

## Setting Environment Variables

.NET Core automatically reads environment variables and uses them to override configuration values. Use the `:` separator to map to nested configuration.

### Connection Strings
Set environment variables using the connection string name:
- `ConnectionStrings__DefaultConnection` - Overrides the DefaultConnection connection string
- `ConnectionStrings__DockerConnection` - Overrides the DockerConnection connection string
- `ConnectionStrings__AzureConnection` - Overrides the AzureConnection connection string
- `ConnectionStrings__AWSConnection` - Overrides the AWSConnection connection string

### JWT Settings
- `JwtSettings__secretkey` - JWT secret key
- `JwtSettings__issuer` - JWT issuer
- `JwtSettings__audience` - JWT audience

### Windows (PowerShell)
```powershell
$env:ConnectionStrings__DefaultConnection = "Server=ZEUS;Database=AGPRIPEDB;User Id=sa;Password=YourPassword;..."
$env:JwtSettings__secretkey = "YourSecretKey"
```

### Windows (Command Prompt)
```cmd
set ConnectionStrings__DefaultConnection=Server=ZEUS;Database=AGPRIPEDB;User Id=sa;Password=YourPassword;...
set JwtSettings__secretkey=YourSecretKey
```

### Linux/macOS
```bash
export ConnectionStrings__DefaultConnection="Server=ZEUS;Database=AGPRIPEDB;User Id=sa;Password=YourPassword;..."
export JwtSettings__secretkey="YourSecretKey"
```

### Docker
```yaml
environment:
  - DB_PASSWORD=YourPassword
  - JWT_SECRET_KEY=YourSecretKey
```

### Azure App Service
1. Go to Configuration → Application Settings
2. Add each environment variable as a new application setting

### AWS
1. Use AWS Systems Manager Parameter Store or Secrets Manager
2. Or set in ECS task definitions or EC2 instance environment variables

## User Secrets (Development Only)

For local development, you can use .NET User Secrets instead of environment variables:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=ZEUS;Database=AGPRIPEDB;User Id=sa;Password=YourPassword;..."
dotnet user-secrets set "JwtSettings:secretkey" "YourSecretKey"
```

## Important Security Notes

1. **Never commit** `appsettings.Development.json` or `appsettings.Production.json` with real credentials
2. Use the template files (`*.template.json`) as reference
3. Rotate secrets regularly
4. Use different secrets for each environment
5. Store production secrets in a secure vault (Azure Key Vault, AWS Secrets Manager, etc.)

## Password Hashing

All user passwords are now hashed using BCrypt before storage. The default IoT user password is also hashed during database seeding.

**Default IoT User Credentials:**
- Email: `IOT_EMAIL_REDACTED`
- Password: `IOT_PASS_REDACTED` (change this in production!)
