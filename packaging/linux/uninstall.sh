#!/usr/bin/env bash
set -euo pipefail

install_root="${HOME:?}/.local/opt/4l60-diagnostics"
expected_root="${HOME:?}/.local/opt/4l60-diagnostics"
launcher="${HOME:?}/.local/bin/4l60-diagnostics"
desktop_file="${HOME:?}/.local/share/applications/4l60-diagnostics.desktop"

if [[ "$install_root" != "$expected_root" || "$install_root" == "${HOME:?}" ]]; then
    echo 'Refusing to uninstall from an unexpected location.' >&2
    exit 1
fi

rm -f -- "$launcher" "$desktop_file"
rm -rf -- "$install_root"
echo 'Maietta Diagnostics was removed.'
