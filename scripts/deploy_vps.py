#!/usr/bin/env python3
"""Pinned-host SSH deploy with validated configuration and secret-safe stage logs."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import ipaddress
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import tempfile
import time


class ConfigurationError(ValueError):
    pass


def annotation(message):
    prefix = "::error::" if os.environ.get("GITHUB_ACTIONS") == "true" else "ERROR: "
    print(prefix + message, flush=True)


def mask(value):
    if value and os.environ.get("GITHUB_ACTIONS") == "true":
        escaped = value.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
        print("::add-mask::" + escaped, flush=True)


@contextmanager
def stage(name):
    started = time.monotonic()
    if os.environ.get("GITHUB_ACTIONS") == "true":
        print("::group::" + name, flush=True)
    print(datetime.now(timezone.utc).isoformat(timespec="seconds") + " [deploy] " + name, flush=True)
    try:
        yield
    except (subprocess.CalledProcessError, OSError):
        annotation(name + " failed. Check the output of this stage.")
        raise
    finally:
        print(f"[deploy] {name}: {time.monotonic() - started:.1f}s", flush=True)
        if os.environ.get("GITHUB_ACTIONS") == "true":
            print("::endgroup::", flush=True)


def checked(name, pattern):
    value = os.environ.get(name, "")
    if not re.fullmatch(pattern, value):
        raise ConfigurationError(f"Invalid or missing {name}.")
    return value


def configuration():
    mask(os.environ.get("VPS_SSH_PASSPHRASE", ""))
    required = ["VPS_HOST", "VPS_USER", "VPS_SSH_KEY", "VPS_KNOWN_HOSTS", "VPS_ENV_FILE", "APP_IMAGE", "RELEASE_SHA"]
    missing = [name for name in required if not os.environ.get(name, "").strip()]
    if missing:
        raise ConfigurationError("Missing configuration: " + ", ".join(missing) + ".")
    values = {}
    for number, line in enumerate(os.environ["VPS_ENV_FILE"].splitlines(), 1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if "=" not in line:
            raise ConfigurationError(f"VPS_ENV_FILE line {number}: expected KEY=VALUE.")
        name, value = line.split("=", 1)
        name, value = name.strip(), value.strip()
        if not re.fullmatch(r"[A-Z][A-Z0-9_]*", name):
            raise ConfigurationError(f"VPS_ENV_FILE line {number}: invalid variable name.")
        if name.endswith(("PASSWORD", "TOKEN", "SECRET", "KEY")) or name == "ADMIN_EMAIL":
            mask(value)
        if name in values:
            raise ConfigurationError(f"VPS_ENV_FILE: duplicated {name}.")
        values[name] = value
    required_env = ["POSTGRES_DB", "POSTGRES_USER", "POSTGRES_PASSWORD", "ADMIN_EMAIL", "ADMIN_PASSWORD", "APP_DOMAIN", "TRAEFIK_NETWORK", "TRUSTED_PROXY_CIDR"]
    errors = []
    for name in required_env:
        if not values.get(name) or "SUBSTITUA" in values[name]:
            errors.append("Set " + name + " in VPS_ENV_FILE.")
        else:
            print("[config] " + name + ": configured (value hidden)", flush=True)
    host = checked("VPS_HOST", r"[A-Za-z0-9][A-Za-z0-9.:-]*")
    user = checked("VPS_USER", r"[a-zA-Z_][a-zA-Z0-9_-]*")
    port = checked("VPS_SSH_PORT", r"[0-9]{1,5}")
    if not 1 <= int(port) <= 65535:
        errors.append("VPS_SSH_PORT must be between 1 and 65535.")
    root = checked("VPS_DEPLOY_PATH", r"/[A-Za-z0-9_./-]+")
    if ".." in Path(root).parts or Path(root) == Path("/"):
        errors.append("Choose a dedicated absolute VPS_DEPLOY_PATH.")
    sha = checked("RELEASE_SHA", r"[a-f0-9]{40}")
    image = checked("APP_IMAGE", r"ghcr\.io/[a-z0-9_./-]+@sha256:[a-f0-9]{64}")
    if not re.fullmatch(r"[a-fA-F0-9]{32,}", values.get("POSTGRES_PASSWORD", "")):
        errors.append("POSTGRES_PASSWORD must be hexadecimal with at least 32 characters.")
    for name in ["POSTGRES_DB", "POSTGRES_USER"]:
        if not re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,62}", values.get(name, "")):
            errors.append(name + " must contain 1-63 letters, digits, underscores, dots or hyphens.")
    if not re.fullmatch(r"(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?", values.get("APP_DOMAIN", "")):
        errors.append("APP_DOMAIN must be a hostname without https://, port or path.")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*", values.get("TRAEFIK_NETWORK", "")):
        errors.append("TRAEFIK_NETWORK must be a Docker network name.")
    try:
        ipaddress.ip_network(values.get("TRUSTED_PROXY_CIDR", ""))
    except ValueError:
        errors.append("TRUSTED_PROXY_CIDR must be a valid network CIDR.")
    if not re.fullmatch(r"[^\s@]+@[^\s@]+", values.get("ADMIN_EMAIL", "")):
        errors.append("ADMIN_EMAIL must be a valid email address.")
    password = values.get("ADMIN_PASSWORD", "")
    if len(password) < 12 or len(set(password)) < 5 or not all(re.search(pattern, password) for pattern in [r"[A-Z]", r"[a-z]", r"[0-9]", r"[^A-Za-z0-9]"]):
        errors.append("ADMIN_PASSWORD requires 12 characters, 5 distinct characters, uppercase, lowercase, digit and symbol.")
    if os.environ.get("VPS_REGISTRY_TOKEN"):
        checked("VPS_REGISTRY_USER", r"[a-zA-Z0-9_-]+")
        mask(os.environ["VPS_REGISTRY_TOKEN"])
    else:
        print("[config] VPS_REGISTRY_TOKEN: not set; VPS must have GHCR access or image must be public.", flush=True)
    if errors:
        raise ConfigurationError("\n".join(errors))
    for name in required:
        print("[config] " + name + ": configured (value hidden)", flush=True)
    return host, user, port, root, sha, image, values


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Validate configuration and SSH files without connecting to the VPS.")
    args = parser.parse_args()
    with stage("Validate deployment configuration"):
        host, user, port, root, sha, image, values = configuration()
        for tool in ["ssh", "scp", "ssh-keygen"]:
            if shutil.which(tool) is None:
                raise ConfigurationError("Install " + tool + " on the runner.")
    with tempfile.TemporaryDirectory() as directory:
        private = Path(directory)
        key = private / "id"
        key.write_text(os.environ["VPS_SSH_KEY"].replace("\r\n", "\n").strip() + "\n")
        key.chmod(0o600)
        known = private / "known_hosts"
        known.write_text(os.environ["VPS_KNOWN_HOSTS"] + "\n")
        with stage("Validate SSH key and pinned host"):
            if os.environ.get("VPS_SSH_PASSPHRASE"):
                askpass = private / "askpass.sh"
                askpass.write_text("#!/bin/sh\nprintf '%s\\n' \"$VPS_SSH_PASSPHRASE\"\n")
                askpass.chmod(0o700)
                key_env = os.environ.copy()
                key_env.update(SSH_ASKPASS=str(askpass), SSH_ASKPASS_REQUIRE="force", DISPLAY="deploy:0")
                result = subprocess.run(["ssh-keygen", "-p", "-N", "", "-f", str(key)], env=key_env,
                                        stdin=subprocess.DEVNULL, capture_output=True, start_new_session=True)
                if result.returncode:
                    raise ConfigurationError("Could not unlock VPS_SSH_KEY. Check VPS_SSH_PASSPHRASE.")
                key.chmod(0o600)
            result = subprocess.run(["ssh-keygen", "-y", "-P", "", "-f", str(key)], capture_output=True)
            if result.returncode:
                raise ConfigurationError("VPS_SSH_KEY must be a valid private key with real line breaks. For an encrypted key, set VPS_SSH_PASSPHRASE.")
            lookup = f"[{host}]:{port}" if int(port) != 22 else host
            result = subprocess.run(["ssh-keygen", "-F", lookup, "-f", str(known)], capture_output=True)
            if result.returncode:
                raise ConfigurationError("VPS_KNOWN_HOSTS has no matching entry for VPS_HOST and VPS_SSH_PORT.")
        if args.check:
            print("[deploy] Configuration validated; no VPS connection attempted.", flush=True)
            return
        env_file = private / ".env"
        env_file.write_text(os.environ["VPS_ENV_FILE"] + "\n")
        env_file.chmod(0o600)
        bootstrap = private / "bootstrap.env"
        bootstrap.write_text("Admin__BootstrapEmail=" + shlex.quote(values["ADMIN_EMAIL"]) + "\nAdmin__BootstrapPassword=" + shlex.quote(values["ADMIN_PASSWORD"]) + "\n")
        bootstrap.chmod(0o600)
        options = ["-i", str(key), "-o", f"UserKnownHostsFile={known}", "-o", "StrictHostKeyChecking=yes", "-o", "BatchMode=yes", "-o", "IdentitiesOnly=yes", "-o", "ConnectTimeout=15", "-o", "ServerAliveInterval=15", "-o", "ServerAliveCountMax=3"]
        target = f"{user}@{host}"
        release = f"{root}/releases/{sha}"

        def remote(command, content=None):
            try:
                subprocess.run(["ssh", *options, "-p", port, target, command], input=content, text=True, check=True)
            except subprocess.CalledProcessError as error:
                if error.returncode == 255:
                    annotation("SSH failed: verify VPS_HOST, VPS_USER, VPS_SSH_PORT, VPS_KNOWN_HOSTS and VPS_SSH_KEY. The matching public key must be in ~/.ssh/authorized_keys for VPS_USER.")
                raise

        with stage("SSH access, Docker and Traefik network"):
            remote("docker info --format '{{.ServerVersion}}' && docker compose version && docker network inspect " + shlex.quote(values["TRAEFIK_NETWORK"]) + " --format '{{.Name}}' >/dev/null && umask 077 && mkdir -p " + shlex.quote(release))
        with stage("Transfer release files"):
            subprocess.run(["scp", *options, "-P", port, "infra/compose.production.yaml", "scripts/deploy/release.sh", str(env_file), str(bootstrap), target + ":" + release + "/"], check=True)
        if os.environ.get("VPS_REGISTRY_TOKEN"):
            with stage("VPS login to GHCR"):
                remote("docker login ghcr.io --username " + shlex.quote(os.environ["VPS_REGISTRY_USER"]) + " --password-stdin", os.environ["VPS_REGISTRY_TOKEN"] + "\n")
        with stage("Remote release, migrations and healthcheck"):
            command = "APP_IMAGE=" + shlex.quote(image) + " DEPLOY_ROOT=" + shlex.quote(root) + " RELEASE_SHA=" + sha + " bash " + shlex.quote(release + "/release.sh")
            remote(command)
        print("[deploy] Deployment healthy. Release: " + sha, flush=True)


if __name__ == "__main__":
    try:
        main()
    except ConfigurationError as error:
        for message in str(error).splitlines():
            annotation(message)
        raise SystemExit(2) from None
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode) from None
    except OSError:
        annotation("Could not execute a deployment tool or access temporary files on the runner.")
        raise SystemExit(1) from None
