# Contributing

Use Windows 10/11 x64 with .NET Framework 4.8. Read the README and offline guide.

```powershell
.\setup-runtime.ps1
.\test.ps1 -OutputDirectory dist\contributor-check
```

The self-check opens controlled native test windows and sends input to those
fixtures. Run it on a desktop you can leave idle until it finishes. It does not
replace user settings. Inspect `test-results.txt`; report any SKIP alongside
PASS/FAIL results. Model tests are optional and require your own compatible
GGUF and, for app-routing tests, the relevant registered applications.

Keep changes focused and describe the user-visible behavior, validation and
limitations. Preserve the preview-before-save flow, input loop prevention,
cancellation, conflict checks and separation between activation scope and target.
Do not add model/provider allowlists or bundle model weights.

Never commit settings exports, API keys, personal logs, downloads, binaries or
screenshots containing private paths. Run `scripts/verify-publication.ps1` after
staging your intended files. See [SECURITY.md](SECURITY.md) for reporting issues.

Contributions are made under the project's GPL-3.0-only license. Keep independent
third-party notices and record any new dependency and its license.
