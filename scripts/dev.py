#!/usr/bin/env python3
"""Local database, migrations and demo runner; credentials stay in infra/.env."""
import argparse
import os
from pathlib import Path
import secrets
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
ENV_FILE = ROOT / "infra/.env"


def initialize():
    ENV_FILE.parent.mkdir(exist_ok=True)
    if not ENV_FILE.exists():
        fd = os.open(ENV_FILE, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(fd, "w") as stream:
            stream.write("POSTGRES_USER=englishroulette\nPOSTGRES_DB=englishroulette\n"
                         "POSTGRES_PASSWORD=" + secrets.token_hex(24) + "\n")
    existing = ENV_FILE.read_text()
    additions = ""
    if not any(line.startswith("ADMIN_EMAIL=") for line in existing.splitlines()):
        additions += "ADMIN_EMAIL=admin@englishroulette.local\n"
    if not any(line.startswith("ADMIN_PASSWORD=") for line in existing.splitlines()):
        additions += "ADMIN_PASSWORD=Er1!" + secrets.token_urlsafe(24) + "\n"
    if additions:
        with ENV_FILE.open("a") as stream:
            stream.write(("\n" if existing and not existing.endswith("\n") else "") + additions)


def environment():
    initialize()
    values = dict(line.split("=", 1) for line in ENV_FILE.read_text().splitlines()
                  if line.strip() and not line.lstrip().startswith("#"))
    env = os.environ.copy()
    host = env.get("DOTNET_HOST_PATH") or shutil.which("dotnet")
    if not host:
        raise SystemExit("Install the .NET 10 SDK, or set DOTNET_HOST_PATH.")
    host = str(Path(host).resolve())
    env["DOTNET_HOST_PATH"] = host
    env["DOTNET_ROOT"] = str(Path(host).parent)
    env["PATH"] = str(Path(host).parent) + os.pathsep + env.get("PATH", "")
    connection = ("Host=127.0.0.1;Port=55432;Database=" + values["POSTGRES_DB"] +
                  ";Username=" + values["POSTGRES_USER"] + ";Password=" + values["POSTGRES_PASSWORD"])
    env["ConnectionStrings__Adventure"] = connection
    env["ENGLISHROULETTE_TEST_CONNECTION"] = connection.replace(
        "Database=" + values["POSTGRES_DB"], "Database=englishroulette_tests")
    return host, env, values


def run(command, env):
    subprocess.run(command, cwd=ROOT, env=env, check=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["init", "db", "migrate", "setup", "run", "test", "admin", "admin-reset"])
    parser.add_argument("--no-build", action="store_true", help="Run the existing Release build.")
    args = parser.parse_args()
    host, env, values = environment()
    compose = ["docker", "compose", "-f", "infra/compose.yaml", "--env-file", "infra/.env"]
    if args.command in {"db", "setup"}:
        run(compose + ["up", "-d", "--wait"], env)
        exists = subprocess.check_output(compose + ["exec", "-T", "postgres", "psql", "-U",
            values["POSTGRES_USER"], "-d", "postgres", "-tAc",
            "SELECT 1 FROM pg_database WHERE datname='englishroulette_tests'"], cwd=ROOT, env=env, text=True)
        if not exists.strip():
            run(compose + ["exec", "-T", "postgres", "createdb", "-U", values["POSTGRES_USER"],
                "englishroulette_tests"], env)
    if args.command in {"migrate", "setup"}:
        run([host, "tool", "restore"], env)
        run([host, "ef", "database", "update", "--project", "src/EnglishRoulette.Infrastructure",
             "--configuration", "Release"], env)
    if args.command == "setup":
        run([shutil.which("python3") or "python3", "scripts/generate_demo_audio.py"], env)
    if args.command in {"setup", "admin", "admin-reset"}:
        env["Admin__BootstrapEmail"] = values["ADMIN_EMAIL"]
        env["Admin__BootstrapPassword"] = values["ADMIN_PASSWORD"]
        arguments = [host, "run", "--project", "src/EnglishRoulette.Api", "--configuration", "Release", "--no-launch-profile"]
        if args.no_build:
            arguments.append("--no-build")
        arguments += ["--", "--bootstrap-admin"]
        if args.command == "admin-reset":
            arguments.append("--reset-admin-password")
        run(arguments, env)
    if args.command == "run":
        os.chdir(ROOT)
        arguments = [host, "run", "--project", "src/EnglishRoulette.Api", "--configuration", "Release",
                     "--launch-profile", "http"]
        if args.no_build:
            arguments.append("--no-build")
        os.execve(host, arguments, env)
    if args.command == "test":
        arguments = [host, "test", "EnglishRoulette.sln", "--configuration", "Release"]
        if args.no_build:
            arguments.append("--no-build")
        run(arguments, env)


if __name__ == "__main__":
    main()
