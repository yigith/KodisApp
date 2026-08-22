#!/usr/bin/env bash
# Publishes the API and installs it on the VPS under /opt/kodisapi.
#
# Self-contained, so the server needs no .NET runtime. Secrets live only in
# /etc/kodisapi/kodisapi.env on the server and are never touched from here.
#
#   ./scripts/deploy.sh            # deploy to the default host
#   HOST=vps ./scripts/deploy.sh   # or name the ssh host explicitly
set -euo pipefail

HOST="${HOST:-vps}"
APP_DIR=/opt/kodisapi
SERVICE=kodisapi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT

echo "==> Running tests"
dotnet test "$repo_root/KodisApp.sln" --nologo -v q

echo "==> Publishing (linux-x64, self-contained)"
dotnet publish "$repo_root/KodisApi/KodisApi.csproj" \
    -c Release -r linux-x64 --self-contained true \
    -o "$staging" --nologo -v q

echo "==> Uploading to $HOST:$APP_DIR"
# The service is stopped first: the binary cannot be replaced while it runs,
# and a brief outage is preferable to a half-written deployment.
ssh "$HOST" "systemctl stop $SERVICE"
tar -czf - -C "$staging" . | ssh "$HOST" "
    rm -rf $APP_DIR/* &&
    tar -xzf - -C $APP_DIR &&
    chmod +x $APP_DIR/KodisApi &&
    chown -R root:$SERVICE $APP_DIR &&
    chmod -R g+rX $APP_DIR"

echo "==> Starting"
ssh "$HOST" "systemctl start $SERVICE"
sleep 5

echo "==> Health"
if ssh "$HOST" "curl -fsS http://127.0.0.1:3003/health"; then
    echo
    echo "==> Deployed."
else
    echo
    echo "!! Health check failed. Recent logs:"
    ssh "$HOST" "journalctl -u $SERVICE -n 30 --no-pager -o cat"
    exit 1
fi
