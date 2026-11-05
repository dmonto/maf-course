# Exercise 1: confirmed operating system

`ConfirmedCaseState` extends the baseline case with `ReportedOperatingSystem`, provenance and confirmation time. The root store preserves the runtime record type in `business_json`. `--confirm-os` is an explicit host-side confirmation fixture; arbitrary model-generated text never sets this field.

From the package root:

```powershell
$session = 'confirmed-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
& ./Scripts/Run-Example.ps1 -Example ex01 -Demo -NoBuild -Session $session -ConfirmOS 'Windows 11'
1..5 | ForEach-Object {
    & ./Scripts/Run-Example.ps1 -Example ex01 -Demo -NoBuild -Session $session `
        -InputText "Record check-in $_. Reply briefly."
}
& ./Scripts/Run-Example.ps1 -Example ex01 -Demo -NoBuild -Session $session `
    -InputText 'What is the recorded operating system for my case?'
```

Expect Windows 11 with `user-confirmed` provenance, retrieved by `GetCaseContext`, after restart and after old conversation turns were removed. The case stage remains `investigating`. The tool still checks the caller and permitted case. Without a confirmation, the field is null; the application does not infer it from conversation text.

Permitted fixture values are Windows 11, Windows 10, Linux and macOS. Use a fresh session ID when changing provider. Replace -Demo with -Live to inspect real-model behavior.
