# Recommended model

**Qwen3.5-4B — Q4_0 GGUF** is our recommended starting point for Makro+ prompt
planning. This is the model file used in the project's development tests.
It is an optional recommendation: you can select another supported GGUF or a
compatible API model in the app.

| Item | Value |
| --- | --- |
| Model family | Qwen3.5-4B |
| GGUF publisher | Unsloth |
| File | `Qwen3.5-4B-Q4_0.gguf` |
| Download size | 2,583,221,408 bytes, approximately 2.58 GB |
| Model license | Apache-2.0, separate from Makro+'s GPL-3.0-only license |
| Included in Makro+ downloads | No |

**[Download the tested GGUF file](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/resolve/e87f176479d0855a907a41277aca2f8ee7a09523/Qwen3.5-4B-Q4_0.gguf?download=true)**

[Model card and other quantizations](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF)
· [Exact file details](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/blob/e87f176479d0855a907a41277aca2f8ee7a09523/Qwen3.5-4B-Q4_0.gguf)

## Set it up

1. Extract the complete Makro+ Windows ZIP, or install the application.
2. Open **AI model → Configure model → Open models folder**.
3. Copy `Qwen3.5-4B-Q4_0.gguf` into that folder next to `MakroPlus.exe`.
4. Select **Local GGUF file** and click **Refresh models folder**. A single GGUF
   is found automatically when no model was selected. If there are several
   models or an old selection, use **Browse GGUF…** to choose this file.
5. Keep **Fast planning** and **Request structured JSON output** enabled as a
   starting configuration, then save model settings.
6. Create a prompt shortcut, generate its preview and inspect the steps before
   saving. The first request loads the model; later requests reuse the session.

For example:

```text
When I press F8, copy the selected text.
```

Makro+ uses text prompts; a separate vision projector is not needed for this
tested setup. The model is not downloaded automatically or fixed as an app
default. A compatible API remains an alternative to local inference.

## Verify the download

The hosted file's SHA-256 matches the actual GGUF used in development tests:

```text
298fcb5fe7a77ccc79745ae24751560c5ac56874caff4bb39b1f2055bd72b8bb
```

On Windows, run this from the folder containing the downloaded file:

```powershell
Get-FileHash -LiteralPath .\Qwen3.5-4B-Q4_0.gguf -Algorithm SHA256
```

The download link is pinned to model-repository revision
`e87f176479d0855a907a41277aca2f8ee7a09523` so it identifies the verified file.

## What to expect

This recommendation reflects the model tested with this app, rather than a
benchmark claiming it is the best model for every task. Model quality, planning
time and memory use depend on the prompt and hardware. RAM/VRAM requirements
include runtime and context overhead in addition to the 2.58 GB file.

Saved shortcuts execute their saved plans without another model request.
Review generated scripts and action steps, and keep explicit waits where a
target application needs time to respond. See the offline `USER_GUIDE.html` in
the Windows ZIP for the full workflow and troubleshooting.
