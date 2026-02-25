# AgripeWeb on AWS (Terraform)

This directory deploys the full AgripeWeb stack (API, UI, MongoDB via variable) on AWS using **ECS Fargate** and an **Application Load Balancer**. The root `main.tf` (Azure) is unchanged.

## Prerequisites

- **Terraform** >= 1.0
- **AWS CLI** (for credentials and optional image push)
- **Docker** (to build and push images to ECR)

## Required IAM permissions

The IAM user or role used for Terraform must be allowed to create and manage ECS, ECR, ELB, VPC/EC2 (security groups, describe VPC and subnets), IAM roles and policies, and CloudWatch Logs. If you see errors like `UnauthorizedOperation: ... is not authorized to perform: ec2:DescribeVpcs`, attach a policy that allows at least:

- **EC2:** `DescribeVpcs`, `DescribeVpcAttribute`, `DescribeSubnets`, `DescribeSecurityGroups`, `CreateSecurityGroup`, `DeleteSecurityGroup`, `AuthorizeSecurityGroupIngress`, `AuthorizeSecurityGroupEgress`, `RevokeSecurityGroupIngress`, `RevokeSecurityGroupEgress`, `CreateTags`, `DescribeNetworkInterfaces`
- **ECS:** full access for cluster, service, task definition (e.g. `ecs:*`) or the minimum create/read/update/delete actions
- **ECR:** `CreateRepository`, `DescribeRepositories`, `GetAuthorizationToken` (for docker push)
- **Elastic Load Balancing:** `CreateLoadBalancer`, `CreateTargetGroup`, `CreateListener`, `Describe*`, `AddTags`, `Modify*`, `Delete*`
- **IAM:** `CreateRole`, `DeleteRole`, `GetRole`, `PassRole`, `PutRolePolicy`, `DeleteRolePolicy`, `AttachRolePolicy`, `DetachRolePolicy`
- **CloudWatch Logs:** `CreateLogGroup`, `DescribeLogGroups`, `DeleteLogGroup`, `PutRetentionPolicy`

You can use the AWS managed policies `AmazonEC2FullAccess`, `AmazonECS_FullAccess`, `AmazonEC2ContainerRegistryFullAccess`, `ElasticLoadBalancingFullAccess`, `IAMFullAccess`, and `CloudWatchLogsFullAccess` for a dev account, or create a custom policy with the minimum actions above. A ready-to-use custom policy is in **`iam-policy-terraform.json`** in this directory: in IAM create a new policy (JSON), paste its contents, then attach the policy to the user or role you use for Terraform.

## Authentication

Configure AWS credentials (do not commit keys):

- **Environment variables:** `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and optionally `AWS_REGION`
- **Named profile:** `aws configure --profile yourprofile` then set variable `aws_profile = "yourprofile"`
- **IAM role:** e.g. in CI or when running on an EC2/ECS with an instance/task role

## Backend (state)

State is stored in **S3** with **DynamoDB** for locking. Create the bucket and table once (same region as `aws_region`):

```bash
# Replace BUCKET_NAME and TABLE_NAME with your choice (e.g. agripeweb-tfstate, agripeweb-tfstate-lock)
aws s3 mb s3://BUCKET_NAME --region us-east-1
aws dynamodb create-table \
  --table-name TABLE_NAME \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1
```

Then initialize Terraform with the backend config:

```bash
cd terraform/aws
terraform init \
  -backend-config="bucket=BUCKET_NAME" \
  -backend-config="dynamodb_table=TABLE_NAME" \
  -backend-config="region=us-east-1"
```

## Variables

Required:

- **mongodb_connection_string** – MongoDB URI (e.g. MongoDB Atlas). Pass via `TF_VAR_mongodb_connection_string` or a non-committed `terraform.tfvars`.

Optional (see `variables.tf`):

- `aws_region` (default `us-east-1`)
- `aws_profile` (default `null`)
- `environment` (default `dev`)
- `mongodb_database_name` (default `agripeweb`)
- `api_image_tag`, `ui_image_tag` (default `latest`)

Example (do not commit real values):

- Copy `terraform.tfvars.example` to `terraform.tfvars` and set `mongodb_connection_string` and other values.

## Apply

```bash
cd terraform/aws
terraform init   # with -backend-config as above on first run
terraform plan   # ensure mongodb_connection_string is set (e.g. TF_VAR_mongodb_connection_string="...")
terraform apply
```

After apply, note the outputs: `app_url`, `ecr_api_url`, `ecr_ui_url`.

### Troubleshooting: 403 UnauthorizedOperation (ec2:DescribeVpcs)

If `terraform plan` or `terraform apply` fails with **You are not authorized to perform: ec2:DescribeVpcs**, the IAM user or role you use does not have the required permissions. In the AWS IAM console, create a new policy (paste the contents of **`iam-policy-terraform.json`** from this directory), then attach that policy to your user (e.g. `agripeweb`). After that, rerun `terraform plan` and `terraform apply`.

### Region sa-east-1 (São Paulo): ALB needs 2 subnets in 2 AZs

The default VPC in **sa-east-1** often has only one subnet. The Application Load Balancer requires at least two subnets in two Availability Zones. Two options:

1. **Recommended:** Ensure the IAM user/role has **`ec2:CreateSubnet`** (it is included in **`iam-policy-terraform.json`**). Update the policy in the IAM console if needed, then run `terraform apply` again. Terraform will create a second subnet in another AZ automatically.
2. **Alternative:** Create a second subnet manually in the default VPC (e.g. in **sa-east-1b**, CIDR `172.31.16.0/20`). Then in `terraform.tfvars` set:
   ```hcl
   alb_subnet_ids = ["subnet-EXISTING", "subnet-NEW"]
   ```

## Build and push Docker images

The API and UI run from ECR. Build and push from the repo root (replace REGION and ACCOUNT_ID):

```bash
aws ecr get-login-password --region REGION | docker login --username AWS --password-stdin ACCOUNT_ID.dkr.ecr.REGION.amazonaws.com

docker build -t agripeweb-api:latest -f AgripeWebAPI/Dockerfile AgripeWebAPI
docker tag agripeweb-api:latest ECR_API_URL:latest
docker push ECR_API_URL:latest

docker build -t agripeweb-ui:latest -f AgripeWebUI/Dockerfile AgripeWebUI
docker tag agripeweb-ui:latest ECR_UI_URL:latest
docker push ECR_UI_URL:latest
```

Use `terraform -chdir=terraform/aws output -raw ecr_api_url` and `ecr_ui_url` for ECR_API_URL and ECR_UI_URL. After pushing, ECS will pull the new images on the next deployment (or force a new deployment in the ECS console).

## API path base (ALB)

The ALB forwards requests with path prefix `/api` to the API. The API receives the full path (e.g. `/api/v1/Auth/...`). Ensure the API is configured with path base `/api` when running behind this ALB (e.g. in ASP.NET Core: `UsePathBase("/api")` or equivalent) so that routes like `v1/Auth` are served at `/api/v1/Auth`.

## Optional: DocumentDB

To use AWS DocumentDB instead of MongoDB Atlas, add a DocumentDB cluster and instance in Terraform, place them in private subnets, and set `mongodb_connection_string` from the DocumentDB endpoint. The rest of the stack (ECS, ALB) stays the same.
