# English Roulette — Story Battle

Jogo de compreensão de inglês com histórias narradas, perguntas e decisões de caminho. Stack planejada: .NET 10, ASP.NET Core, Blazor WebAssembly e PostgreSQL.

**Já existe um jogo local com login administrativo, catálogo editorial, narração e progresso salvo no PostgreSQL**, em Blazor WebAssembly + ASP.NET Core, com três cenas por percurso e dois finais. O motor valida cenas, respostas, prazos e comandos idempotentes; a aventura pode ser retomada após reiniciar a API. Pontuação e XP ainda dependem de validação. Os documentos em `docs/` descrevem o produto planejado; nem todo requisito já foi implementado.

## Experimentar no navegador

Requer SDK .NET 10, Docker Compose, Python 3 e `libespeak-ng1`/`espeak-ng-data` para gerar a narração de demonstração no Linux. A geração é feita antes da execução; a API nunca sintetiza voz durante a partida.

```sh
python3 scripts/dev.py setup
python3 scripts/dev.py run
```

Abra **http://localhost:5080** e clique em **Começar minha aventura**. Depois escolha **Ouvir e iniciar cena**: ouça a narração, responda e escolha um caminho na livraria. Recarregar a página ou reiniciar a API preserva o progresso enquanto o cookie do navegador e o volume PostgreSQL forem mantidos. O prazo de uma pergunta aberta continua correndo.

`setup` cria credenciais locais fora do Git, inicia somente o PostgreSQL deste projeto em `127.0.0.1:55432`, aplica as migrations, provisiona a conta administrativa local e gera os arquivos WAV em `media/` (ignorado pelo Git). Não altera o PostgreSQL do sistema. Caso o SDK 10 não seja o padrão, configure `DOTNET_HOST_PATH` com o caminho do executável adequado.

Consulte [LOCAL_DEMO.md](docs/LOCAL_DEMO.md) para o fluxo de teste, contratos e limites. As rotas do jogo e do catálogo funcionam em todos os ambientes; a rota legada de áudio por hash fica restrita a `Development`.

## Administrar histórias

Abra **http://localhost:5080/admin**. Use `ADMIN_EMAIL` e `ADMIN_PASSWORD` do arquivo privado `infra/.env`. O painel permite criar histórias, cenas, perguntas, caminhos, narrações, imagens/animações e temas visuais, simular o percurso e publicar versões sem alterar o código. Conteúdo publicado é imutável; duplique para editar uma nova versão.

Para atualizar um ambiente existente, execute `python3 scripts/dev.py migrate` e `python3 scripts/dev.py admin`. Consulte [ADMIN.md](docs/ADMIN.md) para acesso, recuperação de senha, publicação e limites de áudio.

## Hospedar em VPS

A esteira do GitHub Actions verifica o código, publica a imagem no GHCR e pode fazer deploy por SSH com Docker Compose e Traefik. O deploy fica desativado até configurar `DEPLOY_ENABLED=true` e os secrets. Veja [DEPLOY.md](docs/DEPLOY.md) e [infra/.env.production.example](infra/.env.production.example) para o modelo completo de variáveis, domínio, chave SSH, volumes e recuperação.

## Início do multiplayer

O [lobby multiplayer](docs/MULTIPLAYER.md) está em **http://localhost:5080/multiplayer**: convidados, salas de 2 a 8, convite, prontidão, escolha de história e sincronização SignalR. A partida simultânea com respostas, votação e ranking é o próximo incremento.

## Executar as verificações

Instale o [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` aceita versões estáveis da família 10.0; o SDK 8 não é suficiente.

```sh
dotnet restore EnglishRoulette.sln --locked-mode
dotnet build EnglishRoulette.sln --configuration Release --no-restore
dotnet test EnglishRoulette.sln --configuration Release --no-build
dotnet format EnglishRoulette.sln --verify-no-changes --no-restore
```

O domínio usa apenas APIs nativas do .NET. Os testes usam xUnit e ferramentas de execução/cobertura do template oficial `dotnet new xunit`. As dependências resolvidas estão nos arquivos `packages.lock.json`. O GitHub Actions executa as mesmas verificações em pushes na `main` e pull requests.

Para incluir os testes PostgreSQL localmente após `setup`, execute `python3 scripts/dev.py test`. Sem `ENGLISHROULETTE_TEST_CONNECTION`, os testes que exigem banco são exibidos como skipped; a CI configura um PostgreSQL real e os executa.

## Estrutura atual

```text
src/EnglishRoulette.Domain/          # Conteúdo, regras e sessão Solo Adventure
src/EnglishRoulette.Application/     # Coordenação, contratos e validação editorial
src/EnglishRoulette.Contracts/       # DTOs de jogo e contratos editoriais restritos a ADMIN
src/EnglishRoulette.Infrastructure/  # EF Core, PostgreSQL, migrations e validação WAV
src/EnglishRoulette.Api/             # Host HTTP, credenciais opacas e mídia publicada
src/EnglishRoulette.Web/             # Interface Blazor WebAssembly
tests/EnglishRoulette.UnitTests/     # Cenas, respostas, prazos, retry e concorrência em memória
tests/EnglishRoulette.IntegrationTests/ # Concorrência, retomada e constraints em PostgreSQL real
infra/                              # PostgreSQL local isolado
scripts/                            # Preparação do ambiente e narração pré-gravada
docs/                               # Requisitos e decisões técnicas
.github/workflows/deploy.yml         # Build, testes, imagem e deploy na VPS
EnglishRoulette.sln
```

Consulte [DEVELOPMENT.md](docs/DEVELOPMENT.md), [SOLO_SESSION.md](docs/SOLO_SESSION.md) e [PERSISTENCE.md](docs/PERSISTENCE.md) para entender o código e os limites atuais.

## Ordem de leitura

1. [REQUIREMENTS.md](docs/REQUIREMENTS.md) — modos solo e multiplayer, conteúdo, mídia e critérios de aceite.
2. [ARCHITECTURE.md](docs/ARCHITECTURE.md) — monólito modular, projetos .NET, engine compartilhado, VPS e storage.
3. [DATABASE.md](docs/DATABASE.md) — entidades PostgreSQL, restrições, concorrência e migrations.
4. [GAME_RULES.md](docs/GAME_RULES.md) — estados, cronômetros, pontuação, votos, XP e testes.
5. [CODING_STANDARDS.md](docs/CODING_STANDARDS.md) — padrões C# e desenvolvimento orientado ao aprendizado.

## Primeiro fluxo a completar

**Story → Scene → Narration → Question → Answer → Next Scene** no modo Solo Adventure.

A engine permanece independente da interface. A prévia Blazor consome a sessão via REST, o servidor persiste checkpoints e o navegador reproduz arquivos WAV publicados. Login administrativo e catálogo editorial já estão disponíveis. Pontuação aprovada, progressão de jogadores e a partida multiplayer completa continuam nos próximos incrementos.

## Arquivos de implementação futuros

`API.md` (contratos REST/SignalR) pode ser detalhado conforme os próximos incrementos. O passo a passo de VPS e recuperação já está em [DEPLOY.md](docs/DEPLOY.md).
