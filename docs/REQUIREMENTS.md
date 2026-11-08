# English Roulette — Story Battle

> **Especificação de requisitos e arquitetura inicial**  
> **Versão:** 1.3 — proposta para validação (Single Player + Multiplayer)  
> **Data:** 06/11/2026  
> **Status:** RASCUNHO / A VALIDAR  
> **Alvo:** aplicação web com Single Player (1 pessoa) e Multiplayer (2 a 8 jogadores), responsiva e preparada para VPS Linux  
> **Sem IA em tempo de execução.** O desenvolvimento poderá utilizar assistentes de IA como apoio ao aprendizado de C#.

## 1. Visão do produto

O **English Roulette — Story Battle** é um jogo educacional single player e multiplayer de compreensão de inglês. No multiplayer, participantes entram em uma sala; no single player, a aventura começa diretamente. Em ambos, acompanham **histórias em inglês narradas por áudio**, com texto na tela quando o desafio permitir. Em momentos definidos da narrativa, respondem a perguntas sobre o que ouviram e leram — simultaneamente quando há vários jogadores. **Acerto e velocidade** influenciam a pontuação conforme o modo escolhido. Em cenas específicas, cada jogador solo **escolhe o próprio caminho**; no multiplayer, o grupo **vota nos caminhos da história**, podendo alcançar finais distintos. Eventos e minijogos variam o ritmo para evitar partidas maçantes.

### 1.1 Objetivos

- Treinar principalmente **listening** e **reading**, com vocabulário e interpretação como habilidades complementares.
- Criar uma experiência divertida e acessível para **1 jogador (solo) ou 2 a 8 jogadores (multiplayer)**, em navegador de computador, tablet ou celular.
- Permitir a inclusão contínua de histórias, narrações, cenas, imagens, perguntas, desafios e eventos por um **painel administrativo**.
- Manter um jogo plenamente operacional **sem serviços de IA** e sem geração dinâmica de narrativas/perguntas durante as partidas.
- Servir de projeto prático para aprender **C#, ASP.NET Core, Blazor, EF Core, PostgreSQL, testes, SignalR e implantação em VPS**.

### 1.2 Princípios de produto

1. **Todos participam:** no multiplayer ninguém fica aguardando um leitor; no solo a pessoa controla o ritmo da aventura.
2. **Narrativa primeiro:** perguntas fazem sentido dentro da cena; minijogos não devem quebrar a história o tempo todo.
3. **Competição justa:** pontuação autoritativa no servidor; escolhas narrativas não devem punir a minoria.
4. **Conteúdo expansível:** novos materiais são publicados pelo painel sem alteração de código.
5. **Variedade sem aleatoriedade caótica:** evitar repetição e escolher apenas desafios válidos para a cena, nível e modo.
6. **Uma VPS primeiro:** arquitetura simples, modular e de baixo custo, preparada para crescer.

---

## 2. Escopo e decisões estabelecidas

| Assunto | Decisão da V1 |
|---|---|
| Plataforma | Web responsiva; PWA instalável quando houver suporte do navegador |
| Participantes | **Single Player: 1**; **Multiplayer: 2 a 8** por sala |
| Acesso ao jogo | Convidados com apelido e avatar; não exigir conta para iniciar |
| Administração | Login protegido para editar e publicar conteúdo |
| Experiência central | História narrada, cenas e perguntas; **solo com decisões próprias e recordes**, multiplayer com ranking e decisões coletivas |
| Língua do conteúdo | Áudio, texto e desafios em inglês; interface pode ter tradução PT-BR |
| Níveis iniciais | A1, A2 e B1; estrutura preparada para B2/C1 |
| Conteúdo | Produzido/importado e revisado antes da publicação; sem IA integrada ao jogo |
| Banco de dados | **PostgreSQL** exclusivamente |
| Narração | Arquivos de áudio vinculados a cenas; metadados no PostgreSQL |
| Hospedagem | VPS Linux, Docker Compose e HTTPS |
| Comunicação | **REST** e estado persistido para solo; **REST + SignalR** para sincronização multiplayer |

**Fora do escopo da V1:** bots adversários, campanha offline, leitura em voz alta por jogadores como mecânica principal, reconhecimento/avaliação de pronúncia, chat de voz nativo, partidas públicas aleatórias, torneios globais, geração por IA ao vivo, apps nativos Android/iOS, loja virtual e microserviços.

---

## 3. Stack tecnológica

| Camada | Tecnologia proposta | Uso |
|---|---|---|
| Linguagem | **C#** | Backend, domínio, contratos e frontend |
| Runtime | **.NET 10 LTS** | Base do projeto |
| Backend | **ASP.NET Core Web API** | REST, autenticação administrativa, regras e orquestração |
| Frontend | **Blazor WebAssembly** | Interface interativa executada no navegador |
| Tempo real | **ASP.NET Core SignalR** | Grupos por sala, sincronização e placares |
| Persistência | **PostgreSQL** | Dados relacionais, partidas, conteúdo e histórico |
| ORM | **Entity Framework Core + Npgsql** | Mapeamentos, LINQ e migrations |
| Arquivos | **Diretório/volume persistente na VPS** | Áudios, imagens e outros assets publicados |
| Entrega de mídias | **Arquivos estáticos via servidor web/proxy** | Download e cache de arquivos de áudio/imagem |
| Proxy e TLS | **Traefik** | HTTPS, domínios e roteamento |
| Infra | **Docker + Docker Compose** | Execução local e VPS |
| Testes | **xUnit + testes de integração** | Regras, persistência e API |
| Contrato API | **OpenAPI** | Documentação dos endpoints |
| CI/CD | **GitHub Actions** | Build, testes e deploy automatizado (etapa posterior) |
| Versionamento | **Git / GitHub** | Código e histórico de alterações |

**Compatibilidade:** escolher versões de EF Core, Npgsql e demais dependências compatíveis entre si e com o target `net10.0`. Não fixar uma versão de pacote sem verificar sua compatibilidade na implementação.

**Escolha de implantação:** frontend Blazor WebAssembly publicado como arquivos estáticos, backend ASP.NET Core em container e PostgreSQL em container separado ou serviço externo compatível. Para uma VPS pequena, medir consumo antes de optar por banco local. **Não usar SQL Server.**

### 3.1 Estrutura sugerida da solução

```text
EnglishRoulette/
├── src/
│   ├── EnglishRoulette.Api/             # Endpoints HTTP, autorização e hubs
│   ├── EnglishRoulette.Application/     # Casos de uso, serviços e orquestração
│   ├── EnglishRoulette.Domain/          # Entidades, enums, regras e estados
│   ├── EnglishRoulette.Infrastructure/  # EF Core, PostgreSQL, repos, arquivos
│   ├── EnglishRoulette.Contracts/       # DTOs públicos compartilhados
│   └── EnglishRoulette.Web/             # Blazor WebAssembly
├── tests/
│   ├── EnglishRoulette.UnitTests/
│   └── EnglishRoulette.IntegrationTests/
├── infra/
│   ├── docker/
│   ├── compose.yaml
│   └── .env.example
├── docs/
│   ├── REQUIREMENTS.md
│   ├── DATABASE.md
│   ├── GAME_RULES.md
│   ├── CODING_STANDARDS.md
│   ├── ARCHITECTURE.md
│   ├── API.md
│   └── DEPLOY.md
├── AGENTS.md
└── EnglishRoulette.sln
```

**Regra arquitetural:** evitar camadas, interfaces ou padrões criados apenas por formalidade. Manter lógica do jogo independente de controllers, hubs e componentes Blazor. SignalR **notifica** as transições; o backend **decide** as transições.

---

## 4. Regras gerais de identidade, sala e partida (multiplayer)

### RF-001 — Identificação de convidado

- Jogador informa nickname (3–20 caracteres) e escolhe avatar de catálogo permitido.
- Sistema emite um identificador de convidado e **credencial opaca de sessão**; a credencial deve ficar protegida e não ser divulgada em links de sala.
- Nickname não identifica de maneira segura o jogador e pode se repetir entre salas; exigir apelidos distintos apenas **dentro da mesma sala**, para reduzir confusão.
- Dados e XP associados à identidade do convidado permanecem recuperáveis enquanto a credencial local for preservada.
- Troca de navegador/dispositivo sem credencial equivale a novo convidado; contas permanentes não fazem parte da V1.

### RF-002 — Criar e entrar em sala

- Permitir criar sala privada com **código aleatório curto não previsível** e link de convite.
- Definir lotação **entre 2 e 8**; por padrão, 8 vagas.
- Entrar por código/link com validação de sala ativa e capacidade.
- Mostrar avatares, nicknames, anfitrião, conexão e estado `READY` de todos.
- Impedir jogadores adicionais após lotação; reconexão não ocupa uma vaga nova.
- O código identifica a sala, mas **não substitui autorização individual**.
- Salas não iniciadas expiram após inatividade configurável; salas encerradas não aceitam novas respostas.

### RF-003 — Lobby e configurações

- Apenas o anfitrião pode escolher modo (Story Battle na V1), quantidade de histórias (1, 2 ou 3), nível A1/A2/B1, gêneros elegíveis e eventos surpresa ativados/desativados.
- Anfitrião inicia quando **pelo menos 2** jogadores estiverem conectados e todos estiverem prontos.
- Durante a partida, a configuração fica congelada; nova configuração apenas na revanche.
- Se o anfitrião sair, transferir liderança determinística ao próximo jogador conectado (mais antigo na sala).

### RF-004 — Reconexão e abandono

- Ao perder conexão, sinalizar estado `DISCONNECTED`, preservando vaga por **90 segundos** (configurável).
- Na reconexão autenticada, reingressar no grupo SignalR e **receber um snapshot do estado atual**; nunca confiar no histórico da conexão.
- Respostas não enviadas dentro do prazo **não são reabertas** após reconexão.
- Com 2 ou mais conectados, a partida pode continuar; com menos de 2, suspender por até 90 segundos e encerrar por abandono se não houver reconexão.
- Jogadores que abandonarem antes do fim não recebem bônus de conclusão; registrar abandono nas estatísticas.

### RF-005 — Revanche e fim

- Ao concluir, mostrar placar, desempenho, história/final alcançado, XP e conquistas.
- Oferecer revanche na mesma sala, exigindo confirmação dos participantes presentes.
- Nova partida reinicia **pontos de partida**, mas preserva XP e histórico do perfil.
- Um jogador ausente na revanche não bloqueia os demais, desde que restem dois.

---

## 5. Fluxo principal: Story Battle (multiplayer e solo)

### 5.1 Fases da partida multiplayer

```text
LOBBY → READY_CHECK → STORY_SELECTION → PRELOADING
→ NARRATING_SCENE → QUESTION_OPEN → QUESTION_RESULT
→ [SURPRISE_EVENT?] → [COLLECTIVE_DECISION?]
→ NEXT_SCENE ────────┘
→ STORY_RESULT → [NEXT_STORY?] → MATCH_RESULT → REMATCH/LOBBY
```

Cada fase é definida pelo servidor com identificador e número de versão do estado. Um cliente atrasado deve reconstruir a tela pelo snapshot, sem reaplicar eventos antigos.

### RF-006 — Escolha de história e roleta

- A roleta inicial sorteia **gênero/tema** com conteúdo válido para nível e duração selecionados; em seguida, seleciona uma história elegível.
- Exibir animação de giro/desaceleração sincronizada; o **resultado é sorteado no servidor**, nunca no CSS/JavaScript.
- Se houver escolha de gênero restrita no lobby, sortear apenas dentro do conjunto habilitado.
- Informar quando o catálogo disponível for pequeno e aplicar política explícita de repetição (seção 11), sem seleção infinita.

### RF-007 — Preparação da narração

- Cada cena publicada possui áudio em inglês associado, texto correspondente, duração e opção de imagem/ilustração.
- O cliente pré-carrega o áudio da cena **antes** do início; sinaliza `AUDIO_READY` ao servidor.
- Antes da primeira cena, mostrar botão **“Enable audio / Ready to listen”** que exige interação real do usuário, pois navegadores podem bloquear reprodução automática.
- Após carregamento dos participantes (ou tempo limite), o servidor envia instante de início e contagem regressiva para todos.
- Cliente reproduz localmente o mesmo arquivo a partir da marcação de início; **não enviar stream de áudio pelo SignalR**.
- Se mídia falhar, mostrar erro, permitir tentar novamente e definir política de continuação/abandono sem deixar a sala travada.

### RF-008 — Cena narrada e texto

- Exibir título/cena, ilustração, progressão da história e texto em inglês alinhado à narração.
- Controles locais: volume, mute, preferência de fonte e legendas conforme a regra da cena.
- **Não permitir avançar ou pausar apenas para um jogador** em partida competitiva; controles individuais não alteram a linha do tempo global.
- Nas cenas normais, mostrar **texto + áudio**; em eventos `Blind Listening`, ocultar texto durante a narração e liberar após o desafio.
- Ao término, esconder o texto quando a pergunta exigir memória/compreensão, e liberar o enunciado para todos.
- Evitar efeitos visuais que encubram legenda ou alternativas.

### RF-009 — Perguntas simultâneas

- Ao final de cena elegível, servidor abre uma pergunta para todos, com `questionId`, `opensAt`, `closesAt` e alternativas ordenadas de maneira definida.
- Todos recebem a mesma pergunta, salvo eventos explicitamente diferenciados e balanceados.
- Cada pergunta tem 10–20 segundos por padrão, ajustável por tipo e dificuldade.
- Cada jogador responde **uma única vez** por pergunta; confirmação é definitiva.
- O servidor valida vínculo à sala, fase vigente, alternativa, prazo e duplicidade.
- Não enviar resposta correta aos clientes enquanto o desafio estiver aberto.
- Revelar resposta, explicação breve, pontos individuais e classificação **após o encerramento**.

### RF-010 — Decisões coletivas e caminhos

- Em pontos narrativos pré-definidos, o grupo escolhe entre **2 a 4 alternativas** que conectam cenas diferentes.
- Cada jogador tem um voto; resultado por **maior número de votos** (pluralidade).
- Em empate, realizar desempate aleatório **no servidor** entre alternativas empatadas, com resultado indicado na interface.
- As decisões **não rendem pontos por escolher a opção vencedora**; servem para alterar a narrativa.
- A escolha vencedora indica a próxima cena no grafo; o grupo segue sempre pelo **mesmo caminho**.
- Uma história pode ter múltiplos finais, identificados e registrados.
- Validar o grafo de publicação: cena inicial, caminhos existentes, finais alcançáveis, ausência de ciclos não intencionais e limite de cenas percorríveis.

### RF-011 — Encerramento

- Concluir história ao chegar a cena final válida.
- Mostrar resumo do percurso, escolhas coletivas, cenas vistas, final alcançado e desempenho na compreensão.
- Ao terminar todas as histórias previstas, calcular vencedor e desempate, persistir resultado e liberar revanche.


### 5.2 Modo Single Player — requisitos específicos

**Conceito:** campanha narrativa individual usando o **mesmo catálogo de histórias, versões, cenas, áudio, perguntas, eventos e motor de transições** do multiplayer. Não criar uma duplicata de entidades ou regras para solo. Nenhuma sala é necessária.

#### RF-SOLO-001 — Acesso e seleção

- Na Home, exibir **Play Solo** e **Multiplayer** como opções equivalentes.
- Identificar o jogador pela mesma sessão de convidado utilizada no multiplayer, preservando XP e histórico; permitir trocar nickname e avatar.
- Escolher gênero, história (quando elegível) e dificuldade A1/A2/B1. A opção **Surprise Me** usa a roleta entre histórias publicadas e elegíveis.
- Selecionar uma das experiências: **Adventure** (pontuação/tempo) ou **Practice** (estudo sem pressão).
- Não exigir lobby, código de sala, SignalR, adversário, voto coletivo nem áudio de microfone no Single Player.

#### RF-SOLO-002 — Aventura individual (Adventure)

- Narrar a cena pré-gravada e apresentar texto segundo as regras da história e do evento.
- Exibir uma questão por vez; pontuar **por acerto + faixa de tempo da resposta**, nunca pela posição entre jogadores.
- Definir faixas de velocidade no servidor e registrá-las por questão; o limite deve ser adequado a A1/A2/B1 e ao tipo de minijogo.
- Mostrar feedback e explicação após cada questão; a narrativa prossegue após a confirmação/encerramento.
- Apurar pontos totais, taxa de acerto, tempo por questão, percurso, final encontrado e recorde pessoal ao concluir.
- O resultado não disputa classificação competitiva multiplayer; leaderboard solo global fica fora do escopo inicial.

#### RF-SOLO-003 — Prática individual (Practice)

- O jogador pode **pausar, repetir e avançar a narração** de forma controlada e ampliar o texto/legendas.
- Permitir repetir a questão como nova **tentativa de estudo**, visualizar explicação e consultar palavras marcadas para revisão. Tentativas adicionais não substituem a primeira resposta da sessão nem geram pontos/XP.
- **Não aplicar ranking de velocidade** nem premiar repetição ilimitada com XP. Exibir progresso e acertos como métricas de estudo.
- O jogador escolhe o caminho da história sozinho; a escolha determina o destino sem votação.
- Salvar progresso de estudos e últimas cenas visitadas para retomada.

#### RF-SOLO-004 — Decisões e ramificações

- Em cada `DecisionInstance`, exibir opções válidas da cena, respeitando a versão publicada.
- Em solo, registrar a **escolha individual**; aplicar diretamente a transição correspondente no grafo.
- Não criar `PlayerVote` coletivo com desempate aleatório para o solo; persistir decisão/percurso equivalente, com `DecisionChoice` por sessão.
- Histórias com caminhos diferentes devem permitir rejogar para encontrar finais alternativos.

#### RF-SOLO-005 — Eventos surpresa adaptativos

- Usar o mesmo catálogo de eventos, com campo de **modos elegíveis** (`SOLO_ADVENTURE`, `SOLO_PRACTICE`, `MULTIPLAYER`).
- `BLIND_LISTENING`, `MEMORY_RUSH`, `MISSING_WORD`, `FIND_THE_LIE`, `SCRAMBLED_SENTENCE` e `FINAL_BOSS` são elegíveis quando houver conteúdo específico.
- `DOUBLE_POINTS` e `SPEED_CHALLENGE` podem aparecer em Adventure, não no Practice sem tempo.
- Eventos que dependam de comparação/votação entre jogadores ficam inelegíveis no solo.
- Evitar repetir um evento consecutivamente e selecionar somente questões já publicadas e compatíveis.

#### RF-SOLO-006 — Sair da aventura e começar do zero

Decisão expressa do responsável em 08/11/2026: a aventura solo não deve ser retomada ao retornar ao site. Substitui a proposta anterior de salvar e continuar Adventure. Practice continua uma proposta futura.

- Estado, respostas, dicas, pontos e prazos permanecem autoritativos no servidor enquanto a página está aberta, para consistência e retries.
- Sair pelo menu, pelo logotipo ou para outra página encerra a aventura. Recarregar, fechar/reabrir a aba ou voltar a uma página solo restaurada pelo navegador mostra o menu inicial.
- Uma nova aventura começa na primeira cena, com pontos e dicas zerados. Não apresentar `Continue Adventure` nem restaurar uma aventura usando cookies ou armazenamento do navegador.
- A credencial da aventura existe somente na memória da página, sem cookie, localStorage, sessionStorage ou URL. Abas diferentes não compartilham essa credencial.
- A reconexão durante a mesma página pode recuperar o snapshot vigente, sem reiniciar prazos ou duplicar pontos. Isso não habilita retomada após sair.
- Multiplayer mantém identidade, reconexão e sala existentes. A V1 solo requer conexão à internet; PWA não significa histórias offline.

#### RF-SOLO-007 — Progressão e antirrepetição

- Manter XP do perfil compartilhado entre solo e multiplayer, mas **pontuações de cada sessão separadas**.
- XP solo do modo Adventure só deve ser creditado após conclusão válida; repetições da mesma história podem ter recompensa reduzida configurável. Practice não concede XP por respostas repetidas.
- Registrar histórias, cenas, finais, tipos de desafio e questões recentemente vistos pelo jogador.
- Priorizar conteúdo ainda não visto, outros caminhos e questões alternativas; oferecer replay explícito quando o catálogo for pequeno.
- Histórico e conquistas devem identificar origem `SOLO` ou `MULTIPLAYER` para comparação correta.

#### RF-SOLO-008 — Contratos e consistência

- `GameSession.Mode` distingue `SOLO_ADVENTURE`, `SOLO_PRACTICE` e `MULTIPLAYER`; `RoomId` será **nulo apenas no solo**.
- `SessionParticipant` deve ter **exatamente um jogador para solo**, e de dois a oito para multiplayer no momento do início; validar no domínio/application.
- Serviços compartilhados administram seleção de cena, grafo narrativo, desafios, avaliações e eventos; políticas de tempo, pontos e escolha variam pelo modo.
- Solo usa endpoints REST idempotentes; multiplayer usa comandos autorizados e notificações SignalR. Nenhum cliente decide gabarito, tempo válido ou pontos.

### 5.3 Fluxo individual

```text
HOME → MODE_SELECTION (ADVENTURE | PRACTICE) → STORY_SELECTION
→ PRELOADING → NARRATING_SCENE → QUESTION → FEEDBACK
→ [SOLO_EVENT?] → [PLAYER_CHOICE?] → NEXT_SCENE
→ STORY_FINISHED → SUMMARY → REPLAY / NEW STORY
                  ↘ EXIT/RELOAD → HOME → NEW_ADVENTURE_FROM_ZERO
```

Em Adventure, o servidor administra prazos e pontuação; em Practice, o usuário controla pausas e revisões. Ambos usam a mesma versão publicada da história.

---

## 6. Catálogo de conteúdo: histórias, áudio e versões

### RF-012 — Cadastro de história

Campos mínimos:

- `Id`, `Title`, `Slug`, `Synopsis`, `GenreId`, `Difficulty`, `EstimatedMinutes`.
- `Language = en`, `Status`, `CoverAssetId`, tags, classificação indicativa e autoria/créditos.
- Dados da fonte: autor original, título da obra, URL de origem (quando houver), categoria de licença, evidência/autorização, restrições de uso e data de verificação.
- `CreatedAt`, `UpdatedAt`, `PublishedAt`, responsável pela revisão.
- Uma história pode possuir **versões publicadas** e um rascunho editável.

**Estados:** `DRAFT → IN_REVIEW → PUBLISHED → ARCHIVED`. Apenas `PUBLISHED` e juridicamente apta aparece na roleta.

### RF-013 — Cenas e ramificações

Cada versão de história contém:

- Uma `StoryScene` inicial e pelo menos uma final.
- `SceneKey`, ordem lógica/caminho, título, texto em inglês, duração estimada, áudio principal, imagem opcional.
- `SceneTransition`: texto da escolha, ordem de exibição e `NextSceneId`.
- Perguntas vinculadas à cena; eventos permitidos; condições de exibição quando pertinentes.

**Versionamento:** alterar texto, áudio, alternativas ou caminhos após publicação deve criar **nova versão**. Partidas existentes conservam referência à versão usada, impedindo que o histórico aponte para conteúdo alterado.

### RF-014 — Narração persistida

- Fazer upload de **MP3** (padrão inicial) e opcionalmente Opus/AAC se houver compatibilidade comprovada.
- Guardar **arquivo fora do PostgreSQL** em armazenamento persistente; banco guarda `AssetId`, caminho/chave, MIME, tamanho, duração, checksum, autoria, licença e data de upload.
- Oferecer upload por cena no painel administrativo, associação a versão de história e player de pré-escuta.
- Validar formato real, tamanho máximo, extensão permitida e acesso do administrador; nomes físicos gerados pelo servidor.
- Garantir que todo arquivo de áudio referenciado em uma versão publicada exista e possa ser servido com HTTPS.
- Preferir arquivos curtos **por cena** (20–45 segundos por padrão) para facilitar ramificações, pré-carregamento e substituições.
- Manter volume persistente entre deploys e backup separado dos arquivos; **não colocar áudio dentro da imagem Docker nem versionar arquivos grandes no Git**.
- Cada mídia usada em partida publicada deve permanecer disponível enquanto essa versão puder ser consultada; exclusão somente quando não houver referência, segundo política de retenção.
- Permitir trocar narração por nova versão, sem sobrescrever silenciosamente áudio usado em partidas concluídas.

**Produção:** narrações podem ser gravadas manualmente ou produzidas **fora da aplicação** com ferramenta de síntese de fala autorizada. Nenhuma API de texto-para-fala será necessária em tempo de jogo. Registrar permissões comerciais/de distribuição do áudio gerado quando aplicável.

### RF-015 — Upload de ilustrações e trilhas

- Vincular capa à história e imagem opcional a cada cena.
- Sons de interface e ambiente opcionais, com volume/mute independentes da narração.
- Usar ativos originais, licenciados ou autorizados; registrar autoria/licença.
- Evitar trilhas sonoras que prejudiquem a compreensão; o áudio narrado tem prioridade.

### RF-016 — Política de aquisição e direitos de histórias

- O administrador pode **pesquisar referências e histórias publicadas na internet**, mas a obra não entra automaticamente no jogo.
- **Disponibilidade no Google não implica permissão de uso.** A publicação de texto, tradução, adaptação, recitação ou gravação de obras protegidas pode exigir autorização.
- Aceitar apenas: **histórias originais**, obras comprovadamente em **domínio público** considerando país/edição/tradução, ou obras com **licença/autorização que permita adaptação, gravação e distribuição** na plataforma.
- Registrar atribuição e condições de licença; guardar documento/link de evidência.
- Usar texto adaptado para aprendizagem quando os direitos permitirem; registrar relação com a obra original.
- Excluir de publicação material com licença desconhecida, autorização insuficiente ou uso incompatível.
- Verificar também a situação autoral de **traduções modernas, narrações de terceiros, imagens e músicas**, mesmo quando a história original está em domínio público.

### RF-017 — Expansão contínua do catálogo

- O administrador poderá publicar novas histórias, revisões, novos finais e pacotes de perguntas **sem deploy**.
- Validar dependências antes de publicar: cenas alcançáveis, áudios carregados, perguntas corretas, alternativas únicas, nível, transições, créditos/licenças.
- Permitir `DUPLICATE AS DRAFT` para criar nova versão/variante pedagógica sem afetar partidas anteriores.
- Mostrar no painel: histórias publicadas, em revisão, mais jogadas, taxa de acerto, mais repetidas e conteúdo com poucos desafios.

---

## 7. Perguntas, dificuldades e variedade

### RF-018 — Modelo de desafio

Todo desafio deve possuir:

- `ChallengeId`, `SceneId` (ou referência à história quando for retrospectivo), `Difficulty`, `Type`, `Prompt`, `TimeLimitSeconds`, `BasePoints`, `Status`.
- Conjunto de alternativas/itens, gabarito, explicação, tags de habilidade (`listening`, `reading`, `vocabulary`, `memory`, `inference`), peso e política de uso.
- Pré-requisitos de contexto: cena atual, cenas já vistas, tipo de texto exibido, evento associado.
- Identificador de versão para não invalidar respostas antigas.

### RF-019 — Tipos de desafio da V1

| Tipo | Interação | Aplicação |
|---|---|---|
| `MULTIPLE_CHOICE` | Selecionar 1 de 4 alternativas | Fatos, interpretação e ideia principal |
| `TRUE_FALSE` | Verdadeiro/Falso | Compreensão rápida |
| `MISSING_WORD` | Escolher palavra/expressão ausente | Vocabulário e contexto |
| `SEQUENCE_ORDER` | Ordenar 3–5 acontecimentos | Memória e sequência narrativa |
| `SCRAMBLED_SENTENCE` | Reorganizar palavras da frase | Gramática e reconstrução de fala |
| `WHO_SAID_IT` | Identificar personagem ou origem de frase | Atenção a detalhes |
| `MEMORY_RECALL` | Pergunta sobre cena anterior | Retenção de informação |
| `FIND_THE_LIE` | Selecionar afirmação inconsistente | Interpretação e atenção |
| `SPEED_ROUND` | 3 perguntas curtas cronometradas | Evento especial |

Em desafios com ordenação, utilizar controles acessíveis também por toque/teclado, sem depender exclusivamente de drag-and-drop.

### RF-020 — Progressão por nível

| Nível | Texto/áudio | Perguntas |
|---|---|---|
| **A1** | Frases curtas, vocabulário frequente, narração mais clara/lenta | Informação explícita e palavras simples |
| **A2** | Narrativas curtas no presente/passado, maior variedade lexical | Detalhes e sequência de fatos |
| **B1** | Histórias mais desenvolvidas, conectores, inferência moderada | Motivações, intenção e contexto |
| **B2** *(futuro)* | Vocabulário e estruturas mais complexas | Inferência, nuances e interpretação crítica |

- Dificuldade da **história e das perguntas** deve ser coerente; não usar pergunta B1 em partida A1 sem configuração explícita.
- Não definir dificuldade somente pela contagem de palavras; considerar gramática, velocidade da narração, inferência e complexidade lexical.
- Cada texto e pergunta deve passar por **revisão humana** antes de publicação.

### RF-021 — Banco de variações

- Permitir **múltiplas perguntas para a mesma cena e habilidade**, para que uma revisita não reproduza sempre a mesma sequência.
- Permitir variantes diferentes da **mesma história** em níveis distintos, com texto/áudio e perguntas próprios.
- O mecanismo selecionará perguntas compatíveis com o caminho realmente percorrido, nível e janela atual.
- Evitar perguntas que revelariam informação de cenas ainda não vistas ou de caminhos não escolhidos.
- Permitir prévia de todas as variantes no painel e simulação de percurso antes da publicação.

---

## 8. Eventos surpresa e minijogos

### RF-022 — Catálogo de eventos

| Evento | Regra | Elegibilidade |
|---|---|---|
| `BLIND_LISTENING` | Cena apenas com áudio; texto oculto até o fim | Cena com narração e perguntas adequadas |
| `DOUBLE_POINTS` | Próxima questão vale dobro para todos | Questão comum ainda não iniciada |
| `MEMORY_RUSH` | Pergunta sobre cena já ouvida | Exige cena anterior válida |
| `FIND_THE_LIE` | Identificar afirmação falsa sobre o ocorrido | Exige alternativas revisadas |
| `SPEED_CHALLENGE` | Três questões curtas em sequência | Exige banco compatível |
| `MISSING_WORD` | Completar expressão ou frase | Exige conteúdo lexical preparado |
| `SCRAMBLED_SENTENCE` | Ordenar palavras de uma frase | Exige frase e gabarito preparados |
| `MYSTERY_BOX` | Revela desafio bônus ou dica definida no servidor | Recompensa balanceada |
| `FINAL_BOSS` | Desafio final de compreensão acumulada | Após a última cena |

### RF-023 — Regras de sorteio de eventos

- Eventos são selecionados **no servidor** entre opções elegíveis para o contexto atual **e o modo de jogo**.
- Usar janela configurável de aparição (proposta: **25% a 40% das transições entre cenas**, com no máximo **1 evento por cena**).
- Nunca interromper narração ou resposta em andamento.
- Não repetir o mesmo evento em duas aparições consecutivas na sala.
- Recompensas devem ser equivalentes para todos os jogadores; nenhuma caixa fornece pontuação gratuita totalmente aleatória para um único participante.
- Antes de iniciar, explicar em uma frase a regra e o tempo do evento.
- Persistir ocorrência de evento e resultado para auditoria.
- Se não existir conteúdo elegível, **não ativar** o evento, sem travar a partida.

---

## 9. Pontuação e desempate

**Escopo:** a classificação por ordem de chegada abaixo vale apenas no **multiplayer**. Em `SOLO_ADVENTURE`, aplicar faixas de tempo e acerto (configuradas por nível/pergunta) sem posição competitiva; em `SOLO_PRACTICE`, métricas de estudo sem pontos por velocidade. A função de avaliação do gabarito é compartilhada.

### RF-024 — Regras de acerto e velocidade

- **Somente respostas corretas pontuam**; erros/timeout dão 0 (sem pontos negativos na V1).
- Ordenar respostas corretas por **horário de recebimento validado no servidor**.
- Pontos sugeridos para questão comum (posição **entre os acertos**):

| Ordem de acerto | Pontos |
|---:|---:|
| 1ª | 200 |
| 2ª | 180 |
| 3ª | 160 |
| 4ª | 140 |
| 5ª | 120 |
| 6ª | 100 |
| 7ª | 80 |
| 8ª | 60 |

- Se apenas 3 jogadores acertarem, recebem 200/180/160, mesmo que outros cinco tenham errado.
- Para respostas recebidas em intervalo muito próximo (**janela de empate sugerida: 300 ms**), atribuir a mesma faixa de pontos. Esse mecanismo reduz, mas **não elimina**, diferenças de rede.
- Armazenar no banco `ReceivedAtUtc`, `WasCorrect`, `AwardedPoints`, `QuestionInstanceId` e posição/faixa atribuída.
- Tempo, pontos e gabarito nunca são aceitos diretamente do cliente como fonte de verdade.
- Questões com limite expirado não entram na classificação; fechamento usa relógio do servidor.
- Regras de bônus (ex.: `DOUBLE_POINTS`) são aplicadas **igualmente a todos** e registradas por ocorrência.

**Nota de justiça:** uma disputa por milissegundos favorece conexões melhores. Para uma futura versão competitiva/rankeada, avaliar **faixas de tempo maiores, calibração e métricas de latência**, em vez de prometer igualdade absoluta no modo por ordem de chegada.

### RF-025 — XP e conquistas

- **Pontos de partida** determinam ranking da sessão; **XP** é progressão persistente separada.
- Conceder XP por participação concluída, acertos e desafios, com limites para desencorajar farming.
- Conquistas iniciais: `FIRST_VICTORY`, `PERFECT_SCENE`, `SPEED_MASTER`, `BOOKWORM`, `VOCABULARY_MASTER`, `COMEBACK`, `EXPLORER`.
- Registrar histórico por jogador, história e nível; mostrar taxa de acerto e gêneros explorados.

### RF-026 — Empates

- Ao final de história/partida, ordenar por pontos.
- Em igualdade no primeiro lugar, aplicar 1 pergunta de desempate aos empatados; no máximo 3 tentativas adicionais.
- Persistindo empate, declarar **vitória compartilhada** em vez de inventar vencedor pelo ping da conexão.

---

## 10. Multiplayer: comunicação, estado e segurança

### RF-027 — Comunicação em tempo real (somente multiplayer)

- Agrupar conexões SignalR por sala (`room:{roomId}`), após verificar a autorização da sessão de jogador.
- Eventos exemplificativos: `PlayerJoined`, `PlayerReadyChanged`, `StorySelected`, `AudioPreloadRequested`, `SceneStarted`, `QuestionOpened`, `AnswerAccepted`, `QuestionClosed`, `ScoresUpdated`, `VotingOpened`, `PathSelected`, `EventStarted`, `GameFinished`, `PlayerDisconnected`.
- Enviar **DTOs públicos**, não entidades EF Core nem dados com gabaritos ocultos.
- O mesmo estado público deve ser recuperável via endpoint de snapshot após reconexão.

### RF-028 — Máquina de estados

- Implementar fluxo por transições explícitas e regras de autorização: `LOBBY`, `READY`, `SELECTING_STORY`, `PRELOADING`, `NARRATING`, `ANSWERING`, `REVEALING`, `VOTING`, `EVENT`, `FINISHED`, `CANCELLED`.
- Cada sala tem uma transição vigente, versão incremental e deadlines do servidor em UTC.
- Impedir avanços concorrentes/duplicados; alterações de estado e pontuações críticas devem ser **idempotentes**, com controle de concorrência e transações quando necessário.
- Persistir estado suficiente no PostgreSQL para **restaurar com segurança** após reinício do backend; se não for possível concluir uma partida interrompida, finalizá-la explicitamente como `CANCELLED` e nunca inventar vencedor.

### RF-029 — Regras de sincronização da narração

- O **servidor define** o início de cena e as datas-limite, mas a mídia toca localmente em cada navegador.
- Exigir `READY` para áudio antes da primeira cena; a cada cena, pré-carregar o arquivo para reduzir buffering.
- Se um cliente não conseguir carregar áudio, oferecer nova tentativa e política de timeout; registrar falha.
- Abrir perguntas somente após janela planejada da cena e margem de segurança configurável; **não confiar em evento `ended` isolado de um cliente**.
- Mostrar indicador de sincronização e conectividade; áudio entre dispositivos pode ter pequenas diferenças, mesmo com countdown.

### RF-030 — Integridade e proteção

- Rate limit para entrada em sala, votos, respostas, uploads e login administrativo.
- Autenticar identidade de convidado por credencial opaca; quando frontend e API estiverem na mesma origem, preferir cookie `HttpOnly`, `Secure` e `SameSite` apropriado. Proteger requisições mutáveis contra CSRF quando a autenticação usar cookie e validar participação a cada ação.
- Limitar payloads SignalR, tamanho de uploads e tipos MIME.
- Evitar XSS na exibição de textos do catálogo; sanitizar conteúdo editorial permitido.
- Não expor segredos, connection strings, credenciais administrativas ou URLs privadas de armazenamento no frontend.
- Arquivos privados do painel administrativo não devem ser públicos antes da publicação.
- Usar logs sem gravar credenciais ou dados pessoais desnecessários.

---

## 11. Estratégia contra repetição e tédio

### RF-031 — Seleção de histórias

Ordem de preferência no sorteio:

1. Conteúdo publicado, licenciado, completo e compatível com nível/configurações.
2. Excluir histórias já jogadas **na partida atual**.
3. Priorizar histórias não jogadas recentemente pelos participantes da sala (quando há histórico confiável).
4. Variar gêneros e finais, quando existirem alternativas.
5. Aplicar pesos configuráveis para valorizar histórias novas e pouco jogadas.
6. Se o catálogo não oferecer inéditas, reabrir a elegibilidade e **sinalizar que pode haver repetição**, em vez de falhar.

### RF-032 — Seleção de desafios

- Não repetir o mesmo `ChallengeId` dentro da partida.
- Selecionar questões entre as **elegíveis para a cena e o percurso efetivo**.
- Alternar tipos: compreensão literal, inferência, sequência, vocabulário e memória.
- Variar ordem visual das alternativas sem alterar o gabarito.
- Evitar repetição recente para jogadores reincidentes quando houver variantes suficientes.
- Separar perguntas obrigatórias de questões-surpresa opcionais; se não houver substituta válida, usar questão obrigatória em vez de bloqueio.

### RF-033 — Duração e ritmo

- Histórias divididas em **5 a 8 cenas**, com narração de **20–45 segundos por cena** como ponto de partida.
- Meta de partida com **1 história: aproximadamente 8–12 minutos**, variando conforme perguntas, votação e eventos.
- Evitar mais de dois blocos de perguntas repetitivos sem alteração de dinâmica.
- Tela de ranking curta após questões, com animações puláveis e controle para reduzir movimento.
- Mostrar progresso de cena/história e não surpreender o jogador com uma partida de duração desconhecida.

**Métrica para revisão editorial:** registrar taxa de abandono por história/cena, tempo de resposta, proporção de acertos e frequência de repetição. Não fazer ajuste automático de conteúdo na V1.

---

## 12. Administração e publicação

### RF-034 — Painel admin protegido

Funcionalidades:

1. Login/logout e controle de acesso de `ADMIN` (papel `EDITOR` opcional futuramente).
2. CRUD de gêneros, tags e níveis suportados.
3. CRUD e versionamento de histórias, cenas, caminhos/finais.
4. Upload e prévia de áudio de cada cena; upload de capa e imagem de cena.
5. CRUD de perguntas, alternativas, gabaritos e explicações.
6. Configuração de eventos: elegibilidade, chance, regras, ativação.
7. **Prévia jogável** de uma história antes de publicar, navegando pelos caminhos.
8. **Validação automática** antes de publicar, com lista de pendências.
9. Publicar/despublicar/arquivar sem remover histórico.
10. Visualizar uso de histórias, taxa de acerto, erros e sessões concluídas.

### RF-035 — Checklist obrigatório de publicação

- [ ] Título, gênero, nível, autoria e créditos preenchidos.
- [ ] Direitos de publicação e adaptação comprovados (incluindo tradução e áudio).
- [ ] Cena inicial e pelo menos uma final identificadas.
- [ ] Todas as transições referenciam cenas existentes.
- [ ] Todos os caminhos possíveis alcançam um fim dentro do limite configurado.
- [ ] Todo áudio exigido foi enviado, validado e reproduzido na prévia.
- [ ] Texto e narração correspondem ao mesmo conteúdo.
- [ ] Há perguntas válidas suficientes para todos os percursos.
- [ ] Cada pergunta tem gabarito correto e explicação revisada.
- [ ] Os desafios são compatíveis com o nível do conteúdo.
- [ ] Imagens, efeitos e narrações têm situação de licença documentada.

### RF-036 — Dados operacionais e auditoria

- Registrar autoria das alterações, data, versão publicada e motivo de despublicação.
- Registrar execução de evento, votação, resposta e pontuação de forma auditável.
- Não expor ferramentas administrativas a usuários convidados.

---

## 13. Modelo de dados conceitual

| Entidade | Campos/relações fundamentais |
|---|---|
| `Player` | Identidade convidado, nickname atual, XP, criação |
| `PlayerSession` | Credencial (hash/identificador), expiração, jogador |
| `GameRoom` | Código, host, lotação, status, configurações |
| `RoomParticipant` | Sala, jogador, presença, ready, entrada/saída |
- `GameSession` | Modo (`SOLO_ADVENTURE`, `SOLO_PRACTICE`, `MULTIPLAYER`), `RoomId?`, status, configuração congelada, início/fim |
| `SessionParticipant` | Sessão, jogador, pontos, posição opcional, desistência; 1 solo ou 2–8 multiplayer |
| `Genre` | Nome, ícone, cor, ativo |
| `Story` | Identidade estável, gênero, slug, status do catálogo |
| `StoryVersion` | Nível, texto editorial, fonte/licença, status, publicação |
| `StoryScene` | Versão, tipo inicial/intermediária/final, texto, mídia |
| `SceneTransition` | Cena origem, opção/ordem, cena destino |
| `MediaAsset` | Tipo, chave de armazenamento, MIME, tamanho, hash, licença |
| `Challenge` | Cena/versão, tipo, nível, enunciado, gabarito, peso |
| `ChallengeOption` | Desafio, texto, ordem, indicador interno de acerto |
| `StoryPlaythrough` | Sessão, versão de história, sequência e final escolhido |
| `ScenePlay` | Percurso, cena, ordem, horários da narração |
| `QuestionInstance` | Questão aplicada na partida, prazo, evento associado |
| `PlayerAnswer` | Primeira resposta válida da instância, jogador, horário e pontos |
| `PracticeAttempt` | Tentativas posteriores em Practice, tentativa sequencial, resposta e feedback, sem pontos |
| `DecisionInstance` | Cena, opções e prazo de votação |
| `PlayerVote` | Voto multiplayer, jogador, alternativa, horário |
| `DecisionChoice` | Escolha de caminho em sessão solo, cena/opção, jogador, horário |
| `PlayerStoryProgress` | Jogador, versão, cenas/finais vistos, progresso e últimas questões (opcional, derivável de eventos) |
| `SurpriseEventDefinition` | Tipo, condições, chances e configuração |
| `TriggeredEvent` | Percurso/cena, evento sorteado, resultado |
| `Achievement` | Código e regra de conquista |
| `PlayerAchievement` | Jogador, conquista, data |
| `AdminUser` | Login e controle de acesso |
| `ContentAuditLog` | Autor, ação, entidade, versão e data |

### 13.1 Restrições fundamentais

- `GameRoom.Code` único entre salas válidas; criar índice.
- `RoomParticipant(RoomId, PlayerId)` único enquanto associação for válida.
- `PlayerAnswer(QuestionInstanceId, PlayerId)` **único** para impedir segunda resposta pontuável.
- `PracticeAttempt(QuestionInstanceId, PlayerId, AttemptNumber)` **único** para permitir revisão sem sobrescrever resposta ou acumular recompensas.
- `PlayerVote(DecisionInstanceId, PlayerId)` **único**.
- `DecisionChoice(DecisionInstanceId, PlayerId)` **único** e aplicável somente a sessões solo.
- `GameSession.RoomId` nulo para solo e obrigatório para multiplayer; invariantes de quantidade de participantes em regras de domínio e testes.
- Recompensa XP idempotente por sessão concluída; evitar crédito repetido por retries.
- `PlayerAchievement(PlayerId, AchievementId)` único.
- Índices em `StoryVersion(Status, Difficulty)`, `QuestionInstance(GameSessionId)` e em consultas frequentes por jogador/sala.
- Referências históricas apontam para **versões publicadas imutáveis**; não executar `ON DELETE CASCADE` destrutivo em dados auditáveis.
- Armazenar timestamps críticos em UTC; converter para horário local apenas na UI.

O desenho definitivo de tabelas, chaves, constraints, cardinalidades e migrations será realizado em `DATABASE.md`, antes de implementar os CRUDs.

---

## 14. API e eventos (contrato inicial)

### 14.1 REST — exemplos de recursos

```text
POST   /api/guest-sessions
POST   /api/solo/sessions                   # adventure/practice, nível, história ou roleta
GET    /api/solo/sessions/active
GET    /api/solo/sessions/{id}/snapshot
POST   /api/solo/sessions/{id}/answers     # idempotency key
POST   /api/solo/sessions/{id}/choices     # caminho narrativo
POST   /api/solo/sessions/{id}/pause       # Practice / pausas previstas
POST   /api/solo/sessions/{id}/resume
POST   /api/solo/sessions/{id}/finish
POST   /api/rooms
POST   /api/rooms/{code}/join
GET    /api/rooms/{code}
POST   /api/rooms/{id}/ready
POST   /api/rooms/{id}/start
GET    /api/games/{id}/snapshot
GET    /api/players/me/stats
GET    /api/stories/{id}/summary

POST   /api/admin/auth/login
GET    /api/admin/stories
POST   /api/admin/stories
POST   /api/admin/stories/{id}/versions
POST   /api/admin/stories/{id}/publish
POST   /api/admin/media
GET    /api/admin/analytics/overview
```

### 14.2 SignalR — comandos cliente → servidor (exemplos)

```text
JoinRoom(roomCode)  # identidade obtida da sessão autenticada
SetReady(ready)
ConfirmAudioReady(sceneId)
SubmitAnswer(questionInstanceId, payload, requestId)
SubmitVote(decisionInstanceId, optionId, requestId)
RequestCurrentState()
```

### 14.3 SignalR — mensagens servidor → cliente (exemplos)

```text
RoomUpdated
StoryRouletteStarted
StorySelected
AudioPreloadRequested
SceneStarted
QuestionOpened
QuestionResultRevealed
ScoreboardUpdated
SurpriseEventAnnounced
VotingOpened
VotingClosed
ScenePathSelected
GameFinished
StateSnapshot
```

Nomes são **contratos propostos**, não implementações fechadas. Na implementação, definir DTOs tipados e tratamento padronizado de erros, autorização e idempotência.

---

## 15. VPS, Docker e operações

### INF-001 — Topologia inicial

O mesmo frontend oferece **Solo** (REST) e **Multiplayer** (REST + SignalR). Ambos usam API, engine e PostgreSQL compartilhados; não criar contêiner/serviço separado para solo.

```text
Navegador (Blazor WebAssembly)
    │ HTTPS / WSS
    ▼
Traefik (TLS / roteamento)
    ├── /             → frontend estático (Nginx ou servidor equivalente)
    ├── /api          → ASP.NET Core Web API
    ├── /hubs/game    → ASP.NET Core SignalR (WebSocket)
    └── /media        → assets públicos publicados
                              │
                          ASP.NET Core
                              │
                PostgreSQL (container ou gerenciado)
                              │
                     Volume persistente
                 (áudios, imagens e backups)
```

**Configuração inicial:** única instância de API/SignalR. Não adicionar Redis, Kafka, microsserviços nem orquestração Kubernetes na V1. Se houver crescimento para várias instâncias, projetar backplane/coordenação de estados e afinidade de sessões apropriadamente.

### INF-002 — Containers e rede

- `web`: arquivos estáticos produzidos pelo Blazor WebAssembly; configuração de fallback da SPA.
- `api`: ASP.NET Core, portas internas, health checks e logs.
- `db` (**opcional**): PostgreSQL com volume persistente; usar apenas se couber na VPS após medição.
- `traefik`: pode ser instância compartilhada já existente na VPS; **não implantar outro proxy disputando 80/443**.
- Rede Docker interna para API e banco. **Nunca publicar porta 5432 na internet.**
- Diretório/volume durável para mídias, montado no componente responsável por servi-las.

### INF-003 — Segredos e ambientes

- `Development`, `Staging` (opcional) e `Production`.
- PWA é uma conveniência de instalação: **não promete multiplayer offline**; cache de recursos deve respeitar licenciamento e atualização de versões.
- Configurar connection strings, chaves de autenticação, origens CORS e armazenamento via variáveis de ambiente/secret manager.
- `.env.example` sem valores sensíveis; não versionar `.env`, senhas, dumps e arquivos privados.
- `HTTPS` obrigatório; `WSS` para SignalR e configuração correta de proxy reverso.
- Executar migrations de maneira controlada antes da nova versão entrar em produção; **não aplicar alterações destrutivas automaticamente**.

### INF-004 — Publicação e deploy

1. Fazer push para repositório.
2. Executar build e testes no CI.
3. Publicar imagens de API e frontend (ou artefatos equivalentes).
4. Preparar backup/snapshot antes de migrations sensíveis.
5. Executar migrations controladas.
6. Atualizar containers com Docker Compose.
7. Checar health endpoints, WebSocket, banco e reprodução de mídia.
8. Verificar uma partida real com dois navegadores após o deploy.

### INF-005 — Backup e recuperação

- Backup **diário** do PostgreSQL com política de retenção definida.
- Backup independente de **volumes de áudio e imagens**.
- Guardar cópia **fora da VPS** (volume local sozinho não é backup).
- Testar restauração de banco + mídias em ambiente separado periodicamente.
- Documentar `restore`, rollback de aplicação e procedimento para migrations incompatíveis.
- Evitar exclusões físicas de arquivos/versões referenciados por partidas históricas.

### INF-006 — Desempenho e operação

- Compactar mídia e habilitar caching por URL versionada/hash, com atenção à política de licenças e acesso.
- Áudio/imagens são entregues como arquivos HTTP; **não passar binário pelo Hub**.
- Medir CPU, RAM, disco, conexões ao banco, uso de banda, falhas de WebSocket, erros e buffering.
- Testar o consumo em uma VPS pequena antes de escolher PostgreSQL local versus gerenciado.
- Criar limites razoáveis de upload e de sessões simultâneas; **8 pessoas por sala não significa apenas 8 usuários no servidor**.

---

## 16. Requisitos não funcionais (RNF)

| ID | Requisito verificável |
|---|---|
| RNF-001 | Experiência utilizável em celular, tablet e desktop atuais |
| RNF-002 | Solo funcional com 1 jogador; multiplayer com 2 a 8 por sala, sem perda de eventos de estado |
| RNF-003 | Contagem, pontos, sorteios e prazos controlados no backend |
| RNF-004 | Respostas duplicadas ou fora de prazo rejeitadas de forma idempotente |
| RNF-005 | Reconexão autenticada e snapshot consistente do estado |
| RNF-006 | Feedback para falha de áudio e controles iniciais que evitem bloqueio de autoplay |
| RNF-007 | Imagens/áudios em armazenamento persistente, separados da imagem Docker |
| RNF-008 | Migrations versionadas, backups e restauração testada |
| RNF-009 | HTTPS/WSS, autorização para admin, validação de upload e rate limit |
| RNF-010 | Acessibilidade: contraste, teclado, legendas/texto quando compatível com o evento, fonte ajustável e redução de movimento |
| RNF-011 | Logs estruturados de erro, estado de sala e eventos críticos sem segredos |
| RNF-012 | Testes unitários do motor de jogo e integração com PostgreSQL/SignalR |
| RNF-013 | Suporte a publicação de novas histórias sem rebuild/deploy |
| RNF-014 | Preservar histórico e referências de versões de conteúdo utilizadas |
| RNF-015 | A sala deve indicar estado de rede e evitar telas indefinidamente bloqueadas por um cliente |
| RNF-016 | Solo utiliza exatamente as mesmas versões publicadas, áudios, cenas e questões do multiplayer |
| RNF-017 | Retomada de solo não duplica pontos/XP e identifica corretamente a fase após refresh ou reinício |

---

## 17. Conteúdo inicial e crescimento

### Meta para um MVP realmente jogável

- **Um percurso solo Adventure e um Practice completos**, com retomada, decisão individual e registro de resultados, usando o mesmo conteúdo do multiplayer.

- **6 histórias publicadas**, sendo pelo menos **2 em A1, 2 em A2 e 2 em B1**.
- **5–8 cenas por história**, com narração em cada cena.
- **Ao menos 2 caminhos/finais** em parte do catálogo (mínimo de 3 histórias ramificadas).
- **3 ou mais tipos de pergunta** por história e perguntas suficientes para cada caminho.
- **4 eventos surpresa** plenamente funcionais: Blind Listening, Memory Rush, Double Points e Find the Lie.
- Roleta, salas para 2–8, ranking por velocidade, decisões coletivas e painel admin de conteúdo.

### Meta para V1 completa, após o MVP funcional

- **12 histórias ou mais**, com diversidade de gêneros e dificuldade. O catálogo continuará crescendo após a V1.
- Biblioteca crescente de variantes de perguntas para reduzir repetição.
- Todos os tipos de minijogo e eventos da seção 8.
- Métricas de uso e revisão periódica das histórias menos divertidas/difíceis.

**Observação:** publicar muitas histórias não é pré-requisito para implementar o sistema; construir um **pipeline robusto de cadastro, revisão e publicação** é prioritário. Quantidade de conteúdo cresce continuamente após o lançamento.

---

## 18. Critérios de aceitação (testes de ponta a ponta)

### CA-000 — Single Player

- [ ] Jogador inicia `SOLO_ADVENTURE` e `SOLO_PRACTICE` sem sala ou outro participante.
- [ ] Mesmas cenas e narrações funcionam nos dois modos e no multiplayer, sem duplicação de conteúdo.
- [ ] Aventura calcula pontos por acerto e tempo, enquanto prática permite repetir áudio e receber explicações sem pontuação de velocidade.
- [ ] Jogador toma sozinho decisão que altera a próxima cena e o final.
- [ ] Refresh/reconexão retoma a sessão e não duplica respostas, pontos ou XP.
- [ ] O catálogo evita repetir desnecessariamente histórias/questões já vistas e oferece replay.
- [ ] Eventos inelegíveis para solo não são sorteados e eventos compatíveis funcionam.

### CA-001 — Multiplayer

- [ ] Criar sala de até 8 vagas e conectar 8 navegadores/dispositivos.
- [ ] Participantes visualizam lobby, roleta, perguntas, votação e ranking sincronizados.
- [ ] Jogador pode reconectar sem duplicar vaga nem receber novas respostas indevidas.

### CA-002 — História narrada

- [ ] Story com 5+ cenas é selecionada e reproduz **áudio + texto** quando permitido.
- [ ] Todos precisam liberar áudio por interação e passar por preparação/contagem.
- [ ] O áudio de uma cena está fisicamente armazenado e vinculado no banco por metadados.
- [ ] Falha de arquivo é tratada sem travar a sala.

### CA-003 — Perguntas e velocidade

- [ ] Cada um dos 8 jogadores pode enviar exatamente uma resposta válida por questão.
- [ ] Acertos mais rápidos recebem mais pontos conforme regra registrada.
- [ ] Erros não pontuam; gabarito só é revelado no momento correto.
- [ ] O placar corresponde à soma das respostas persistidas, inclusive após reconexão.

### CA-004 — Narrativa ramificada

- [ ] Votação coletiva escolhe um caminho e todos veem a mesma cena seguinte.
- [ ] Desempate de votação é resolvido pelo servidor e comunicado.
- [ ] Caminhos distintos podem chegar a finais distintos sem acessar cena inválida.

### CA-005 — Eventos e diversidade

- [ ] Quatro eventos da V1 são aplicados somente em cenas compatíveis.
- [ ] História/pergunta não é repetida dentro da mesma partida quando há opções alternativas.
- [ ] Jogadores conseguem iniciar revanche e obter conteúdo diferente quando disponível.

### CA-006 — Administração e conteúdo

- [ ] Admin cria uma história e cadastra cenas, ramificações, áudio, perguntas e respostas.
- [ ] Sistema impede publicação sem áudio, perguntas, caminho válido ou informação de direitos.
- [ ] Nova história publicada aparece no sorteio **sem novo deploy**.
- [ ] Alterar material gera nova versão sem quebrar histórico já gravado.

### CA-007 — VPS e operação

- [ ] Deploy HTTPS com Docker Compose funciona em VPS Linux.
- [ ] PostgreSQL persiste dados entre reinícios e não está público na porta 5432.
- [ ] Áudio e imagens persistem após atualização dos containers.
- [ ] Backup do banco e mídias é restaurável.
- [ ] API, SignalR e arquivos de mídia funcionam atrás do proxy.

---

## 19. Sequência de implementação para aprender C# com IA

### Etapa A — Fundação (C# + banco)

1. Criar solution/projetos, configurar build, testes e PostgreSQL.
2. Criar entidades centrais de conteúdo, DbContext, configurações EF Core e primeira migration.
3. Implementar CRUD de gêneros, histórias e cenas com validação.
4. Criar upload/associação de áudio e validação de publicação.

**Aprendizado:** classes, propriedades, nullable, enums, LINQ, DTOs, injeção de dependência, async/await e migrations.

### Etapa B — Primeira aventura solo ponta a ponta

1. Implementar `GameSession.Mode`, seleção de história, narração, resposta e transição individual.
2. Adicionar `Adventure` (pontuação por acerto/tempo) e `Practice` (pausa, replay e explicação).
3. Persistir escolhas, progresso, conclusão e retomada com API REST idempotente.
4. Testar a mesma história e o mesmo gabarito compartilhado entre os modos.

**Aprendizado:** modelagem de estados, domínio, idempotência, polimorfismo/estratégia quando justificável, SQL e testes.

### Etapa C — Sala e multiplayer

1. Identidade convidado e autorização de participação.
2. Criar/entrar em sala e lobby com SignalR.
3. Máquina de estados de jogo, reconexão e snapshot.
4. Testar duas abas/navegadores antes de testar oito.

**Aprendizado:** interfaces, serviços, concorrência, validação, WebSockets, estado distribuído e testes.

### Etapa D — Primeira partida multiplayer ponta a ponta

1. Publicar **uma história curta** com áudio e perguntas.
2. Roleta → carregar áudio → narrar → abrir pergunta → computar pontos → revelar.
3. Adicionar voto e duas rotas até finais diferentes.
4. Persistir partida completa e exibir resultado.

**Meta:** primeira partida completa antes de polimento visual.

### Etapa E — Variação e diversão

1. Eventos-surpresa com regras declarativas.
2. Tipos adicionais de questões e rotação anti-repetição.
3. Roleta animada, transições, feedback e conquistas.
4. Refinar interface mobile e acessibilidade.

### Etapa F — VPS e catálogo contínuo

1. Compose, Traefik, volume de mídia e backup.
2. CI/CD, health checks, logs e monitoração simples.
3. Ampliação do catálogo via painel admin.
4. Testes reais de carga por sala e de conectividade móvel.

**Papel da IA no desenvolvimento:** explicar conceitos, revisar PRs, gerar testes quando solicitado e discutir alternativas. Antes de aceitar código gerado, o desenvolvedor deve conseguir explicar a regra, as dependências e as exceções; priorizar implementação manual por etapas e evitar grandes blocos de código não compreendidos.

---

## 20. Decisões que ainda precisam de validação

> Estes pontos **não impedem iniciar o núcleo do projeto**, mas precisam de confirmação antes de estabilizar contrato, UX e regras finais.

| ID | Proposta atual | Validar |
|---|---|---|
| DV-001 | Story Battle com `SOLO_ADVENTURE`, `SOLO_PRACTICE` e `MULTIPLAYER` na V1 | [ ] |
| DV-002 | Salas privadas de 2–8; acesso como convidado | [ ] |
| DV-003 | Até 3 histórias por partida; cada uma com 5–8 cenas | [ ] |
| DV-004 | Narração por cena em MP3; texto visível exceto em eventos especiais | [ ] |
| DV-005 | Pontuação por ordem de acerto com janela de empate de 300 ms | [ ] |
| DV-006 | Decisão coletiva por pluralidade; empate sorteado | [ ] |
| DV-007 | 90 segundos de tolerância para desconexão | [ ] |
| DV-008 | Um arquivo de áudio em inglês por cena/versão, sem TTS online | [ ] |
| DV-009 | PostgreSQL na VPS se houver recurso; externo se necessário | [ ] |
| DV-010 | Convidado sem conta permanente; histórico depende de credencial local | [ ] |
| DV-011 | MVP com 6 histórias; expansão editorial contínua | [ ] |
| DV-012 | Painel admin com publicação e controle de direitos | [ ] |
| DV-013 | Single Player sem sala e sem SignalR; mesmas histórias, com escolha individual | [ ] |
| DV-014 | Adventure pontua por acerto/tempo; Practice permite repetir sem XP farmável | [ ] |
| DV-015 | Salvar e continuar sessão solo; jogabilidade online na V1 | [ ] |

---

## 21. Referências técnicas e jurídicas

- .NET 10 LTS: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- Blazor WebAssembly hosting: https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0
- SignalR — grupos e reconexão: https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-10.0
- Npgsql para EF Core: https://www.npgsql.org/efcore/
- Regras de autoplay em navegadores: https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Autoplay
- Backup PostgreSQL: https://www.postgresql.org/docs/current/backup.html
- Direitos autorais — Ministério da Cultura: https://www.gov.br/cultura/pt-br/assuntos/direitos-autorais/perguntas-frequentes/perguntas-frequentes

---

**Documento preparado para validação (v1.3).** Após aprovação, consolidar decisões em `GAME_RULES.md`, o esquema físico em `DATABASE.md` e os contratos em `API.md`. `CODING_STANDARDS.md` e `ARCHITECTURE.md` acompanham este rascunho.
