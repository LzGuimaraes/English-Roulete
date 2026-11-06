# English Roulette — Story Battle

Jogo de compreensão de inglês com histórias narradas, perguntas e decisões de caminho. Stack planejada: .NET 10, ASP.NET Core, Blazor WebAssembly e PostgreSQL.

O desenvolvimento começou pelo núcleo C# independente de infraestrutura: navegação entre cenas, validação estrutural do grafo e avaliação de múltipla escolha. **Ainda não existe aplicação jogável, API, persistência ou interface.** Os documentos em `docs/` descrevem o produto planejado; regras propostas continuam sujeitas a validação.

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
src/EnglishRoulette.Domain/          # Conteúdo imutável e regras puras
tests/EnglishRoulette.UnitTests/     # Fluxo de cenas e avaliação de respostas
docs/                               # Requisitos e decisões técnicas
.github/workflows/ci.yml             # Build, testes e formatação
EnglishRoulette.sln
```

Consulte [DEVELOPMENT.md](docs/DEVELOPMENT.md) para entender as classes, os testes, os limites deste incremento e os próximos passos.

## Ordem de leitura

1. [REQUIREMENTS.md](docs/REQUIREMENTS.md) — modos solo e multiplayer, conteúdo, mídia e critérios de aceite.
2. [ARCHITECTURE.md](docs/ARCHITECTURE.md) — monólito modular, projetos .NET, engine compartilhado, VPS e storage.
3. [DATABASE.md](docs/DATABASE.md) — entidades PostgreSQL, restrições, concorrência e migrations.
4. [GAME_RULES.md](docs/GAME_RULES.md) — estados, cronômetros, pontuação, votos, XP e testes.
5. [CODING_STANDARDS.md](docs/CODING_STANDARDS.md) — padrões C# e desenvolvimento orientado ao aprendizado.

## Primeiro fluxo a completar

**Story → Scene → Narration → Question → Answer → Next Scene** no modo Solo Adventure.

A engine começa como **classes C# testáveis** na solução. Depois de existir uma aventura solo funcional, a mesma lógica de cena/pergunta será aproveitada no multiplayer com SignalR. API, identidade de convidado, deadlines, persistência PostgreSQL, mídia e Blazor serão acrescentados em incrementos seguintes.

## Arquivos de implementação futuros

`API.md` (contratos REST/SignalR) e `DEPLOY.md` (passo a passo da VPS e recuperação) podem ser detalhados quando o primeiro fluxo vertical estiver pronto. Não são necessários para iniciar o domínio.
