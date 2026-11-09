#!/usr/bin/env python3
"""Import an authored JSON story through the admin API as a draft. No SQL or AI runtime."""
import argparse
import copy
import getpass
from http.cookiejar import CookieJar
import json
import mimetypes
import secrets
import os
from pathlib import Path
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import build_opener, HTTPCookieProcessor, HTTPRedirectHandler, Request
from uuid import UUID

EMPTY_ID = "00000000-0000-0000-0000-000000000000"
SCENE_FIELDS = ["key", "title", "narration", "isEnding", "prompt", "options", "correctOptionKey", "explanation", "transitions"]
METADATA_FIELDS = ["title", "synopsis", "genre", "difficulty", "author", "licenseType", "licenseEvidence", "attribution", "appearance", "language"]


def load_story(path):
    if path.stat().st_size > 1024 * 1024:
        raise ValueError("Use um JSON de até 1 MB.")
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict) or not isinstance(document.get("metadata"), dict):
        raise ValueError("O JSON precisa de metadata e scenes.")
    if document["metadata"].get("difficulty") not in {"A1", "A2", "B1", "B2", "C1"}:
        raise ValueError("Use difficulty A1, A2, B1, B2 ou C1.")
    if document["metadata"].get("language", "en") not in {"en", "pt-BR"}:
        raise ValueError("Use language en ou pt-BR.")
    scenes = document.get("scenes")
    if not isinstance(scenes, list) or not 1 <= len(scenes) <= 40:
        raise ValueError("Use uma lista scenes com 1 a 40 cenas.")
    if any(not isinstance(scene, dict) or any(field not in scene for field in SCENE_FIELDS) for scene in scenes):
        raise ValueError("Há uma cena incompleta. Confira o modelo de autoria.")
    keys = [scene["key"] for scene in scenes]
    if any(not isinstance(key, str) for key in keys) or len(set(keys)) != len(keys) or document.get("initialSceneKey") not in keys:
        raise ValueError("Use chaves de cena únicas e uma initialSceneKey existente.")
    return document


def editor_payload(document, draft):
    if draft.get("status") != "Draft":
        raise ValueError("A versão precisa ser um rascunho. Duplique versões publicadas pelo painel.")
    result = copy.deepcopy(draft)
    result["metadata"].update({key: value for key, value in document["metadata"].items() if key in METADATA_FIELDS})
    result["initialSceneKey"] = document["initialSceneKey"]
    previous = {scene["key"]: scene for scene in draft["scenes"]}
    result["scenes"] = []
    for source in document["scenes"]:
        old = previous.get(source["key"], {})
        scene = {field: source[field] for field in SCENE_FIELDS}
        for field in ("vocabulary", "dialogue"):
            if field in source:
                scene[field] = copy.deepcopy(source[field])
            elif field in old:
                scene[field] = copy.deepcopy(old[field])
        scene.update(id=old.get("id", EMPTY_ID), audio=old.get("audio"), visual=old.get("visual"))
        result["scenes"].append(scene)
    return result


def load_media(path, document):
    if path is None:
        return {}
    manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(manifest, dict):
        raise ValueError("O manifesto de mídia precisa ser um objeto por chave de cena.")
    root = path.parent.resolve()
    result = {}
    for scene in document["scenes"]:
        key = scene["key"]
        definition = manifest.get(key, {})
        if not isinstance(definition, dict):
            raise ValueError(f"Definição de mídia inválida para {key}.")
        result[key] = {}
        for kind, limit in [("audio", 10 * 1024 * 1024), ("visual", 8 * 1024 * 1024)]:
            asset = definition.get(kind)
            if not isinstance(asset, dict) or any(not isinstance(asset.get(field), str) or not asset[field].strip() for field in ["file", "attribution", "licenseEvidence"]):
                raise ValueError(f"Mídia {kind} de {key}: informe file, attribution e licenseEvidence.")
            file = (root / asset["file"]).resolve()
            if not file.is_relative_to(root) or not file.is_file() or not 0 < file.stat().st_size <= limit:
                raise ValueError(f"Mídia {kind} de {key}: arquivo ausente, fora da pasta do pacote ou acima do limite.")
            if kind == "audio" and file.suffix.lower() != ".wav":
                raise ValueError(f"Áudio de {key}: use WAV PCM de 16 bits, até 2 minutos.")
            if kind == "visual" and (not isinstance(asset.get("altText"), str) or not asset["altText"].strip()):
                raise ValueError(f"Imagem/animação de {key}: informe altText.")
            result[key][kind] = dict(asset, path=file)
    return result


def multipart(asset, kind):
    boundary = "roulette-" + secrets.token_hex(16)
    parts = []
    fields = {"attribution": asset["attribution"], "licenseEvidence": asset["licenseEvidence"]}
    if kind == "visual":
        fields["altText"] = asset["altText"]
    for name, value in fields.items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    content_type = "audio/wav" if kind == "audio" else mimetypes.guess_type(asset["path"].name)[0] or "application/octet-stream"
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="asset"\r\nContent-Type: {content_type}\r\n\r\n'.encode())
    parts.extend([asset["path"].read_bytes(), f'\r\n--{boundary}--\r\n'.encode()])
    return b"".join(parts), "multipart/form-data; boundary=" + boundary


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class AdminApi:
    def __init__(self, base_url):
        parsed = urlsplit(base_url)
        if parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in {"", "/"}:
            raise ValueError("Use somente a origem do site, sem credenciais, caminho ou query.")
        if not parsed.hostname or not (parsed.scheme == "https" or parsed.scheme == "http" and parsed.hostname in {"localhost", "127.0.0.1", "::1"}):
            raise ValueError("Use HTTPS; HTTP é permitido apenas para o ambiente local.")
        self.base = base_url.rstrip("/")
        self.http = build_opener(HTTPCookieProcessor(CookieJar()), NoRedirect())

    def request(self, method, path, body=None, content_type=None):
        headers = {"Accept": "application/json"}
        if method != "GET":
            headers["X-CSRF-TOKEN"] = self.request("GET", "/api/admin/antiforgery")["token"]
        data = body if isinstance(body, bytes) else None if body is None else json.dumps(body, ensure_ascii=False).encode("utf-8")
        if data is not None:
            headers["Content-Type"] = content_type or "application/json"
        try:
            with self.http.open(Request(self.base + path, data=data, method=method, headers=headers), timeout=30) as response:
                content = response.read()
                return json.loads(content) if content else None
        except HTTPError as error:
            try:
                problem = json.loads(error.read())
                message = problem.get("detail") or problem.get("title") or "Operação recusada."
            except (ValueError, AttributeError):
                message = "Operação recusada. Confira a URL, autenticação e o estado da API."
            raise ValueError(f"HTTP {error.code}: {message}") from None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("file", type=Path)
    parser.add_argument("--media-manifest", type=Path, help="Pacote de WAV e GIF/APNG por cena, com créditos e direitos.")
    parser.add_argument("--check", action="store_true", help="Conferir o formato local, sem conectar ou gravar.")
    parser.add_argument("--url", help="Origem HTTPS da aplicação ou http://localhost:5080.")
    parser.add_argument("--email", default=os.environ.get("ENGLISHROULETTE_ADMIN_EMAIL"))
    parser.add_argument("--version-id", type=UUID, help="Atualizar um rascunho existente em vez de criar outra história.")
    args = parser.parse_args()
    document = load_story(args.file)
    media = load_media(args.media_manifest, document)
    if args.check:
        print(f"Formato local conferido: {len(document['scenes'])} cenas, nível {document['metadata']['difficulty']}. Grafo e publicação continuam validados pelo servidor.")
        if media:
            print("Arquivos do pacote encontrados. Formato real, duração e animação serão verificados na API.")
        return
    if not args.url or not args.email:
        parser.error("Informe --url e --email para importar.")
    api = AdminApi(args.url)
    password = os.environ.get("ENGLISHROULETTE_ADMIN_PASSWORD")
    if not password:
        if not sys.stdin.isatty():
            raise ValueError("Execute em um terminal para informar a senha, ou defina ENGLISHROULETTE_ADMIN_PASSWORD no ambiente.")
        password = getpass.getpass("Senha de administrador: ")
    api.request("POST", "/api/admin/login", {"email": args.email, "password": password})
    draft = api.request("GET", f"/api/admin/stories/{args.version_id}") if args.version_id else api.request("POST", "/api/admin/stories", {})
    print("Versão de rascunho: " + draft["id"], flush=True)
    print("Para retomar esta edição, use --version-id " + draft["id"], flush=True)
    draft = api.request("PUT", "/api/admin/stories/" + draft["id"], editor_payload(document, draft))
    for key, assets in media.items():
        for kind in ["audio", "visual"]:
            scene = next(scene for scene in draft["scenes"] if scene["key"] == key)
            content, content_type = multipart(assets[kind], kind)
            path = f"/api/admin/stories/{draft['id']}/scenes/{scene['id']}/{kind}?revision={draft['revision']}"
            draft = api.request("POST", path, content, content_type)
            current = next(scene for scene in draft["scenes"] if scene["key"] == key)
            if kind == "visual" and assets[kind].get("animated") and not current["visual"]["animated"]:
                raise ValueError(f"A mídia visual de {key} foi salva, mas não é uma animação. Substitua o arquivo e retome o rascunho.")
            print(f"{key}: {kind} enviado.")
    print(f"Rascunho salvo: {len(draft['scenes'])} cenas. Abra {api.base}/admin para revisar e adicionar mídia.")
    check = api.request("GET", "/api/admin/stories/" + draft["id"] + "/validation")
    for issue in check["issues"]:
        print("Pendência: " + issue)
    print("A importação não publica a história. Revise conteúdo, áudio e direitos pelo painel.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, URLError) as error:
        print("Erro: " + str(error), file=sys.stderr)
        raise SystemExit(1) from None
