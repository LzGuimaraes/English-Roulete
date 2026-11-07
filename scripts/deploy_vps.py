#!/usr/bin/env python3
"""Pinned-host SSH deploy. Secrets are passed through environment, never shell arguments."""
import os
from pathlib import Path
import re
import shlex
import subprocess
import tempfile


def checked(name, pattern):
    value = os.environ.get(name, "")
    if not re.fullmatch(pattern, value):
        raise SystemExit(f"Invalid or missing {name}.")
    return value


def main():
    host = checked("VPS_HOST", r"[A-Za-z0-9][A-Za-z0-9.:-]*")
    user = checked("VPS_USER", r"[a-zA-Z_][a-zA-Z0-9_-]*")
    port = checked("VPS_SSH_PORT", r"[0-9]{1,5}")
    root = checked("VPS_DEPLOY_PATH", r"/[A-Za-z0-9_./-]+")
    if ".." in Path(root).parts or root == "/":
        raise SystemExit("Choose a dedicated absolute VPS_DEPLOY_PATH.")
    sha = checked("RELEASE_SHA", r"[a-f0-9]{40}")
    image = checked("APP_IMAGE", r"ghcr\.io/[a-z0-9_./-]+@sha256:[a-f0-9]{64}")
    with tempfile.TemporaryDirectory() as directory:
        private = Path(directory)
        key = private / "id"
        key.write_text(os.environ["VPS_SSH_KEY"] + "\n")
        key.chmod(0o600)
        known = private / "known_hosts"
        known.write_text(os.environ["VPS_KNOWN_HOSTS"] + "\n")
        env_file = private / ".env"
        env_file.write_text(os.environ["VPS_ENV_FILE"] + "\n")
        env_file.chmod(0o600)
        values = {}
        for line in env_file.read_text().splitlines():
            if line.strip() and not line.lstrip().startswith("#"):
                name, value = line.split("=", 1)
                values[name] = value.strip()
        for name in ["POSTGRES_PASSWORD", "ADMIN_EMAIL", "ADMIN_PASSWORD", "APP_DOMAIN", "TRAEFIK_NETWORK", "TRUSTED_PROXY_CIDR"]:
            if not values.get(name) or "SUBSTITUA" in values[name]:
                raise SystemExit(f"Set {name} in VPS_ENV_FILE.")
        if not re.fullmatch(r"[a-fA-F0-9]{32,}", values["POSTGRES_PASSWORD"]):
            raise SystemExit("Use a hexadecimal POSTGRES_PASSWORD of at least 32 characters.")
        bootstrap = private / "bootstrap.env"
        bootstrap.write_text("Admin__BootstrapEmail=" + shlex.quote(values["ADMIN_EMAIL"]) + "\nAdmin__BootstrapPassword=" + shlex.quote(values["ADMIN_PASSWORD"]) + "\n")
        bootstrap.chmod(0o600)
        options = ["-i", str(key), "-o", f"UserKnownHostsFile={known}", "-o", "StrictHostKeyChecking=yes", "-o", "BatchMode=yes"]
        target = f"{user}@{host}"
        release = f"{root}/releases/{sha}"
        def remote(command, content=None):
            subprocess.run(["ssh", *options, "-p", port, target, command], input=content, text=True, check=True)
        remote("umask 077; mkdir -p " + shlex.quote(release))
        subprocess.run(["scp", *options, "-P", port, "infra/compose.production.yaml", "scripts/deploy/release.sh", str(env_file), str(bootstrap), target + ":" + release + "/"], check=True)
        if os.environ.get("VPS_REGISTRY_TOKEN"):
            registry_user = checked("VPS_REGISTRY_USER", r"[a-zA-Z0-9_-]+")
            remote("docker login ghcr.io --username " + shlex.quote(registry_user) + " --password-stdin", os.environ["VPS_REGISTRY_TOKEN"] + "\n")
        command = "APP_IMAGE=" + shlex.quote(image) + " DEPLOY_ROOT=" + shlex.quote(root) + " RELEASE_SHA=" + sha + " bash " + shlex.quote(release + "/release.sh")
        remote(command)
        print("Deployment healthy. Release: " + sha)


if __name__ == "__main__":
    main()
