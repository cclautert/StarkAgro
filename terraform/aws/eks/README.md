# AgripeWeb on EKS

Terraform configuration to deploy AgripeWeb (API + UI) on Amazon EKS with EC2 worker nodes and an Application Load Balancer.

## Prerequisites

- [terraform/aws](../) applied first (ECR repositories must exist)
- AWS CLI configured with credentials that have EKS permissions
- `kubectl` installed

## IAM Permissions

The Terraform user/role needs EKS permissions. Add to `terraform/aws/iam-policy-terraform.json` if missing:

- `eks:*` (or granular: `eks:CreateCluster`, `eks:DeleteCluster`, `eks:DescribeCluster`, etc.)
- `ec2:CreateNetworkInterface`, `ec2:DescribeNetworkInterfaces` (VPC CNI)
- `ec2:CreateTags` for subnets (if not already allowed)

## Usage

1. Copy and configure variables:
   ```bash
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with mongodb_connection_string, region, etc.
   ```

2. Initialize and apply:
   ```bash
   terraform init
   terraform plan -var="mongodb_connection_string=YOUR_MONGODB_URI"
   terraform apply -var="mongodb_connection_string=YOUR_MONGODB_URI"
   ```

3. Configure kubectl:
   ```bash
   aws eks update-kubeconfig --region <region> --name agripeweb-eks-dev
   ```

4. Get ALB DNS (after controller provisions it, ~2–5 min):
   ```bash
   kubectl get ingress -n agripeweb agripeweb-ingress -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
   ```

5. Set `app_base_url` for OAuth and re-apply or restart API pods:
   ```bash
   kubectl rollout restart deployment/agripeweb-api -n agripeweb
   ```

## Architecture

- **EKS cluster** with Kubernetes 1.31
- **EC2 managed node group** (t3.medium, scalable)
- **AWS Load Balancer Controller** (Helm) for ALB ingress
- **Ingress**: `/api` -> API service:8080, `/` -> UI service:80
- Reuses ECR repos `agripeweb-api` and `agripeweb-ui` from parent terraform/aws
