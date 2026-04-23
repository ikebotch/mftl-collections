#!/bin/bash
# Fixed start script for ARM64 macOS
export DOTNET_ROLL_FORWARD=Major
cd src/MFTL.Collections.Api
func start
