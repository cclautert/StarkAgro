output "cluster_name" {
  description = "EKS cluster name"
  value       = aws_eks_cluster.main.name
}

output "cluster_endpoint" {
  description = "EKS cluster API endpoint"
  value       = aws_eks_cluster.main.endpoint
  sensitive   = true
}

output "cluster_certificate_authority_data" {
  description = "Base64-encoded certificate data for the cluster"
  value       = aws_eks_cluster.main.certificate_authority[0].data
  sensitive   = true
}

output "kubeconfig_command" {
  description = "Command to configure kubectl for this cluster"
  value       = "aws eks update-kubeconfig --region ${var.aws_region} --name ${aws_eks_cluster.main.name}"
}

output "ecr_api_repository_url" {
  description = "ECR repository URL for API image"
  value       = data.aws_ecr_repository.api.repository_url
}

output "ecr_ui_repository_url" {
  description = "ECR repository URL for UI image"
  value       = data.aws_ecr_repository.ui.repository_url
}

output "namespace" {
  description = "Kubernetes namespace for AgripeWeb workloads"
  value       = kubernetes_namespace.agripeweb.metadata[0].name
}

# ALB DNS is populated asynchronously by the AWS LB Controller. After apply, run:
# kubectl get ingress -n agripeweb agripeweb-ingress -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
output "alb_dns_instructions" {
  description = "Instructions to get ALB DNS name after controller provisions it"
  value       = "kubectl get ingress -n agripeweb agripeweb-ingress -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'"
}
