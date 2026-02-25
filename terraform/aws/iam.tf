# Task execution role: used by ECS to pull images and write logs
resource "aws_iam_role" "ecs_execution" {
  name_prefix = "agripeweb-ecs-exec-"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_execution" {
  role       = aws_iam_role.ecs_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Task role: optional, for task-to-AWS access (e.g. S3, Secrets Manager) later
resource "aws_iam_role" "ecs_task" {
  name_prefix = "agripeweb-ecs-task-"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      }
    ]
  })
}

# Optional: attach inline or managed policies to ecs_task for S3/Secrets Manager etc.
# For now the task role has no extra permissions.
