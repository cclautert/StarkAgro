variable "aws_region" {
  description = "AWS region for resources"
  type        = string
  default     = "us-east-1"
}

variable "alb_subnet_ids" {
  description = "Optional: list of 2+ subnet IDs for the ALB (required in regions where default VPC has only 1 subnet, e.g. sa-east-1). If not set, Terraform uses 2 subnets from default VPC or creates one."
  type        = list(string)
  default     = null
}

variable "aws_profile" {
  description = "AWS CLI profile name (optional; leave null to use default credential chain)"
  type        = string
  default     = null
}

variable "environment" {
  description = "Environment name (e.g. dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "mongodb_connection_string" {
  description = "MongoDB connection string (e.g. Atlas or DocumentDB). Pass via TF_VAR or terraform.tfvars; do not commit."
  type        = string
  sensitive   = true
}

variable "mongodb_database_name" {
  description = "MongoDB database name"
  type        = string
  default     = "agripeweb"
}

variable "api_image_tag" {
  description = "Docker image tag for the API (ECR image)"
  type        = string
  default     = "latest"
}

variable "ui_image_tag" {
  description = "Docker image tag for the UI (ECR image)"
  type        = string
  default     = "latest"
}

variable "mobile_ui_image_tag" {
  description = "Docker image tag for the mobile web UI (ECR image)"
  type        = string
  default     = "latest"
}

variable "google_client_id" {
  description = "Google OAuth 2.0 client ID. Pass via TF_VAR or terraform.tfvars; do not commit."
  type        = string
  sensitive   = true
}

variable "google_client_secret" {
  description = "Google OAuth 2.0 client secret. Pass via TF_VAR or terraform.tfvars; do not commit."
  type        = string
  sensitive   = true
}
