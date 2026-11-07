"""Deployment checks use temporary SSH keys and a fake Docker CLI; no VPS is contacted."""
from contextlib import redirect_stdout
import importlib.util
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("deploy_vps", ROOT / "scripts/deploy_vps.py")
deploy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deploy)


def settings():
    return {
        "VPS_HOST": "example.invalid", "VPS_USER": "deploy", "VPS_SSH_PORT": "22",
        "VPS_DEPLOY_PATH": "/opt/englishroulette", "VPS_SSH_KEY": "test-key",
        "VPS_KNOWN_HOSTS": "test-host", "RELEASE_SHA": "a" * 40,
        "APP_IMAGE": "ghcr.io/example/app@sha256:" + "b" * 64,
        "VPS_ENV_FILE": "\n".join([
            "POSTGRES_DB=roulette", "POSTGRES_USER=roulette", "POSTGRES_PASSWORD=" + "c" * 64,
            "ADMIN_EMAIL=admin@example.invalid", "ADMIN_PASSWORD=Fixture1!Password",
            "APP_DOMAIN=example.invalid", "TRAEFIK_NETWORK=traefik-public", "TRUSTED_PROXY_CIDR=172.20.0.0/16"
        ])
    }


class ConfigurationTests(unittest.TestCase):
    def validate(self, values):
        output = io.StringIO()
        with patch.dict(os.environ, values, clear=True), redirect_stdout(output):
            result = deploy.configuration()
        return result, output.getvalue()

    def test_valid_configuration_logs_names_without_secret_values(self):
        values = settings()
        result, output = self.validate(values)
        self.assertEqual(result[0], "example.invalid")
        self.assertIn("POSTGRES_DB: configured", output)
        self.assertNotIn("Fixture1!Password", output)
        self.assertNotIn("c" * 64, output)
        self.assertNotIn("admin@example.invalid", output)

    def test_missing_database_fields_report_names(self):
        values = settings()
        values["VPS_ENV_FILE"] = "\n".join(line for line in values["VPS_ENV_FILE"].splitlines() if not line.startswith("POSTGRES_DB="))
        with self.assertRaisesRegex(deploy.ConfigurationError, "POSTGRES_DB"):
            self.validate(values)

    def test_invalid_port_domain_cidr_and_password_are_actionable(self):
        for field, before, after in [
            ("APP_DOMAIN", "APP_DOMAIN=example.invalid", "APP_DOMAIN=https://example.invalid/path"),
            ("TRUSTED_PROXY_CIDR", "172.20.0.0/16", "not-a-network"),
            ("ADMIN_PASSWORD", "Fixture1!Password", "short"),
            ("POSTGRES_PASSWORD", "c" * 64, "not-hex")
        ]:
            with self.subTest(field=field):
                values = settings(); values["VPS_ENV_FILE"] = values["VPS_ENV_FILE"].replace(before, after)
                with self.assertRaisesRegex(deploy.ConfigurationError, field):
                    self.validate(values)
        values = settings(); values["VPS_SSH_PORT"] = "65536"
        with self.assertRaisesRegex(deploy.ConfigurationError, "VPS_SSH_PORT"):
            self.validate(values)

    def test_malformed_or_duplicate_environment_does_not_echo_payload(self):
        for line in ["sensitive-payload-without-equals", "POSTGRES_DB=another"]:
            values = settings(); values["VPS_ENV_FILE"] += "\n" + line
            with self.assertRaises(deploy.ConfigurationError) as error:
                self.validate(values)
            self.assertNotIn(line, str(error.exception))

    def test_check_validates_real_ssh_files_without_connecting(self):
        with tempfile.TemporaryDirectory() as directory:
            key = Path(directory) / "key"
            subprocess.run(["ssh-keygen", "-q", "-t", "ed25519", "-N", "", "-f", str(key)], check=True)
            values = settings(); values["PATH"] = os.environ["PATH"]
            values["VPS_SSH_KEY"] = key.read_text()
            values["VPS_KNOWN_HOSTS"] = "example.invalid " + key.with_suffix(".pub").read_text()
            execute = subprocess.run
            def local_only(command, **kwargs):
                self.assertEqual(command[0], "ssh-keygen")
                return execute(command, **kwargs)
            with patch.dict(os.environ, values, clear=True), patch("sys.argv", ["deploy_vps.py", "--check"]), patch.object(deploy.subprocess, "run", side_effect=local_only), redirect_stdout(io.StringIO()) as output:
                deploy.main()
            self.assertIn("no VPS connection attempted", output.getvalue())
            self.assertNotIn(values["VPS_SSH_KEY"], output.getvalue())
            values["VPS_KNOWN_HOSTS"] = values["VPS_KNOWN_HOSTS"].replace("example.invalid", "other.invalid")
            with patch.dict(os.environ, values, clear=True), patch("sys.argv", ["deploy_vps.py", "--check"]), redirect_stdout(io.StringIO()):
                with self.assertRaisesRegex(deploy.ConfigurationError, "VPS_KNOWN_HOSTS"):
                    deploy.main()


class ReleaseTests(unittest.TestCase):
    def release(self, existing=False, failure=""):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); release = root / "release"; release.mkdir()
            binary = root / "bin"; binary.mkdir()
            docker = binary / "docker"
            docker.write_text('''#!/usr/bin/env python3
import os,sys
from pathlib import Path
args=sys.argv[1:]
phase='backup' if 'exec' in args else 'migrate' if '--migrate' in args else 'start' if 'up' in args and args[-1]=='app' else 'logs' if 'logs' in args else 'other'
with Path(os.environ['COMMAND_LOG']).open('a') as stream: stream.write(phase+'\\n')
if phase==os.environ.get('FAIL_STAGE') and os.environ['APP_IMAGE']=='new-image': sys.exit(17)
if phase=='backup': print('fixture-backup')
if phase=='logs': print('fixture-service-log')
'''); docker.chmod(0o700)
            (release / "release.sh").write_text((ROOT / "scripts/deploy/release.sh").read_text())
            (release / "bootstrap.env").write_text("")
            if existing:
                (root / "current-image").write_text("old-image\n")
            env = os.environ.copy(); env.update(PATH=str(binary) + os.pathsep + env["PATH"], APP_IMAGE="new-image", DEPLOY_ROOT=str(root), RELEASE_SHA="fixture", COMMAND_LOG=str(root / "commands"), FAIL_STAGE=failure)
            result = subprocess.run(["bash", str(release / "release.sh")], env=env, text=True, capture_output=True)
            phases = (root / "commands").read_text().splitlines()
            image = (root / "current-image").read_text() if (root / "current-image").exists() else None
            backup = (root / "backups/pre-fixture.dump").exists()
            return result, phases, image, backup

    def test_first_deployment_skips_backup(self):
        result, phases, image, backup = self.release()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("backup", phases); self.assertFalse(backup)
        self.assertIn("First deployment", result.stdout); self.assertEqual(image, "new-image\n")

    def test_upgrade_backs_up_before_migration(self):
        result, phases, image, backup = self.release(existing=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(backup); self.assertLess(phases.index("backup"), phases.index("migrate"))
        self.assertEqual(image, "new-image\n")

    def test_backup_failure_logs_phase_and_stops_before_migration(self):
        result, phases, image, _ = self.release(existing=True, failure="backup")
        self.assertEqual(result.returncode, 17)
        self.assertNotIn("migrate", phases); self.assertEqual(image, "old-image\n")
        self.assertIn("FAILED: Back up existing database", result.stdout)
        self.assertIn("fixture-service-log", result.stdout)

    def test_health_failure_collects_diagnostics_and_restores_previous_image(self):
        result, phases, image, _ = self.release(existing=True, failure="start")
        self.assertEqual(result.returncode, 1)
        self.assertEqual(phases.count("start"), 2); self.assertEqual(image, "old-image\n")
        self.assertIn("Rollback to previous image", result.stdout)
        self.assertIn("fixture-service-log", result.stdout)


if __name__ == "__main__":
    unittest.main()
