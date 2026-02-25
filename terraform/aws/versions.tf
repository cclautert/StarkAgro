terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Use local backend for initial run; switch to S3 for team/production (see README).
  backend "local" {
    path = "terraform.tfstate"
  }
}
