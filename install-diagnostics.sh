#!/bin/bash
# Install diagnostic tools for .NET and SQLite debugging

set -e

echo "Installing diagnostic tools..."

# .NET diagnostic tools
echo "Installing .NET diagnostic tools..."
dotnet tool install --global dotnet-trace || echo "dotnet-trace already installed"
dotnet tool install --global dotnet-counters || echo "dotnet-counters already installed"
dotnet tool install --global dotnet-dump || echo "dotnet-dump already installed"
dotnet tool install --global dotnet-sos || echo "dotnet-sos already installed"

# SQLite tools (if not already installed)
echo "Installing SQLite tools..."
sudo apt-get update
sudo apt-get install -y sqlite3 sqlitebrowser

# Network diagnostic tools
echo "Installing network diagnostic tools..."
sudo apt-get install -y curl wget netcat-openbsd tcpdump

# Process monitoring
echo "Installing process monitoring tools..."
sudo apt-get install -y htop iotop strace lsof

# Log analysis tools
echo "Installing log analysis tools..."
sudo apt-get install -y jq

echo "Done! Installed tools:"
echo "  - dotnet-trace: .NET tracing"
echo "  - dotnet-counters: Performance counters"
echo "  - dotnet-dump: Memory dumps"
echo "  - dotnet-sos: Debugging extensions"
echo "  - sqlite3: SQLite CLI"
echo "  - sqlitebrowser: SQLite GUI"
echo "  - Network tools: curl, wget, netcat, tcpdump"
echo "  - Process tools: htop, iotop, strace, lsof"
echo "  - jq: JSON processor"

