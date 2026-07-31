Maietta Diagnostics — Linux x64 installation

Open a terminal in this extracted folder and run:

    bash install.sh

The installer places the self-contained app under ~/.local/opt, creates an
application-menu entry, and creates ~/.local/bin/4l60-diagnostics. It does not
install .NET system-wide and does not require root access.

To remove the app, run:

    bash ~/.local/opt/4l60-diagnostics/uninstall.sh
