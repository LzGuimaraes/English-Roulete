# English Roulette — Story Battle

Jogo de compreensão de inglês com histórias narradas, perguntas e decisões de caminho. Stack planejada: .NET 10, ASP.NET Core, Blazor WebAssembly e PostgreSQL.

O núcleo C# já possui navegação entre cenas, avaliação de múltipla escolha e uma sessão Solo Adventure em memória com fases, prazos e comandos idempotentes. **Já existe uma demonstração local no navegador**, em Blazor WebAssembly + ASP.NET Core, com três cenas por percurso e dois finais. A prévia é de leitura: áudio, pontuação e persistência PostgreSQL ainda não estão implementados. Os documentos em `docs/` descrevem o produto planejado; regras propostas continuam sujeitas a validação.

## Experimentar no navegador

Com o SDK .NET 10 instalado:

```sh
dotnet run --project src/EnglishRoulette.Api --configuration Release --launch-profile http
```

Abra **http://localhost:5080** e clique em **Começar minha aventura**. Leia a cena, responda à pergunta e escolha um caminho na livraria. Recarregar a página retoma a sessão enquanto o servidor continuar rodando; reiniciar o servidor perde as sessões em memória.

Consulte [LOCAL_DEMO.md](docs/LOCAL_DEMO.md) para o fluxo de teste, contratos e limites. Os endpoints da demonstração ficam habilitados somente no ambiente `Development`.

## Executar as verificações

Instale o [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` aceita versões estáveis da família 10.0; o SDK 8 não é suficiente.

```sh
dotnet restore EnglishRoulette.sln --locked-mode
dotnet build EnglishRoulette.sln --configuration Release --no-restore
dotnet test EnglishRoulette.sln --configuration Release --no-build
dotnet format EnglishRoulette.sln --verify-no-changes --no-restore
```

O domínio usa apenas APIs nativas do .NET. Os testes usam xUnit e ferramentas de execução/cobertura do template oficial `dotnet new xunit`. As dependências resolvidas estão nos arquivos `packages.lock.json`. O GitHub Actions executa as mesmas verificações em pushes na `main` e pull requests.

## Estrutura atual

```text
src/EnglishRoulette.Domain/          # Conteúdo, regras e sessão Solo Adventure
src/EnglishRoulette.Application/     # Coordenação com relógio do servidor
src/EnglishRoulette.Contracts/       # DTOs públicos sem gabaritos
src/EnglishRoulette.Api/             # Host HTTP e demonstração local em memória
src/EnglishRoulette.Web/             # Interface Blazor WebAssembly
tests/EnglishRoulette.UnitTests/     # Cenas, respostas, prazos, retry e concorrência em memória
docs/                               # Requisitos e decisões técnicas
.github/workflows/ci.yml             # Build, testes e formatação
EnglishRoulette.sln
```

Consulte [DEVELOPMENT.md](docs/DEVELOPMENT.md) para acompanhar os incrementos e [SOLO_SESSION.md](docs/SOLO_SESSION.md) para entender as fases, os comandos, os testes e os limites da sessão atual.

## Ordem de leitura

1. [REQUIREMENTS.md](docs/REQUIREMENTS.md) — modos solo e multiplayer, conteúdo, mídia e critérios de aceite.
2. [ARCHITECTURE.md](docs/ARCHITECTURE.md) — monólito modular, projetos .NET, engine compartilhado, VPS e storage.
3. [DATABASE.md](docs/DATABASE.md) — entidades PostgreSQL, restrições, concorrência e migrations.
4. [GAME_RULES.md](docs/GAME_RULES.md) — estados, cronômetros, pontuação, votos, XP e testes.
5. [CODING_STANDARDS.md](docs/CODING_STANDARDS.md) — padrões C# e desenvolvimento orientado ao aprendizado.

## Primeiro fluxo a completar

**Story → Scene → Narration → Question → Answer → Next Scene** no modo Solo Adventure.

A engine começa como **classes C# testáveis** na solução. A prévia Blazor já consome a sessão em memória via REST. Persistência PostgreSQL, identidade durável de convidado, mídia publicada, pontuação validada e multiplayer serão acrescentados em incrementos seguintes.

## Arquivos de implementação futuros

`API.md` (contratos REST/SignalR) e `DEPLOY.md` (passo a passo da VPS e recuperação) podem ser detalhados quando o primeiro fluxo vertical estiver pronto. Não são necessários para iniciar o domínio.
