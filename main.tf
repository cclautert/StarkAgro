terraform {
  required_providers {
    azurerm = {
      source = "hashicorp/azurerm"
      version = "4.58.0"
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
    location = "East US"
  
}

resource "azurerm_service_plan" "service_plan" {
    name                = "agripe-app-service-plan"
    location            = azurerm_resource_group.name.location
    resource_group_name = azurerm_resource_group.name.name
    sku_name            = "F1"
    os_type             = "Linux"
}

resource "azurerm_linux_web_app" "servicer_app" {
    service_plan_id = azurerm_service_plan.service_plan.id
    name            = "agripeweb-app"
    location        = azurerm_resource_group.name.location
    resource_group_name = azurerm_resource_group.name.name    

    site_config {
        always_on = false
        application_stack {
            //docker_image_name = "mcr.microsoft.com/dotnet/aspnet:8.0"
            dotnet_version = "8.0"
        }
    }

  app_settings = {
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "false"
  }
}

output "name" {
  value = "https://${azurerm_linux_web_app.servicer_app.name}.azurewebsites.net"
}