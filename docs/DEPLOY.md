# VPS com Docker Compose e GitHub Actions

O workflow `.github/workflows/deploy.yml` executa restore travado, build Release, testes PostgreSQL e formatação. Na `main`, publica a imagem em `ghcr.io/lzguimaraes/english-roulete` com tag do commit e usa seu **digest imutável** no deploy. Pull requests executam somente verificações.

O job de deploy requer `DEPLOY_ENABLED=true` nas **Repository variables** e usa o Environment `production`. Enquanto essa variável não for definida, testes e imagem funcionam sem acessar a VPS. Proteções/aprovações do Environment podem ser configuradas no GitHub conforme seu processo.

## Configuração no GitHub

Em **Settings → Secrets and variables → Actions**, adicione:

| Secret | Modelo / finalidade |
|---|---|
| `VPS_HOST` | `203.0.113.10` — IP ou hostname da VPS |
| `VPS_USER` | `deploy` — usuário SSH com acesso ao Docker e ao diretório de deploy |
| `VPS_SSH_KEY` | Conteúdo completo da chave privada SSH dedicada ao deploy |
| `VPS_SSH_PASSPHRASE` | Passphrase da chave privada; deixe ausente se a chave não for protegida |
| `VPS_KNOWN_HOSTS` | Linha completa de known_hosts da VPS, verificada por canal confiável |
| `VPS_ENV_FILE` | Conteúdo completo do exemplo abaixo, com valores reais |
| `VPS_REGISTRY_TOKEN` | Opcional: token GitHub com `read:packages` para imagem privada |

| Variable | Modelo |
|---|---|
| `DEPLOY_ENABLED` | `false` durante preparação; `true` para habilitar deploy |
| `VPS_SSH_PORT` | `22` |
| `VPS_DEPLOY_PATH` | `/opt/englishroulette` |
| `VPS_REGISTRY_USER` | `LzGuimaraes` — necessário quando usar `VPS_REGISTRY_TOKEN` |

`GITHUB_TOKEN` é fornecido automaticamente pelo Actions para publicar a imagem. Não crie um secret com esse nome. Pacotes GHCR novos podem ser privados: torne o pacote público ou configure o token de leitura da VPS. O script faz login por stdin; o token não entra em argumentos de shell.

Modelo de **`VPS_ENV_FILE`** (também em `infra/.env.production.example`):

```dotenv
APP_DOMAIN=english.seudominio.com.br
TRAEFIK_NETWORK=traefik-public
TRUSTED_PROXY_CIDR=172.20.0.0/16
TRAEFIK_ENTRYPOINT=websecure
TRAEFIK_CERT_RESOLVER=letsencrypt
POSTGRES_DB=englishroulette
POSTGRES_USER=englishroulette
POSTGRES_PASSWORD=SUBSTITUA_POR_SENHA_HEXADECIMAL_ALEATORIA
ADMIN_EMAIL=admin@seudominio.com.br
ADMIN_PASSWORD=SUBSTITUA_POR_SENHA_FORTE
```

Gere `POSTGRES_PASSWORD` com `openssl rand -hex 32`. Não reutilize a senha local. A senha administrativa precisa cumprir a política do painel (12 caracteres, maiúscula, minúscula, número, símbolo e cinco caracteres distintos). Use uma linha por valor e sem aspas externas no secret; o deploy trata a senha de bootstrap como dado, sem interpolá-la em comandos.

## Preparar a VPS uma vez

1. Instale Docker Engine, o plugin Docker Compose com suporte a `up --wait` e OpenSSH. Use arquitetura **Linux amd64**, padrão da imagem publicada por este workflow.
2. Crie o usuário de deploy e autorize sua chave pública em `~/.ssh/authorized_keys`. Dê acesso ao Docker e propriedade de `/opt/englishroulette`. O acesso ao Docker permite administrar o host; use uma conta dedicada.
3. Configure o DNS de `APP_DOMAIN` para o IP da VPS.
4. Use o **Traefik existente**, com TLS, entrypoint e cert resolver informados no secret. O Compose não cria outro proxy nem ocupa portas 80/443. A rede externa precisa existir e conter o Traefik.
5. Consulte o CIDR dessa rede com `docker network inspect NOME_DA_REDE`. Informe o valor real em `TRUSTED_PROXY_CIDR`; a API só confia nos cabeçalhos encaminhados por essa rede. Não use um CIDR público genérico.
6. Obtenha a chave de host SSH pela console da VPS ou outro canal confiável. Uma linha de `ssh-keyscan -p PORTA HOST` precisa ter sua impressão digital conferida; para porta diferente de 22, known_hosts usa `[HOST]:PORTA`. O script exige `StrictHostKeyChecking=yes`.
7. Configure secrets e variables, então mude `DEPLOY_ENABLED` para `true` e execute o workflow manualmente na `main` ou envie um novo commit.

Banco e API usam somente redes Docker; nenhuma porta de banco/API é publicada pelo Compose de produção. Traefik entrega o frontend, a API, mídia e `/hubs/rooms` na mesma origem HTTPS/WSS. Não é necessário configurar CORS permissivo.

## Sequência e recuperação

`scripts/deploy_vps.py` valida o destino, cria arquivos temporários privados no runner e copia somente Compose, script e configuração para `<VPS_DEPLOY_PATH>/releases/<commit>`. A chave privada fica no runner e é apagada no encerramento do diretório temporário. O checkout inteiro e a mídia local não são enviados.

`release.sh` baixa a imagem por digest e inicia PostgreSQL. Quando existe `current-image`, faz um `pg_dump` antes das migrations; o primeiro deploy pula esse backup. Executa a imagem com `--migrate` como um comando separado. Esse comando rejeita operações marcadas como destrutivas pelo EF e SQL arbitrário; migrations desse tipo exigem revisão e aplicação explícita antes do deploy. Migrations não rodam durante o startup normal.

Depois, provisiona ADMIN com `--bootstrap-admin`, sem resetar senha de usuário existente e **sem sintetizar/importar conteúdo demo na produção**. O primeiro catálogo estará vazio; cadastre histórias pelo `/admin`. Não precisa instalar eSpeak na imagem de produção.

Por último, `docker compose up --wait` exige readiness do banco e migrations. Se a nova aplicação não ficar saudável, o script tenta voltar ao digest anterior e mantém o banco no estado migrado. Isso exige migrations compatíveis com a versão anterior; rollback de imagem não desfaz alterações de dados. `current-image` e `current-release` registram a última versão saudável.

Os arquivos de configuração da release ficam em diretório privado do usuário. Não imprima `docker compose config` com secrets reais nem versione esses arquivos. Trocar `ADMIN_PASSWORD` não substitui a senha existente: use o painel ou o comando explícito de recuperação. Alterar `POSTGRES_PASSWORD` no secret também não altera automaticamente a senha de um banco já inicializado.

## Volumes e backup

O projeto Compose `englishroulette-production` mantém três volumes: PostgreSQL, mídia e chaves Data Protection. Recriar o container preserva os dados. Não execute `down -v` em produção. A API usa o usuário sem privilégios `app` da imagem .NET.

Os dumps pré-deploy ficam em `<VPS_DEPLOY_PATH>/backups/`. Configure também backup diário **fora da VPS** para banco, mídia e chaves, com retenção e teste de restauração. Os dumps de deploy não substituem esse agendamento. A esteira não remove backups, mídias ou releases antigas automaticamente.

Acompanhe a aplicação em `https://APP_DOMAIN/health/ready`, `/admin` e `/`. O healthcheck interno envia o Host configurado para funcionar com `AllowedHosts` restrito.

## Logs e diagnóstico

O Actions apresenta etapas nomeadas de restore, build, testes, build/publicação da imagem, validação de configuração e deploy. O build Docker inclui resumo e registro para download com retenção de sete dias. Falhas mostram o estado do builder e o uso de disco do runner.

Antes de conectar, `python3 scripts/deploy_vps.py --check` valida presença e formato dos secrets, os campos obrigatórios de `VPS_ENV_FILE` (incluindo `POSTGRES_DB` e `POSTGRES_USER`), porta SSH, domínio sem protocolo/caminho, CIDR, senha administrativa, chave privada e entrada de known_hosts. Os logs mostram nomes e estado de configuração, sem imprimir os valores. Senhas dentro do secret multilinha também são registradas no mascaramento do GitHub antes dos comandos remotos.

Na VPS, cada etapa registra horário e nome: configuração Compose, download, PostgreSQL, backup, migrations, administrador e healthcheck. Em falhas, coleta status, últimas 80 linhas dos serviços e somente os campos de estado/saúde dos containers. Não executa dump de ambiente, `set -x`, `docker compose config` completo ou `docker inspect` completo. Mensagens da aplicação devem continuar sem credenciais; o mascaramento é uma proteção adicional.

`Permission denied (publickey,password)` indica autenticação SSH recusada. Confira `VPS_USER` e se a chave pública correspondente a `VPS_SSH_KEY` está no `~/.ssh/authorized_keys` desse usuário. A chave privada deve ser válida e ter quebras de linha reais. Se for protegida, cadastre sua passphrase em `VPS_SSH_PASSPHRASE`. O script desbloqueia somente a cópia temporária no runner usando SSH_ASKPASS; a passphrase fica no ambiente e não é passada nos argumentos ou gravada no helper. A chave original permanece protegida. O deploy não consegue corrigir a autorização da VPS quando o acesso SSH já foi recusado.

## Validação e fontes

A imagem é montada por `Dockerfile` em dois estágios e `.dockerignore` exclui `.env`, mídia, chaves, Git e resultados de build. O processo de publicação executa o trimming do Blazor; valide também o fluxo de login e upload na imagem publicada.

Referências oficiais: [Docker e GitHub Actions](https://docs.docker.com/build/ci/github-actions/), [GHCR no GitHub Actions](https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images), [Traefik com Docker](https://doc.traefik.io/traefik/master/reference/routing-configuration/other-providers/docker/) e [proxies no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer).
