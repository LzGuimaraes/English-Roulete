# Multiplayer — primeiro incremento de lobby

Abra **http://localhost:5080/multiplayer** ou use o link de convite `/join/{codigo}`. Esta etapa começa RF-001 a RF-004: perfil de convidado, criação/entrada em sala, participantes, prontidão, roleta compartilhada e reconexão do lobby. A partida sincronizada, votação, ranking, XP e revanche ainda não estão implementados; a interface informa esse limite.

## Fluxo disponível

1. Informe apelido de 3 a 20 caracteres e escolha um dos avatares permitidos.
2. Crie uma sala de 2 a 8 vagas ou entre com o código/link recebido.
3. Cada jogador marca seu próprio estado “pronto”. Outros navegadores recebem uma notificação SignalR e buscam o snapshot autorizado.
4. Com pelo menos dois jogadores conectados e todos prontos, o anfitrião inicia a roleta; o servidor sorteia o gênero e a história.
5. Ao sair, a liderança passa ao participante conectado mais antigo; na ausência de conectados, uma vaga ainda dentro da janela de reconexão conserva a sala.

Apelidos precisam ser distintos dentro da sala. Reconectar com o mesmo cookie não ocupa outra vaga. O código tem oito caracteres gerados por criptografia e não substitui a autenticação individual. O convite não contém credenciais.

## Estado, relógio e autorização

Um cookie de convidado protegido pelo ASP.NET Core autentica o jogador, com HttpOnly, SameSite=Strict e Secure fora de Development. Ele é separado do cookie administrativo. O servidor fornece token antiforgery específico da identidade de jogador para mutações de sala. Entrada/perfil têm limite por IP e comandos têm limite por usuário. A API valida Origin nas rotas de sala e no hub quando o navegador envia esse cabeçalho.

`GET /api/rooms/current` funciona como heartbeat de 10 segundos na UI. Presença sem heartbeat por 30 segundos aparece como reconectando; a vaga é preservada até 90 segundos. Salas inativas expiram em 30 minutos. Expiração é aplicada na próxima leitura/comando; não há worker de limpeza neste incremento. Abas em segundo plano podem ter timers suspensos pelo navegador.

O PostgreSQL guarda perfis em `game_guests`, snapshots em `multiplayer_lobbies` e a associação ativa em `lobby_memberships`. Cada convidado ocupa uma sala por vez. Locks transacionais serializam alterações na sala; um lock consultivo por jogador e a chave única de associação impedem criar duas salas simultâneas com a mesma identidade. A revisão de estado rejeita comandos de prontidão/configuração obsoletos.

SignalR envia somente identificador e revisão da sala. Cada leitura REST e ingresso no grupo verifica a participação. Ao reconectar o transporte, o cliente reingressa no grupo e busca o snapshot; não reconstrói o estado com mensagens perdidas. O polling mantém uma alternativa quando o transporte está indisponível. Há uma instância de API, conforme a arquitetura V1.

Novas aventuras solo usam a identidade de convidado quando ela já existe. Sessões solo anteriores conservam seu proprietário e a credencial legada para preservar o progresso. A migração completa de perfis históricos e XP será tratada na progressão.

## Código e validação

| Componente | Responsabilidade |
|---|---|
| `LobbyRoom` | Regras puras de capacidade, participação, prontidão, liderança e expiração |
| `ILobbyStore` | Porta de persistência de perfis e salas |
| `LobbyStore` | PostgreSQL, snapshot e concorrência transacional |
| `RoomEndpoints` | Cookie de jogador, autorização, antiforgery e contratos REST |
| `RoomHub` | Ingresso autorizado nos grupos e sinal de atualização |
| `RoomClient` / `Multiplayer` | Comandos, snapshot, reconexão e UI do lobby |

Execute os testes do README. Os casos incluem lotação, apelido duplicado, repetição de entrada, revisão antiga, acesso de não participante, CSRF, concorrência ao criar salas, transferência e expiração da presença.

Para verificar manualmente, abra dois perfis de navegador (ou janela anônima), crie uma sala com um jogador e entre com o outro. Confira prontidão em ambos, resultado compartilhado da roleta, refresh, transferência ao sair e ausência de acesso administrativo para convidados.

O fluxo de partida agora reutiliza `StoryFlowEngine` e `ChallengeEvaluator` para preloading, áudio simultâneo, perguntas com prazo do servidor, decisão coletiva, chat curto e feedback pós-resposta. O chat fica disponível desde a criação da sala, inclusive para o primeiro participante, e o histórico segue para a partida. Antes do sorteio, o anfitrião pode ajustar tempo por pergunta (10–60 segundos), vagas (2–8, sem retirar participantes presentes), idioma (inglês ou português) e nível editorial (todos ou A1–C1); todos recebem o estado atualizado. Depois do sorteio, essas opções ficam bloqueadas. O solo também oferece o filtro por nível antes do sorteio. O servidor restringe o catálogo ao idioma e nível escolhidos antes de sortear gênero e história; se não houver história elegível, o sorteio não começa. O nível da sala permanece nas partidas seguintes. `StoryDifficulty` valida os níveis, `StoryRoulette` aplica o filtro e `LobbyState` persiste a escolha. DECISION NEEDED: definir se mudar o idioma depois de jogadores marcarem “pronto” deve limpar a prontidão de todos; por ora a prontidão permanece. DECISION NEEDED: definir se os níveis editoriais em português devem receber nomes próprios; por ora usam os mesmos códigos do catálogo, sem alegação de certificação CEFR para português. Veja [LIVE_CHAT_TIMED_QUESTIONS_TRANSLATION.md](increments/LIVE_CHAT_TIMED_QUESTIONS_TRANSLATION.md) para critérios, limites e decisões ainda abertas.

## Atualização: roleta compartilhada

A escolha manual de história foi substituída por `POST /api/rooms/{id}/spin` com `SpinRoomRequest(ExpectedStateVersion)`. O anfitrião inicia após todos os presentes conectados confirmarem prontidão (mínimo de dois). O servidor sorteia gênero/história, persiste resultado e `RouletteStartedAtUtc` no checkpoint e notifica a sala. Não é permitido trocar o resultado com outro giro. A partida simultânea permanece pendente. Veja [GAME_EXPERIENCE.md](GAME_EXPERIENCE.md) para aceite, responsabilidades e validação.
