data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

data "aws_subnet" "default_each" {
  for_each = toset(data.aws_subnets.default.ids)
  id       = each.value
}

data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_ecr_repository" "api" {
  name = "agripeweb-api"
}

data "aws_ecr_repository" "ui" {
  name = "agripeweb-ui"
}

data "aws_caller_identity" "current" {}

locals {
  cluster_name   = "agripeweb-eks-${var.environment}"
  subnet_per_az  = { for s in data.aws_subnet.default_each : s.availability_zone => s.id }
  first_az       = length(local.subnet_per_az) > 0 ? keys(local.subnet_per_az)[0] : data.aws_availability_zones.available.names[0]
  second_az      = [for az in data.aws_availability_zones.available.names : az if az != local.first_az][0]
  need_extra_az  = length(local.subnet_per_az) < 2
}

# Extra subnet in second AZ when default VPC has only one (e.g. sa-east-1); EKS requires 2+ AZs
resource "aws_subnet" "eks_second_az" {
  count                   = local.need_extra_az ? 1 : 0
  vpc_id                  = data.aws_vpc.default.id
  availability_zone       = local.second_az
  cidr_block              = cidrsubnet(data.aws_vpc.default.cidr_block, 4, 2)
  map_public_ip_on_launch = true
}

locals {
  subnet_ids = local.need_extra_az ? concat(values(local.subnet_per_az), [aws_subnet.eks_second_az[0].id]) : slice(values(local.subnet_per_az), 0, 2)
}

# Required for EKS to use subnets
resource "aws_ec2_tag" "subnet_cluster" {
  for_each    = toset(local.subnet_ids)
  resource_id = each.value
  key         = "kubernetes.io/cluster/${local.cluster_name}"
  value       = "shared"
}

# Required for AWS Load Balancer Controller to create ALB in these subnets
resource "aws_ec2_tag" "subnet_elb" {
  for_each    = toset(local.subnet_ids)
  resource_id = each.value
  key         = "kubernetes.io/role/elb"
  value       = "1"
}
