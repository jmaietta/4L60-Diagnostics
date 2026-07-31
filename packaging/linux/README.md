# Linux runtime and serial permissions

Create the self-contained per-user installer archive with:

```powershell
powershell -File scripts/package-linux.ps1
```

The archive installs the app without root access. Cable-specific serial permissions are separate and are not changed automatically.

Avalonia 12 uses X11 by default. Debian/Ubuntu systems need `libx11-6`, `libice6`, `libsm6`, and `libfontconfig1` in addition to the published application.

The included udev rule grants `0660` access through a dedicated `lt1diag` group and `uaccess`; it never grants world read/write permissions. It matches only FTDI's documented default FT232R identity (`0403:6001`). A cable with a reprogrammed or different FTDI product ID requires an explicit, reviewed rule after confirming its identity with `lsusb`.

Install with:

```bash
sudo bash packaging/linux/install-udev-rule.sh
sudo usermod -aG lt1diag "$USER"
```

Sign out and back in before testing. Never run Maietta Diagnostics as root.

Source: FTDI FT232R datasheet, version 2.16, table 8.1: <https://ftdichip.com/wp-content/uploads/2020/08/DS_FT232R.pdf>.
