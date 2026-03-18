variable "aws_region" {
  description = "AWS region for resources"
  type        = string
  default     = "us-east-1"
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

variable "node_instance_types" {
  description = "EC2 instance types for EKS node group (t2.micro = Free Tier eligible)"
  type        = list(string)
  default     = ["t2.micro"]
}

variable "node_desired_size" {
  description = "Desired number of nodes in the EKS node group (1 = within Free Tier 750h/month)"
  type        = number
  default     = 1
}

variable "node_min_size" {
  description = "Minimum number of nodes in the EKS node group"
  type        = number
  default     = 1
}

variable "node_max_size" {
  description = "Maximum number of nodes in the EKS node group"
  type        = number
  default     = 2
}

variable "app_base_url" {
  description = "Base URL for the app (e.g. http://k8s-agripe-xxx.elb.amazonaws.com). Set after first apply when ALB is provisioned; used for OAuth redirect."
  type        = string
  default     = ""
}
