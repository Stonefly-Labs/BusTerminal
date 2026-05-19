#!/usr/bin/env bash
# BusTerminal prerequisite verification (no installs).
# Exits 0 when every prerequisite is present at or above the minimum;
# exits 1 with an actionable summary when one or more are missing.
set -uo pipefail

# --- Required minimums ---
REQ_DOTNET_MAJOR=10
REQ_NODE_MAJOR=22
REQ_PNPM_MAJOR=10
REQ_TOFU_MAJOR=1
REQ_TOFU_MINOR=10
REQ_AZ_MAJOR=2
REQ_AZ_MINOR=60
REQ_PWSH_MAJOR=7
REQ_PWSH_MINOR=4

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }
bold()   { printf '\033[1m%s\033[0m\n' "$*"; }

gaps=()
ok=()

bold "BusTerminal prerequisite check"
echo

# --- .NET 10 ---
if command -v dotnet >/dev/null 2>&1; then
  dotnet_version="$(dotnet --version 2>/dev/null || echo '')"
  major="${dotnet_version%%.*}"
  if [[ "$major" =~ ^[0-9]+$ ]] && (( major >= REQ_DOTNET_MAJOR )); then
    ok+=(".NET SDK $dotnet_version")
  else
    gaps+=(".NET SDK ${REQ_DOTNET_MAJOR}+ required (found '$dotnet_version'). Install via https://dotnet.microsoft.com/download")
  fi
else
  gaps+=(".NET SDK ${REQ_DOTNET_MAJOR}+ not found. Install via https://dotnet.microsoft.com/download")
fi

# --- Node.js 22 ---
if command -v node >/dev/null 2>&1; then
  node_version="$(node --version 2>/dev/null | sed 's/^v//')"
  major="${node_version%%.*}"
  if [[ "$major" =~ ^[0-9]+$ ]] && (( major >= REQ_NODE_MAJOR )); then
    ok+=("Node.js $node_version")
  else
    gaps+=("Node.js ${REQ_NODE_MAJOR} LTS required (found '$node_version'). Install via https://nodejs.org or 'nvm install ${REQ_NODE_MAJOR}'")
  fi
else
  gaps+=("Node.js ${REQ_NODE_MAJOR} LTS not found. Install via https://nodejs.org or 'nvm install ${REQ_NODE_MAJOR}'")
fi

# --- pnpm ---
if command -v pnpm >/dev/null 2>&1; then
  pnpm_version="$(pnpm --version 2>/dev/null)"
  major="${pnpm_version%%.*}"
  if [[ "$major" =~ ^[0-9]+$ ]] && (( major >= REQ_PNPM_MAJOR )); then
    ok+=("pnpm $pnpm_version")
  else
    gaps+=("pnpm ${REQ_PNPM_MAJOR}+ recommended (found '$pnpm_version'). Install: 'corepack enable pnpm' or 'npm i -g pnpm'")
  fi
else
  gaps+=("pnpm not found. Install: 'corepack enable pnpm' or 'npm i -g pnpm'")
fi

# --- OpenTofu ---
if command -v tofu >/dev/null 2>&1; then
  tofu_version="$(tofu version 2>/dev/null | head -1 | awk '{print $2}' | sed 's/^v//')"
  major="${tofu_version%%.*}"
  rest="${tofu_version#*.}"
  minor="${rest%%.*}"
  if [[ "$major" =~ ^[0-9]+$ ]] && [[ "$minor" =~ ^[0-9]+$ ]] && \
     ( (( major > REQ_TOFU_MAJOR )) || ( (( major == REQ_TOFU_MAJOR )) && (( minor >= REQ_TOFU_MINOR )) ) ); then
    ok+=("OpenTofu $tofu_version")
  else
    gaps+=("OpenTofu ${REQ_TOFU_MAJOR}.${REQ_TOFU_MINOR}+ required (found '$tofu_version'). Install via https://opentofu.org/docs/intro/install/")
  fi
else
  gaps+=("OpenTofu ${REQ_TOFU_MAJOR}.${REQ_TOFU_MINOR}+ not found. Install via https://opentofu.org/docs/intro/install/")
fi

# --- Azure CLI ---
if command -v az >/dev/null 2>&1; then
  az_version="$(az version --output tsv --query '"azure-cli"' 2>/dev/null)"
  major="${az_version%%.*}"
  rest="${az_version#*.}"
  minor="${rest%%.*}"
  if [[ "$major" =~ ^[0-9]+$ ]] && [[ "$minor" =~ ^[0-9]+$ ]] && \
     ( (( major > REQ_AZ_MAJOR )) || ( (( major == REQ_AZ_MAJOR )) && (( minor >= REQ_AZ_MINOR )) ) ); then
    ok+=("Azure CLI $az_version")
  else
    gaps+=("Azure CLI ${REQ_AZ_MAJOR}.${REQ_AZ_MINOR}+ required (found '$az_version'). Install via https://aka.ms/InstallAzureCLI")
  fi
else
  gaps+=("Azure CLI ${REQ_AZ_MAJOR}.${REQ_AZ_MINOR}+ not found. Install via https://aka.ms/InstallAzureCLI")
fi

# --- Docker ---
if command -v docker >/dev/null 2>&1; then
  if docker info >/dev/null 2>&1; then
    docker_version="$(docker version --format '{{.Client.Version}}' 2>/dev/null)"
    ok+=("Docker $docker_version (daemon reachable)")
  else
    gaps+=("Docker is installed but daemon is not reachable. Start Docker Desktop or the docker service.")
  fi
else
  gaps+=("Docker not found. Install Docker Desktop (https://docs.docker.com/get-docker/) or Docker Engine.")
fi

# --- PowerShell 7 ---
if command -v pwsh >/dev/null 2>&1; then
  pwsh_version="$(pwsh --version 2>/dev/null | awk '{print $2}')"
  major="${pwsh_version%%.*}"
  rest="${pwsh_version#*.}"
  minor="${rest%%.*}"
  if [[ "$major" =~ ^[0-9]+$ ]] && [[ "$minor" =~ ^[0-9]+$ ]] && \
     ( (( major > REQ_PWSH_MAJOR )) || ( (( major == REQ_PWSH_MAJOR )) && (( minor >= REQ_PWSH_MINOR )) ) ); then
    ok+=("PowerShell $pwsh_version")
  else
    gaps+=("PowerShell ${REQ_PWSH_MAJOR}.${REQ_PWSH_MINOR}+ required (found '$pwsh_version'). Install via https://aka.ms/PowerShell")
  fi
else
  gaps+=("PowerShell ${REQ_PWSH_MAJOR}.${REQ_PWSH_MINOR}+ not found. Install via https://aka.ms/PowerShell")
fi

# --- Report ---
for entry in "${ok[@]}"; do
  green "  ok  $entry"
done

if (( ${#gaps[@]} > 0 )); then
  echo
  red "$(bold 'Missing prerequisites:')"
  for entry in "${gaps[@]}"; do
    yellow "  - $entry"
  done
  echo
  red "Resolve the gaps above, then re-run scripts/bootstrap.sh."
  exit 1
fi

echo
green "$(bold 'All prerequisites satisfied.')"
exit 0
