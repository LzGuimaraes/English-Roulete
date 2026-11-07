#!/usr/bin/env bash
set -euo pipefail
umask 077
cd "$(dirname "$0")"
: "${APP_IMAGE:?Release digest required}"
: "${DEPLOY_ROOT:?Deploy directory required}"
compose=(docker compose --env-file .env -f compose.production.yaml)
# Images must be downloadable by the VPS: authenticate GHCR there once if private.
"${compose[@]}" pull app postgres
"${compose[@]}" up -d --wait postgres
mkdir -p "$DEPLOY_ROOT/backups"
# Keep an on-host pre-migration recovery point; schedule off-host backups separately.
"${compose[@]}" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$DEPLOY_ROOT/backups/pre-${RELEASE_SHA:?}.dump"
"${compose[@]}" run --rm --no-deps app --migrate
# Bootstrap is explicit and never resets an existing password. No demo assets are required.
set -a
source ./bootstrap.env
set +a
"${compose[@]}" run --rm --no-deps -e Admin__BootstrapEmail -e Admin__BootstrapPassword app --bootstrap-admin
unset Admin__BootstrapEmail Admin__BootstrapPassword
if ! "${compose[@]}" up -d --wait --wait-timeout 180 app; then
  if [[ -f "$DEPLOY_ROOT/current-image" ]]; then
    APP_IMAGE=$(cat "$DEPLOY_ROOT/current-image") "${compose[@]}" up -d --wait --wait-timeout 180 app
  fi
  exit 1
fi
printf '%s\n' "$APP_IMAGE" > "$DEPLOY_ROOT/current-image"
printf '%s\n' "$PWD" > "$DEPLOY_ROOT/current-release"
