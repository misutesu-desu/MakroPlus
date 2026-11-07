# Security

Only the latest published release receives fixes. The app is unsigned. Obtain
release assets from this repository and compare the ZIP with its SHA-256 sidecar.

Prompt generation does not execute the plan. Review scripts, destinations and
steps before saving. Saved PowerShell/cmd actions run with your user privileges.
API mode sends prompts and registered app context to the endpoint you choose;
local mode uses a loopback server. API keys are DPAPI-encrypted for the Windows
user and are excluded from exported settings.

For a vulnerability, use the repository's private vulnerability reporting option
if available. Do not put credentials, private configuration or exploit details
that expose another person into a public issue. If private reporting is not
available, open a minimal issue requesting a private contact channel without
publishing the sensitive details.

Include the release version, affected feature, expected/actual result and a
minimal reproduction with synthetic files and placeholder credentials. Redact
local usernames, application data and server secrets from logs/screenshots.
