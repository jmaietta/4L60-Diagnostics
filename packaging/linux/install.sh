#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source_dir="$script_dir/app"
install_root="${HOME:?}/.local/opt/4l60-diagnostics"
bin_dir="${HOME:?}/.local/bin"
applications_dir="${HOME:?}/.local/share/applications"
executable="$install_root/4L60-Diagnostics"

if [[ ! -f "$source_dir/4L60-Diagnostics" ]]; then
    echo 'The application files are missing. Extract the complete archive before running install.sh.' >&2
    exit 1
fi

mkdir -p "$install_root" "$bin_dir" "$applications_dir"
cp -R "$source_dir"/. "$install_root"/
cp "$script_dir/uninstall.sh" "$install_root/uninstall.sh"
chmod +x "$executable"
chmod +x "$install_root/uninstall.sh"
ln -sfn "$executable" "$bin_dir/4l60-diagnostics"

desktop_file="$applications_dir/4l60-diagnostics.desktop"
printf '%s\n' \
    '[Desktop Entry]' \
    'Type=Application' \
    'Name=4L60 Diagnostics' \
    'Comment=4L60E diagnostics for the 1994 Buick Roadmaster' \
    "Exec=$executable" \
    'Icon=applications-system' \
    'Terminal=false' \
    'Categories=Utility;Automotive;' > "$desktop_file"
chmod 0644 "$desktop_file"

echo '4L60 Diagnostics is installed.'
echo 'Open it from your application menu or run: 4l60-diagnostics'
