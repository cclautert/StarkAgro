resource "aws_ecs_cluster" "main" {
  name = "agripeweb-${var.environment}"
}

# API task definition
resource "aws_ecs_task_definition" "api" {
  family                   = "agripeweb-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"

  execution_role_arn = aws_iam_role.ecs_execution.arn
  task_role_arn      = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name  = "api"
      image = "${aws_ecr_repository.api.repository_url}:${var.api_image_tag}"

      portMappings = [
        {
          containerPort = 8080
          protocol     = "tcp"
        }
      ]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_HTTP_PORTS", value = "8080" },
        { name = "ASPNETCORE_PATHBASE", value = "/api" },
        { name = "MongoDb__ConnectionString", value = var.mongodb_connection_string },
        { name = "MongoDb__DatabaseName", value = var.mongodb_database_name },
        # OAuth credentials — supply via TF_VAR_google_client_id / TF_VAR_google_client_secret
        { name = "OAuth__Google__ClientId",            value = var.google_client_id },
        { name = "OAuth__Google__ClientSecret",        value = var.google_client_secret },
        # OAuth: Angular UI web callback + native app deep-link
        { name = "OAuth__Google__AllowedRedirectUris", value = "https://agripeweb.com/login/callback,agripeweb://callback" }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])
}

# UI task definition
resource "aws_ecs_task_definition" "ui" {
  family                   = "agripeweb-ui"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"

  execution_role_arn = aws_iam_role.ecs_execution.arn
  task_role_arn      = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name  = "ui"
      image = "${aws_ecr_repository.ui.repository_url}:${var.ui_image_tag}"

      portMappings = [
        {
          containerPort = 80
          protocol     = "tcp"
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.ui.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])
}

# CloudWatch log groups
resource "aws_cloudwatch_log_group" "api" {
  name_prefix = "/ecs/agripeweb-api-"
  retention_in_days = 7
}

resource "aws_cloudwatch_log_group" "ui" {
  name_prefix = "/ecs/agripeweb-ui-"
  retention_in_days = 7
}

# ECS services (after ALB listener so target groups are attached)
resource "aws_ecs_service" "api" {
  name            = "agripeweb-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition  = aws_ecs_task_definition.api.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  depends_on = [aws_lb_listener.http]

  network_configuration {
    subnets          = data.aws_subnets.default.ids
    security_groups  = [aws_security_group.ecs_api.id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }
}

resource "aws_ecs_service" "ui" {
  name            = "agripeweb-ui"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.ui.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  depends_on = [aws_lb_listener.http]

  network_configuration {
    subnets          = data.aws_subnets.default.ids
    security_groups  = [aws_security_group.ecs_ui.id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.ui.arn
    container_name   = "ui"
    container_port   = 80
  }
}
