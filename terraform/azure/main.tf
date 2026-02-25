terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "4.58.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }
}


provider "azurerm" {
  use_cli = true
  subscription_id = "0d4b2ec6-8a33-4693-994d-022fac93ecca"
  features {    
  }
}

resource "azurerm_resource_group" "name" {
    name     = "rg-agripeweb"
    location = "Brazil South"
  
}

# Azure Container Registry para imagens Docker da API e da UI
resource "random_id" "acr_suffix" {
  byte_length = 4
}

resource "azurerm_container_registry" "acr" {
  name                = "agripewebacr${random_id.acr_suffix.hex}"
  resource_group_name = azurerm_resource_group.name.name
  location            = azurerm_resource_group.name.location
  sku                 = "Basic"
  admin_enabled       = true
}

resource "azurerm_service_plan" "service_plan" {
    name                = "agripe-app-service-plan"
    location            = azurerm_resource_group.name.location
    resource_group_name = azurerm_resource_group.name.name
    sku_name            = "B1"  # If apply fails with "quota" 401: request App Service quota in Azure Portal (Subscriptions → Usage + quotas)
    os_type             = "Linux"
}

# Web App API (imagem Docker do ACR; autenticação via Managed Identity)
resource "azurerm_linux_web_app" "api" {
  service_plan_id      = azurerm_service_plan.service_plan.id
  name                = "agripeweb-api"
  location            = azurerm_resource_group.name.location
  resource_group_name = azurerm_resource_group.name.name

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                          = false
    container_registry_use_managed_identity = true
    application_stack {
      docker_image_name   = "agripeweb-api:latest"
      docker_registry_url = "https://${azurerm_container_registry.acr.login_server}"
    }
  }

  app_settings = {
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    "WEBSITES_PORT"                       = "8080"
  }
}

resource "azurerm_role_assignment" "api_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

# Web App UI (imagem Docker do ACR; autenticação via Managed Identity)
resource "azurerm_linux_web_app" "ui" {
  service_plan_id      = azurerm_service_plan.service_plan.id
  name                = "agripeweb-ui"
  location            = azurerm_resource_group.name.location
  resource_group_name = azurerm_resource_group.name.name

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on                          = false
    container_registry_use_managed_identity = true
    application_stack {
      docker_image_name   = "agripeweb-ui:latest"
      docker_registry_url = "https://${azurerm_container_registry.acr.login_server}"
    }
  }

  app_settings = {
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
    "WEBSITES_PORT"                       = "80"
    "API_BASE_URL"                       = "https://agripeweb-api.azurewebsites.net"
  }
}

resource "azurerm_role_assignment" "ui_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.ui.identity[0].principal_id
}

output "api_url" {
  value = "https://${azurerm_linux_web_app.api.name}.azurewebsites.net"
}

output "ui_url" {
  value = "https://${azurerm_linux_web_app.ui.name}.azurewebsites.net"
}

output "acr_login_server" {
  value     = azurerm_container_registry.acr.login_server
  sensitive = false
}

output "acr_name" {
  value = azurerm_container_registry.acr.name
}

