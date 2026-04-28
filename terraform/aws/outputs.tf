output "app_url" {
  description = "Application URL (ALB). Use http://<this> until HTTPS is configured."
  value       = "http://${aws_lb.main.dns_name}"
}

output "alb_dns_name" {
  description = "ALB DNS name"
  value       = aws_lb.main.dns_name
}

output "ecr_api_url" {
  description = "ECR repository URL for API image (use for docker push)"
  value       = aws_ecr_repository.api.repository_url
}

output "ecr_ui_url" {
  description = "ECR repository URL for UI image (use for docker push)"
  value       = aws_ecr_repository.ui.repository_url
}

output "ecr_mobile_ui_url" {
  description = "ECR repository URL for mobile web UI image (use for docker push)"
  value       = aws_ecr_repository.mobile_ui.repository_url
}
