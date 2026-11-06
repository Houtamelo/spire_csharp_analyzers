# Customer package acceptance

Run the actual Package Manager installation, sample import, serialized-value
checks, offline documentation checks, and Showcase tests in an owned project:

```powershell
dotnet run -c Release --project tools/Spire.Tool -- unity-customer --project tmp/customer-acceptance --package C:\Packages\com.houtamelo.spire
```

The Editor must be licensed. Use `--editor` for a non-default installation.
Reports and Unity logs are written in the disposable project. A missing report,
failed check, or nonzero Unity exit fails acceptance.

Project settings come from `tests/UnityPackageSample`, and upgrade preparation
copies consumer assets from the baseline package's own `Samples~/Showcase`.
The workflow does not access the interactive projects under `examples/`.
Package/baseline inputs and validation outputs inside that directory are rejected.

The polling regression uses a separate clean project without an imported sample:

```powershell
dotnet run -c Release --project tools/Spire.Tool -- unity-customer --prepare-only --project tmp/customer-polling --package C:\Packages\com.houtamelo.spire
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode -nographics -projectPath "$PWD\tmp\customer-polling" -executeMethod Spire.UnityCustomerAcceptance.CustomerAcceptance.VerifyPollingTimeout -logFile "$PWD\tmp\customer-polling\polling.log"
```

This regression deliberately exits with code 1. Its `customer-acceptance.json`
must contain `PASS More than 6000 Editor updates do not exhaust the time budget.`
and a failure beginning `Timed out while waiting for sample-import.` It first
executes 6,002 real polling callbacks, then expires the persisted deadline and
checks rejection. Callback frequency must not substitute for elapsed time.
