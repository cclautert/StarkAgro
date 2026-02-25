provider "aws" {
  region  = var.aws_region
  profile = var.aws_profile
}

# Backend is declared in versions.tf; pass bucket and dynamodb_table via:
# terraform init -backend-config="bucket=YOUR_BUCKET" -backend-config="dynamodb_table=YOUR_TABLE"
