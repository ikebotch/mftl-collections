#!/bin/bash
# Fixed build script for ARM64 macOS
export DOTNET_ROLL_FORWARD=Major
dotnet build src/MFTL.Collections.Api/MFTL.Collections.Api.csproj
