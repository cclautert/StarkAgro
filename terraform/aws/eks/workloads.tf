# Namespace for AgripeWeb workloads
resource "kubernetes_namespace" "agripeweb" {
  metadata {
    name = "agripeweb"
  }
}

# API Deployment
resource "kubernetes_deployment" "api" {
  metadata {
    name      = "agripeweb-api"
    namespace = kubernetes_namespace.agripeweb.metadata[0].name
    labels = {
      app = "agripeweb-api"
    }
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "agripeweb-api"
      }
    }

    template {
      metadata {
        labels = {
          app = "agripeweb-api"
        }
      }

      spec {
        container {
          name  = "api"
          image = "${data.aws_ecr_repository.api.repository_url}:${var.api_image_tag}"

          port {
            container_port = 8080
          }

          env {
            name  = "ASPNETCORE_ENVIRONMENT"
            value = "Production"
          }
          env {
            name  = "ASPNETCORE_HTTP_PORTS"
            value = "8080"
          }
          env {
            name  = "ASPNETCORE_PATHBASE"
            value = "/api"
          }
          env {
            name  = "MongoDb__ConnectionString"
            value = var.mongodb_connection_string
          }
          env {
            name  = "MongoDb__DatabaseName"
            value = var.mongodb_database_name
          }
          env {
            name  = "OAuth__Google__AllowedRedirectUris"
            value = var.app_base_url != "" ? "${var.app_base_url}/login/callback" : "http://localhost/login/callback"
          }

          liveness_probe {
            http_get {
              path = "/api/v1/health"
              port = 8080
            }
            initial_delay_seconds = 30
            period_seconds        = 10
            timeout_seconds       = 5
            failure_threshold     = 3
          }
          readiness_probe {
            http_get {
              path = "/api/v1/health"
              port = 8080
            }
            initial_delay_seconds = 10
            period_seconds       = 5
            timeout_seconds      = 3
            failure_threshold    = 2
          }

          resources {
            requests = {
              cpu    = "100m"
              memory = "256Mi"
            }
            limits = {
              cpu    = "500m"
              memory = "512Mi"
            }
          }
        }
      }
    }
  }

  depends_on = [helm_release.aws_load_balancer_controller]
}

# UI Deployment
resource "kubernetes_deployment" "ui" {
  metadata {
    name      = "agripeweb-ui"
    namespace = kubernetes_namespace.agripeweb.metadata[0].name
    labels = {
      app = "agripeweb-ui"
    }
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "agripeweb-ui"
      }
    }

    template {
      metadata {
        labels = {
          app = "agripeweb-ui"
        }
      }

      spec {
        container {
          name  = "ui"
          image = "${data.aws_ecr_repository.ui.repository_url}:${var.ui_image_tag}"

          port {
            container_port = 80
          }

          liveness_probe {
            http_get {
              path = "/"
              port = 80
            }
            initial_delay_seconds = 15
            period_seconds        = 10
            timeout_seconds       = 5
            failure_threshold     = 3
          }
          readiness_probe {
            http_get {
              path = "/"
              port = 80
            }
            initial_delay_seconds = 5
            period_seconds       = 5
            timeout_seconds      = 3
            failure_threshold    = 2
          }

          resources {
            requests = {
              cpu    = "50m"
              memory = "128Mi"
            }
            limits = {
              cpu    = "200m"
              memory = "256Mi"
            }
          }
        }
      }
    }
  }

  depends_on = [helm_release.aws_load_balancer_controller]
}

# API Service
resource "kubernetes_service" "api" {
  metadata {
    name      = "agripeweb-api"
    namespace = kubernetes_namespace.agripeweb.metadata[0].name
    labels = {
      app = "agripeweb-api"
    }
    annotations = {
      "alb.ingress.kubernetes.io/healthcheck-path" = "/api/v1/health"
    }
  }

  spec {
    selector = {
      app = "agripeweb-api"
    }
    port {
      port        = 8080
      target_port = 8080
      protocol    = "TCP"
    }
    type = "ClusterIP"
  }
}

# UI Service
resource "kubernetes_service" "ui" {
  metadata {
    name      = "agripeweb-ui"
    namespace = kubernetes_namespace.agripeweb.metadata[0].name
    labels = {
      app = "agripeweb-ui"
    }
  }

  spec {
    selector = {
      app = "agripeweb-ui"
    }
    port {
      port        = 80
      target_port = 80
      protocol    = "TCP"
    }
    type = "ClusterIP"
  }
}

# Ingress - ALB routes /api to API, / to UI
resource "kubernetes_ingress_v1" "main" {
  metadata {
    name      = "agripeweb-ingress"
    namespace = kubernetes_namespace.agripeweb.metadata[0].name
    annotations = {
      "alb.ingress.kubernetes.io/scheme"       = "internet-facing"
      "alb.ingress.kubernetes.io/target-type"  = "ip"
      "alb.ingress.kubernetes.io/healthcheck-path"             = "/"
      "alb.ingress.kubernetes.io/healthcheck-interval-seconds" = "15"
      "alb.ingress.kubernetes.io/healthcheck-timeout-seconds"   = "5"
      "alb.ingress.kubernetes.io/healthy-threshold-count"       = "2"
      "alb.ingress.kubernetes.io/unhealthy-threshold-count"    = "3"
    }
  }

  spec {
    ingress_class_name = "alb"

    rule {
      http {
        # /api and /api/* -> API service (evaluated first)
        path {
          path      = "/api"
          path_type = "Prefix"
          backend {
            service {
              name = kubernetes_service.api.metadata[0].name
              port {
                number = 8080
              }
            }
          }
        }
        # Default / -> UI service
        path {
          path      = "/"
          path_type = "Prefix"
          backend {
            service {
              name = kubernetes_service.ui.metadata[0].name
              port {
                number = 80
              }
            }
          }
        }
      }
    }
  }

  depends_on = [
    kubernetes_service.api,
    kubernetes_service.ui,
    helm_release.aws_load_balancer_controller
  ]
}
