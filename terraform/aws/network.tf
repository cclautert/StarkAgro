# Default VPC and subnets (no new VPC created)
data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

# One subnet per AZ (ALB requires at least 2 subnets in 2 AZs)
data "aws_subnet" "default_each" {
  for_each = toset(data.aws_subnets.default.ids)
  id       = each.value
}

data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  subnet_id_per_az = { for s in data.aws_subnet.default_each : s.availability_zone => s.id }
  first_az         = length(local.subnet_id_per_az) > 0 ? keys(local.subnet_id_per_az)[0] : data.aws_availability_zones.available.names[0]
  second_az        = [for az in data.aws_availability_zones.available.names : az if az != local.first_az][0]
  need_extra       = length(local.subnet_id_per_az) < 2
}

# Extra subnet in a second AZ when default VPC has only one (required for ALB in e.g. sa-east-1). IAM needs ec2:CreateSubnet. Skip if alb_subnet_ids is set.
resource "aws_subnet" "alb_second_az" {
  count             = (var.alb_subnet_ids == null && local.need_extra) ? 1 : 0
  vpc_id            = data.aws_vpc.default.id
  availability_zone = local.second_az
  cidr_block        = cidrsubnet(data.aws_vpc.default.cidr_block, 4, 1)
}

locals {
  alb_subnet_ids = var.alb_subnet_ids != null ? var.alb_subnet_ids : (local.need_extra ? concat(values(local.subnet_id_per_az), [aws_subnet.alb_second_az[0].id]) : slice(values(local.subnet_id_per_az), 0, 2))
}

# ALB security group: allow 80 (and 443 if you add HTTPS later)
resource "aws_security_group" "alb" {
  name_prefix = "agripeweb-alb-"
  description = "ALB for AgripeWeb"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
    description = "HTTP"
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  lifecycle {
    create_before_destroy = true
  }
}

# ECS API: allow traffic only from ALB on 8080
resource "aws_security_group" "ecs_api" {
  name_prefix = "agripeweb-ecs-api-"
  description = "ECS API tasks"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
    description     = "From ALB"
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  lifecycle {
    create_before_destroy = true
  }
}

# ECS UI: allow traffic only from ALB on 80
resource "aws_security_group" "ecs_ui" {
  name_prefix = "agripeweb-ecs-ui-"
  description = "ECS UI tasks"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port       = 80
    to_port         = 80
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
    description     = "From ALB"
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  lifecycle {
    create_before_destroy = true
  }
}
