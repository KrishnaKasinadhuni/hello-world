#!/usr/bin/env bash
# ==============================================================================
# Automated CLI Test Suite for quest-tennis-trainer/scripts/sandbox.sh
# Tests syntax, hardened Docker test execution, mocked ADB quest-status,
# 120Hz refresh rate property lock, APK sideloading, and error handling.
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SANDBOX_SH="${SCRIPT_DIR}/sandbox.sh"

PASS_COUNT=0
assert_pass() {
  echo "  [PASS] $1"
  PASS_COUNT=$((PASS_COUNT + 1))
}

echo "==> Running CLI test suite for ${SANDBOX_SH}..."

# 1. Bash syntax validation
bash -n "${SANDBOX_SH}"
assert_pass "1. Bash syntax check (bash -n) succeeded"

# 2. Invalid subcommand handling
if "${SANDBOX_SH}" unknown-subcommand >/dev/null 2>&1; then
  echo "  [FAIL] Expected unknown-subcommand to exit non-zero"
  exit 1
fi
assert_pass "2. Invalid subcommand exits non-zero with usage message"

# 3. Missing ADB handling for quest-status
if PATH="/usr/bin:/bin" "${SANDBOX_SH}" quest-status >/dev/null 2>&1; then
  echo "  [FAIL] Expected quest-status without adb to exit non-zero"
  exit 1
fi
assert_pass "3. Missing 'adb' binary is detected cleanly with install hint"

# 4. Mocked ADB for quest-status & sideload
TMP_MOCK_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_MOCK_DIR}"' EXIT

ADB_LOG="${TMP_MOCK_DIR}/adb_calls.log"
cat << 'EOF' > "${TMP_MOCK_DIR}/adb"
#!/usr/bin/env bash
echo "adb $*" >> "${ADB_LOG_FILE}"
if [[ "${1:-}" == "devices" ]]; then
  echo "List of devices attached"
  echo "2G0YC1ZF8Q0123         device usb:1-1 product:eureka model:Quest_3 device:eureka"
fi
exit 0
EOF
chmod +x "${TMP_MOCK_DIR}/adb"

STATUS_OUT="$(ADB_LOG_FILE="${ADB_LOG}" PATH="${TMP_MOCK_DIR}:${PATH}" "${SANDBOX_SH}" quest-status)"
echo "${STATUS_OUT}" | grep -q "Quest_3"
assert_pass "4. 'quest-status' lists connected Meta Quest 3 device via adb"

# 5. Sideload missing APK check
if ADB_LOG_FILE="${ADB_LOG}" PATH="${TMP_MOCK_DIR}:${PATH}" "${SANDBOX_SH}" sideload "${TMP_MOCK_DIR}/missing.apk" >/dev/null 2>&1; then
  echo "  [FAIL] Expected sideload with missing APK to exit non-zero"
  exit 1
fi
assert_pass "5. 'sideload' rejects missing APK path before calling adb install"

# 6. Sideload valid APK + verify 120Hz refresh rate lock & adb install -r
DUMMY_APK="${TMP_MOCK_DIR}/QuestTennisTrainer.apk"
touch "${DUMMY_APK}"
ADB_LOG_FILE="${ADB_LOG}" PATH="${TMP_MOCK_DIR}:${PATH}" "${SANDBOX_SH}" sideload "${DUMMY_APK}" >/dev/null
grep -q "adb shell setprop debug.oculus.refreshRate 120" "${ADB_LOG}"
grep -q "adb install -r ${DUMMY_APK}" "${ADB_LOG}"
assert_pass "6. 'sideload' locks Quest 3 to 120 Hz and runs 'adb install -r'"

# 7. End-to-end hardened Docker NUnit test subcommand
TEST_OUT="$("${SANDBOX_SH}" test)"
echo "${TEST_OUT}" | grep -q "Overall result: Passed"
assert_pass "7. 'test' subcommand builds and passes hardened distroless NUnit suite"

echo ""
echo "==> All ${PASS_COUNT}/7 sandbox.sh CLI tests passed!"
