# Windows packaging

Create a self-contained x64 publish:

```powershell
dotnet publish .\src\LT1Diagnostics.App\LT1Diagnostics.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\win-x64
```

Create the user-installable ZIP with `powershell -File scripts/package-windows.ps1`.

Driver installation and hardware identification remain operator-controlled. The application does not install or replace FTDI drivers.
