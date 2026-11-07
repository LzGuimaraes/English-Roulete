#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
cd "$(dirname "$0")"
: "${APP_IMAGE:?Release digest required}"
: "${DEPLOY_ROOT:?Deploy directory required}"
compose=(docker compose --env-file .env -f compose.production.yaml)
phase="Validate Compose configuration"

log() { printf '%s [deploy] %s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" "$*"; }
diagnostics() {
  log "Container status and recent logs (configuration and environment are omitted)"
  "${compose[@]}" ps -a || true
  "${compose[@]}" logs --no-color --tail 80 app postgres || true
  local service id
  for service in app postgres; do
    id=$("${compose[@]}" ps -a -q "$service" 2>/dev/null) || true
    if [[ -n "$id" ]]; then
      docker inspect --format 'Status={{.State.Status}} ExitCode={{.State.ExitCode}} Restarts={{.RestartCount}} {{if .State.Health}}Health={{.State.Health.Status}}{{end}}' "$id" || true
      docker inspect --format '{{if .State.Health}}{{range .State.Health.Log}}HealthExit={{.ExitCode}} {{.Output}}{{end}}{{end}}' "$id" || true
    fi
  done
}
on_error() {
  local status=$?
  trap - ERR
  set +e
  log "FAILED: $phase (exit $status)"
  if [[ "$phase" == "Download images" ]]; then
    log "Check APP_IMAGE and GHCR access: public package, VPS login or VPS_REGISTRY_TOKEN."
  fi
  diagnostics
  exit "$status"
}
trap on_error ERR

log "$phase"
"${compose[@]}" config --quiet
phase="Download images"
log "$phase"
# Images must be downloadable by the VPS: authenticate GHCR there once if private.
"${compose[@]}" pull app postgres
phase="Start PostgreSQL and wait for readiness"
log "$phase"
"${compose[@]}" up -d --wait --wait-timeout 120 postgres
mkdir -p "$DEPLOY_ROOT/backups"
# Back up existing deployments before migration; the first deployment has no current image.
if [[ -f "$DEPLOY_ROOT/current-image" ]]; then
  phase="Back up existing database"
  log "$phase"
  "${compose[@]}" exec -T postgres \
    sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' \
    > "$DEPLOY_ROOT/backups/pre-${RELEASE_SHA:?}.dump"
else
  log "First deployment: no current-image; pre-migration backup skipped."
fi
phase="Apply reviewed migrations"
log "$phase"
"${compose[@]}" run --rm --no-deps app --migrate
phase="Provision administrator (preserve existing password)"
log "$phase"
# Bootstrap is explicit and never resets an existing password. No demo assets are required.
set -a
source ./bootstrap.env
set +a
"${compose[@]}" run --rm --no-deps -e Admin__BootstrapEmail -e Admin__BootstrapPassword app --bootstrap-admin
unset Admin__BootstrapEmail Admin__BootstrapPassword
phase="Start application and wait for healthcheck"
log "$phase"
if ! "${compose[@]}" up -d --wait --wait-timeout 180 app; then
  log "FAILED: $phase; collecting diagnostics before rollback."
  diagnostics
  if [[ -f "$DEPLOY_ROOT/current-image" ]]; then
    phase="Rollback to previous image"
    log "$phase"
    APP_IMAGE=$(cat "$DEPLOY_ROOT/current-image") "${compose[@]}" up -d --wait --wait-timeout 180 app
    log "Previous image restored; this release remains failed."
  else
    log "First deployment: no previous image available for rollback."
  fi
  exit 1
fi
phase="Record healthy release"
log "$phase"
printf '%s\n' "$APP_IMAGE" > "$DEPLOY_ROOT/current-image"
printf '%s\n' "$PWD" > "$DEPLOY_ROOT/current-release"
log "Deployment completed successfully."
