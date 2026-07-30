#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this installer as root (for example, with sudo)." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
rule_source="$script_dir/99-lt1diagnostics-ftdi.rules"
rule_target="/etc/udev/rules.d/99-lt1diagnostics-ftdi.rules"

if ! getent group lt1diag >/dev/null; then
  groupadd --system lt1diag
fi

install -o root -g root -m 0644 "$rule_source" "$rule_target"
udevadm control --reload-rules
udevadm trigger --subsystem-match=tty

echo "Installed $rule_target. Add the intended desktop user to group 'lt1diag', then sign out and back in."
echo "Example: sudo usermod -aG lt1diag USERNAME"

