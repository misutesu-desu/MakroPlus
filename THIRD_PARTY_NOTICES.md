# Third-party notices

Makro+ source code and original project assets are licensed under
**GPL-3.0-only**. See [LICENSE](LICENSE). This does not relicense independent
third-party software or user-supplied models.

## llama.cpp and GGML

The Windows distribution uses the independent llama.cpp server process and its
libraries from the upstream **b12132 Vulkan x64** release. The source repository
does not contain those binaries. `setup-runtime.ps1` downloads the pinned archive
and checks its SHA-256 before extraction.

- Project and corresponding upstream source: <https://github.com/ggml-org/llama.cpp/tree/b12132>
- License: MIT, copyright 2023–2026 The ggml authors.
- Full license accompanies binary distributions as `runtime/llama/LICENSE-llama.cpp.txt`.

## LLVM OpenMP

The upstream Windows runtime contains `libomp.dll`, distributed under the
Apache License 2.0 with LLVM exceptions. The full upstream notice is retained
as `runtime/llama/LICENSE-LLVM-OpenMP` in distributions.

- Project: <https://llvm.org/>
- License: <https://llvm.org/LICENSE.txt>

## Microsoft Windows components

Makro+ targets .NET Framework 4.8 and uses Windows APIs. These are separately
licensed Microsoft platform components, not part of the GPL-licensed app source.
Some Windows packages contain Microsoft's signed Visual C++ x64 redistributable
installer in `prerequisites/`. That installer remains subject to Microsoft's own
terms; it is not licensed under GPL. The app installer checks its signature
before running it when the dependency is missing.

- Official prerequisite information: <https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist>
- .NET Framework: <https://dotnet.microsoft.com/download/dotnet-framework/net48>

## Language models

No model weights are included in the repository, source archive or Windows
release ZIP. Users obtain their own GGUF model or configure a compatible API.
Each model and server retains its own license and terms.

## Source availability

Release source is the matching repository tag, also available as a separate
source archive. It includes C# source, original assets, build and packaging
scripts, tests and documentation. A .NET Framework compiler supplied by Windows
build tools and the separately downloaded upstream runtime are used as described
in the README. No proprietary Makro+ build service or model download is required.
