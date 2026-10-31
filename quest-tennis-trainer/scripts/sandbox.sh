#!/usr/bin/env bash
# ==============================================================================
# Quest 3 Tennis Trainer — Local Hardened Sandbox & ADB Sideload CLI
# Usage:
#   ./quest-tennis-trainer/scripts/sandbox.sh test
#   ./quest-tennis-trainer/scripts/sandbox.sh quest-status
#   ./quest-tennis-trainer/scripts/sandbox.sh sideload [path/to/QuestTennisTrainer.apk]
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
IMAGE_NAME="quest-tennis-sandbox:latest"
DEFAULT_APK="${PROJECT_DIR}/build/Android/QuestTennisTrainer.apk"

cmd_test() {
  echo "==> Building hardened distroless physics sandbox (${IMAGE_NAME})..."
  docker build -q -t "${IMAGE_NAME}" "${PROJECT_DIR}"

  echo "==> Running NUnit 120Hz Physics & Training Drill Suite (non-root, read-only, cap-drop=ALL)..."
  docker run --rm \
    --read-only \
    --tmpfs /tmp:rw,noexec,nosuid,size=16m \
    --cap-drop=ALL \
    --security-opt=no-new-privileges \
    "${IMAGE_NAME}"
}

cmd_quest_status() {
  echo "==> Checking ADB & Meta Quest 3 Developer Mode connection..."
  if ! command -v adb >/dev/null 2>&1; then
    echo "WARN: 'adb' not found on PATH."
    echo "Install via Homebrew: brew install --cask android-platform-tools"
    exit 1
  fi

  adb devices -l
  echo ""
  echo "Tip: If your Quest 3 shows 'unauthorized', put on the headset and click 'Always allow from this computer'."
}

cmd_sideload() {
  local apk_path="${1:-${DEFAULT_APK}}"
  if ! command -v adb >/dev/null 2>&1; then
    echo "ERROR: 'adb' not found. Install via: brew install --cask android-platform-tools"
    exit 1
  fi

  if [[ ! -f "${apk_path}" ]]; then
    echo "ERROR: APK file not found at: ${apk_path}"
    echo "Build the APK in Unity 6 (Android / ARM64) or pass the APK path explicitly."
    exit 1
  fi

  echo "==> Locking Meta Quest 3 display refresh rate to 120 Hz..."
  adb shell setprop debug.oculus.refreshRate 120 || true

  echo "==> Installing ${apk_path} onto Meta Quest 3..."
  adb install -r "${apk_path}"
  echo "==> Sideload complete!"
}

ACTION="${1:-test}"
case "${ACTION}" in
  test)
    cmd_test
    ;;
  quest-status)
    cmd_quest_status
    ;;
  sideload)
    cmd_sideload "${2:-}"
    ;;
  *)
    echo "Usage: $0 {test|quest-status|sideload [apk_path]}"
    exit 1
    ;;
esac

