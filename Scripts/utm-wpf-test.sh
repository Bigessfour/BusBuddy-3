#!/bin/zsh
# utm-wpf-test.sh
# Compatibility shim → utm-dev-bridge.sh test
#
#   ./Scripts/utm-wpf-test.sh
#   ./Scripts/utm-wpf-test.sh --deps-only
#   ./Scripts/utm-wpf-test.sh --no-sync --filter "FullyQualifiedName~DestinationServiceTests"
#   ./Scripts/utm-wpf-test.sh --full

emulate -L zsh
set -u
setopt pipefail

typeset -r SCRIPT_DIR="${0:A:h}"
exec "${SCRIPT_DIR}/utm-dev-bridge.sh" test "$@"
