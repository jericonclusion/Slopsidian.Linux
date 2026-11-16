#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
dotnet tool restore
dotnet restore Slopsidian.Linux.sln --source https://api.nuget.org/v3/index.json --nologo
dotnet csharpier check src/RonSijm.Slopsidian.Linux tests/RonSijm.Slopsidian.Linux.Tests
dotnet format style src/RonSijm.Slopsidian.Linux --no-restore --verify-no-changes --severity warn
dotnet build Slopsidian.Linux.sln --no-restore --configuration Release --nologo --warnaserror
