# English Roulette — Arquitetura Técnica

> **Versão:** 0.1 — proposta para validação | **Data:** 06/11/2026  
> Documento complementar a `REQUIREMENTS.md` v1.3. O foco é começar simples em uma VPS e manter um caminho claro para crescimento.

## 1. Decisão arquitetural

**Monólito modular** ASP.NET Core + frontend independente Blazor WebAssembly + PostgreSQL. Uma instância do backend na V1. Tanto Single Player quanto Multiplayer reutilizam **um único motor de narrativa** (cenas, perguntas, eventos e grafo de decisões), com políticas diferentes para cronômetro, pontuação e escolha de caminhos.

```text
Navegador (Blazor WebAssembly; PWA online)
            │ HTTPS
            ▼
       Traefik existente
        │      │      │
        │      │      └── /media/* → assets publicados, HTTP cache/Range
        │      └───────── /api/*   → ASP.NET Core REST (Solo + Admin + salas)
        └──────────────── /hubs/*  → SignalR (multiplayer)
                                  │
               ┌──────────────────┴───────────────────┐
               │ ASP.NET Core (uma instância)         │
               │ - Auth convidado/admin               │
               │ - Game Engine / lógica de domínio    │
               │ - Estado e contratos                 │
               │ - Conteúdo e publicação              │
               │ - MediaStorage abstraído             │
               └──────────────┬───────────────────────┘
                        ┌─────┴──────┐
                        ▼            ▼
                  PostgreSQL      Media Storage
                    (dados)       (áudio/imagens)
```

**Não usar nesta fase:** microserviços, Kafka, Kubernetes, Redis ou CQRS/event sourcing por padrão. Projetar dependências para permitir extraí-los quando necessário, não implantá-los antecipadamente.

## 2. Módulos lógicos

| Módulo | Responsabilidade |
|---|---|
| Identity | Convidado, credenciais, administrador, autorização |
| Catalog | Gêneros, histórias, versões, cenas e ramificações |
| Content | Perguntas, alternativas, níveis e revisão editorial |
| Media | Upload, validação, armazenamento, URLs de entrega, metadados |
| GameEngine | Estados, transições, avaliação, seleção e eventos |
| Solo | Adventure, Practice, salvar/continuar, recordes pessoais |
| Multiplayer | Salas, participantes, ready, deadlines compartilhados, SignalR |
| Progression | Pontos de sessão, XP, conquistas e estatísticas |
| Operations | Logs, health checks, backups e administração |

A modularidade é **de código e responsabilidades**; não requer serviço separado para cada módulo.

## 3. Projetos e dependências

```text
EnglishRoulette.Domain          # Entidades, Value Objects, regras puras, enums
EnglishRoulette.Application     # Casos de uso e portas/interfaces necessárias
EnglishRoulette.Infrastructure  # EF Core, PostgreSQL, MediaStorage, integrações
EnglishRoulette.Api             # HTTP, SignalR, DI, auth, OpenAPI
EnglishRoulette.Contracts       # DTOs compartilhados públicos; nunca entidades EF
EnglishRoulette.Web             # Blazor WASM, telas, cliente HTTP e Hub
EnglishRoulette.UnitTests
EnglishRoulette.IntegrationTests
```

Regras de referência: `Domain` não conhece EF, ASP.NET, SignalR ou Blazor; `Application` depende de `Domain`; `Infrastructure` implementa abstrações necessárias; `Api` compõe aplicações e infraestrutura; `Web` consome contratos públicos. **Não compartilhar gabaritos ou entidades internas no `Contracts` exposto ao cliente.**

## 4. Motor compartilhado de jogo

- `GameSession` possui `Mode = SOLO_ADVENTURE | SOLO_PRACTICE | MULTIPLAYER`, referência à `StoryVersion` e `RoomId` nulo somente no solo.
- `SceneFlowService`: resolve cena inicial, transição válida, cena final e percurso.
- `ChallengeService`: escolhe questão aplicável, valida resposta e retorna explicação somente após a fase correta.
- `EventEligibilityService`: seleciona apenas eventos compatíveis com modo, cena, nível e histórico recente.
- `ScoringService`: **política por modo** (solo Adventure: acerto + faixas de tempo; solo Practice: estudo sem disputa; multiplayer: ordem de acertos por servidor).
- `SessionCoordinator`: valida e executa uma transição única com controle de concorrência; persiste antes de emitir notificação.
- `ModeRules` ou enum + estratégias pequenas: introduzir interfaces somente quando regras realmente divergirem; não duplicar o engine.

**No solo:** comandos via API REST e snapshots no PostgreSQL para a página em andamento, sem WebSocket. Por decisão do responsável em 08/11/2026, a aventura não é retomada ao sair/recarregar: a credencial é mantida somente na memória da página e o retorno ao site exige nova aventura. **No multiplayer:** comandos validados pelo backend e notificações de estado via SignalR (grupos por sala; reconectar solicita snapshot).

### Concorrência e idempotência

- Todo comando de resposta/voto contém `RequestId` único e identidade autenticada.
- Chaves únicas no PostgreSQL para `PlayerAnswer(QuestionInstanceId, PlayerId)` e votos/escolhas aplicáveis.
- Comparar `StateVersion` e fase atual; avançar apenas uma vez por transação e rejeitar estado obsoleto.
- Registrar pontos/XP em transação; retry não produz créditos duplicados.
- Ao reiniciar a API, reconstruir sessões ativas a partir do banco; finalizar explicitamente como `CANCELLED` se a fase não puder ser retomada com segurança.

## 5. Imagens e áudios: onde armazenar

**Decisão para V1:** armazenamento no **volume persistente da VPS**, fora do código, da imagem Docker e do banco. PostgreSQL guarda **somente metadados e referências**. O contrato `IMediaStorage` deve permitir migrar futuramente para armazenamento de objetos S3-compatível sem alterar `StoryScene`, os componentes Blazor ou a regra editorial.

```text
/var/lib/englishroulette/media/    # Host VPS; backup separado
├── audio/2026/11/<uuid>.mp3
├── images/2026/11/<uuid>.webp
└── private/drafts/<uuid>            # não expor em /media público
```

**Dados de `MediaAsset`:** `Id`, `Kind`, `StorageProvider`, `StorageKey`, `ContentType`, `SizeBytes`, `Sha256`, `DurationMs?`, `Width?`, `Height?`, `License`, `Attribution`, `CreatedAt`, `PublishedAt?`. A cena e a versão publicada referenciam o `AssetId`; não persistir domínio/URL absoluto como chave principal.

### Upload de conteúdo

1. Admin autenticado inicia o upload, com limites por tipo/tamanho e rate limit.
2. API valida extensão, MIME e **assinatura/estrutura real do arquivo** quando possível; para mídia, executar probe seguro de duração/dimensões. Nomes físicos são gerados pelo servidor.
3. Gravar primeiramente em área privada temporária, calcular `SHA-256`, persistir metadados e validar licença/atribuição.
4. Ao publicar uma versão, verificar que todos os assets existem e são compatíveis; mover/copiar para armazenamento publicado sem sobrescrever assets imutáveis.
5. Entregar apenas assets publicados com `Content-Type` correto, `Content-Length`, `Accept-Ranges`/HTTP Range para áudio, cache com URL versionada e HTTPS. Não permitir execução de arquivos enviados.
6. Evitar limpar arquivos referenciados por versões ou sessões históricas; limpeza somente após verificação de referências e política de retenção.

A documentação da Microsoft recomenda armazenamento dedicado e nomes gerados pelo servidor para uploads: https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads

### Para escalar depois

Quando tráfego/armazenamento justificar, implementar `S3MediaStorage` e migrar os objetos com verificação por hash. Manter PostgreSQL para metadados. Opcionalmente, usar CDN e URLs com expiração para material privado. A aplicação **não deve retransmitir o conteúdo de áudio por SignalR**.

**Direitos autorais:** somente publicar obras e materiais com autorização suficiente para usar, adaptar, narrar e distribuir; guardar evidências por versão e mídia.

## 6. Regras de sincronização do áudio

- Uma gravação por **cena/versão**, preferencialmente MP3; o frontend pré-carrega antes da rodada.
- Navegador precisa de gesto do usuário para liberar reprodução; mostrar botão `Enable audio`.
- Multiplayer: o servidor comunica `startsAtUtc`, mas cada cliente reproduz o arquivo localmente. Admitir pequenas diferenças de sincronização e margem após narração.
- Solo: jogador controla o play/pause em Practice; em Adventure, regras de cronômetro e retomada são validadas pelo backend.
- Áudio e imagem podem ser cacheados pelo navegador conforme cabeçalhos e licenças; não confundir PWA com modo offline na V1.

## 7. Banco, operações e VPS

- **PostgreSQL + EF Core/Npgsql**; usar migrations revisadas e compatíveis.
- VPS com `web`, `api`, PostgreSQL opcional em container e **Traefik já existente**; não criar proxy adicional na porta 80/443.
- API recebe apenas portas internas; PostgreSQL nunca expõe `5432` na internet.
- Secrets fora do Git; configurados por ambiente. HTTPS/WSS; cookies seguros/CSRF conforme mecanismo de autenticação.
- Backup diário de PostgreSQL e backup independente das mídias **fora da VPS**. Fazer teste de restauração.
- Logs estruturados e health endpoints (`/health/live`, `/health/ready`); métricas básicas de CPU, RAM, espaço e conexões.
- CI: build, testes, imagens Docker e deploy controlado; não executar migrations destrutivas indiscriminadamente.
- **Escalabilidade V1:** uma instância da API. Se futuramente houver múltiplas instâncias, estudar estado distribuído, backplane Redis/Azure SignalR e afinidade de sessão antes de habilitar réplicas: https://learn.microsoft.com/en-us/aspnet/core/signalr/scale

## 8. Decisões a validar antes de começar

- [ ] VPS com volume local de mídia na V1; storage S3-compatível apenas na evolução.
- [ ] Solo V1 online, com Adventure e Practice, sem bots ou offline.
- [ ] A mesma identidade de convidado mantém XP e progresso solo/multiplayer.
- [ ] Recompensas para repetição de aventuras são reduzidas e idempotentes.
- [ ] Estado de jogo persistido por fase e snapshot reconstituível após refresh.
- [ ] Uma instância de API e um PostgreSQL, sem Redis/microserviços.

## 9. Próximos documentos técnicos

1. `DATABASE.md`: chaves, restrições, relacionamentos e índices, especialmente sessões, respostas, cenas e mídias.
2. `GAME_RULES.md`: contratos da máquina de estados, pontuação por modo, elegibilidade dos eventos e políticas de retomada.
3. `API.md`: endpoints, comandos SignalR, snapshots, erros, autorização e idempotência.
4. `DEPLOY.md`: compose, volumes, proxy, backups e restauração.

**Regra prática:** iniciar pelo primeiro fluxo completo **Story → Scene → Audio → Question → Answer → Next Scene** no solo. Reusar esse núcleo no multiplayer.
