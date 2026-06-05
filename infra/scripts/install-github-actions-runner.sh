#!/usr/bin/env bash
# Install a GitHub Actions self-hosted runner on the AgripeWeb Hostinger VPS.
# Outbound-only — avoids inbound SSH from GitHub-hosted runner IP ranges.
#
# Usage (on VPS):
#   export RUNNER_REG_TOKEN='<from GitHub repo Settings → Actions → Runners → New>'
#   sudo -E ./infra/scripts/install-github-actions-runner.sh
#
# Rollback:
#   cd /opt/actions-runner-agripeweb && sudo ./svc.sh stop && sudo ./svc.sh uninstall
#   sudo rm -rf /opt/actions-runner-agripeweb

set -euo pipefail

REPO="${GITHUB_REPOSITORY:-cclautert/AgripeWeb}"
RUNNER_VERSION="${RUNNER_VERSION:-2.325.0}"
INSTALL_DIR="${RUNNER_INSTALL_DIR:-/opt/actions-runner-agripeweb}"
RUNNER_USER="${RUNNER_USER:-${SUDO_USER:-$(whoami)}}"
RUNNER_LABELS="${RUNNER_LABELS:-self-hosted,linux,agripeweb-prod}"
RUNNER_NAME="${RUNNER_NAME:-agripeweb-prod-vps}"

if [[ -z "${RUNNER_REG_TOKEN:-}" ]]; then
  echo "Set RUNNER_REG_TOKEN (registration token, expires in ~1h)." >&2
  echo "Generate: gh api -X POST repos/${REPO}/actions/runners/registration-token -q .token" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "docker not found — install Docker Engine before registering the runner." >&2
  exit 1
fi

if ! id "$RUNNER_USER" >/dev/null 2>&1; then
  echo "User ${RUNNER_USER} does not exist." >&2
  exit 1
fi

if ! groups "$RUNNER_USER" | grep -q docker; then
  echo "User ${RUNNER_USER} must be in the docker group." >&2
  exit 1
fi

ARCH="$(uname -m)"
case "$ARCH" in
  x86_64) RUNNER_ARCH=x64 ;;
  aarch64|arm64) RUNNER_ARCH=arm64 ;;
  *)
    echo "Unsupported architecture: $ARCH" >&2
    exit 1
    ;;
esac

TARBALL="actions-runner-linux-${RUNNER_ARCH}-${RUNNER_VERSION}.tar.gz"
URL="https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${TARBALL}"

echo "Installing GitHub Actions runner v${RUNNER_VERSION} (${RUNNER_ARCH}) for ${REPO}..."
mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

if [[ ! -f ./config.sh ]]; then
  curl -fsSL -o "$TARBALL" "$URL"
  tar xzf "$TARBALL"
  rm -f "$TARBALL"
fi

./config.sh --unattended \
  --url "https://github.com/${REPO}" \
  --token "$RUNNER_REG_TOKEN" \
  --name "$RUNNER_NAME" \
  --labels "$RUNNER_LABELS" \
  --work "_work" \
  --replace

./svc.sh install "$RUNNER_USER"
./svc.sh start

echo "Runner installed. Verify in GitHub → ${REPO} → Settings → Actions → Runners."
