# Abertura, roleta e ambientação

Incremento de RF-006, RF-008 e RF-015 solicitado em 07/11/2026: o jogador inicia antes de descobrir o tema. A escolha manual de história foi removida das telas solo e multiplayer. A seleção explícita da API solo continua disponível para compatibilidade com integrações e testes editoriais.

## Critérios de aceite

- Home com abertura de jogo, roleta animada, acesso equivalente a solo e grupo, instruções e trilha opcional acionada pelo usuário.
- Iniciar solo solicita uma história ao servidor sem enviar escolha. O servidor sorteia primeiro o gênero e depois uma versão publicada desse gênero. A animação apresenta o resultado; não decide o sorteio.
- Após uma resposta válida do servidor, a roleta apresenta o resultado por até três segundos e a primeira cena abre automaticamente. “Abrir história agora” pula a apresentação. A narração continua exigindo um clique próprio. Recarregar recupera a aventura já persistida, sem sortear novamente.
- No lobby, o anfitrião apenas inicia o giro. Pelo menos dois participantes conectados e todos prontos são obrigatórios. O resultado e o início UTC ficam no checkpoint JSON da sala, compartilhados por snapshot e notificação SignalR. Uma segunda tentativa não troca o resultado. Não há migração de banco neste incremento.
- Reconectar durante o giro usa o tempo transcorrido do servidor para posicionar a animação. Consultas periódicas atualizam presença; o cliente não decide regras nem deadlines.
- A imagem publicada de cada cena preenche o viewport com `object-fit: cover`. Há recorte proporcional para acomodar a tela; texto e controles continuam em painéis legíveis. Sem imagem, há um fundo atmosférico. Imagens animadas mantêm controle para reproduzir/ocultar.
- Gêneros `Terror`, `Horror`, `Terror psicológico` e `Psychological horror` ativam paleta escura, vinheta, névoa lenta e música de suspense. A aparência não modifica texto, gabarito, prazo ou fluxo.
- A trilha original é sintetizada localmente com Web Audio, sem assets grandes ou serviços externos. Há botão de ligar/desligar independente da narração, volume reduzido durante fala, silêncio quando a aba está oculta e limpeza ao sair da página.
- “Como jogar” abre um diálogo nativo com teclado, Escape e retorno de foco. Explica passos, caminhos, prazos e os nove eventos do catálogo previsto, identificando claramente os eventos ainda indisponíveis. Acessibilidade por `prefers-reduced-motion` e configuração editorial `Motion = none` preservadas.

## Responsabilidades e C#

| Código | Papel |
|---|---|
| `StoryRoulette` (Application) | Agrupa versões publicadas por gênero normalizado e realiza dois sorteios com `RandomNumberGenerator`. Um `Func<int,int>` opcional permite cenários determinísticos nos testes. Não expõe entidades/gabaritos. |
| `DemoSessions` (API) | Resolve o sorteio quando não há ID explícito e cria a aventura persistida existente; acrescenta o gênero público ao snapshot. |
| `LobbyRoom.Spin` (Domain) | Valida anfitrião, versão, conectividade, prontidão e resultado único. Retorna um novo record de estado com resultado e instante UTC recebido da camada externa. |
| `RoomEndpoints` (API) | Expõe `POST /api/rooms/{id}/spin` com `SpinRoomRequest(ExpectedStateVersion)`, autenticação, CSRF e rate limit; sorteia no servidor e persiste sob o lock existente. Substitui a rota de escolha manual `/story`. |
| `StoryWheel` (Web) | Desenha os gêneros públicos e anima até o setor do resultado. O atraso inicial é capturado uma vez por resultado para não acelerar o giro a cada renderização. Em telas pequenas, o início do giro traz a roleta para a área visível. |
| `GameMenu` / `game-atmosphere.js` (Web) | Manual, foco de diálogo, ativação de áudio por gesto, ambiente, redução de volume e descarte de recursos. |
| `ThemePalette` / `game.css` (Web) | Paleta, terror, botões, cena em tela cheia, layouts responsivos e movimento reduzido. |

## Validação

```sh
dotnet restore EnglishRoulette.sln --locked-mode
dotnet build EnglishRoulette.sln --configuration Release --no-restore
dotnet test EnglishRoulette.sln --configuration Release --no-build
dotnet format EnglishRoulette.sln --verify-no-changes --no-restore
```

Para incluir PostgreSQL real, configure `ENGLISHROULETTE_TEST_CONNECTION` para um banco **de testes**; as fixtures criam e removem seus próprios schemas. Os testes cobrem seleção em duas etapas, catálogo vazio/pequeno, exclusão de rascunhos/arquivados, prontidão, autorização, prazo de presença, conflito de versão, persistência compartilhada e tentativa de trocar o resultado pelo payload.

Validação manual em desktop e celular: abra `/`, ligue/desligue a trilha, abra o manual e feche com Escape; inicie, acompanhe o giro e entre. Confira a imagem de fundo e a leitura. Inicie a narração, responda e avance. Recarregue durante a história. Com uma história de terror publicada, confira a ambientação e a redução da trilha durante a fala. Em duas sessões de navegador distintas, entre na mesma sala, confirme prontidão e gire como anfitrião; ambos devem ver o mesmo resultado. Recarregar não permite outro sorteio. Ative movimento reduzido no sistema e repita.

## Limites e decisões de produto

- A autorização desta solicitação altera o fluxo padrão descrito em RF-SOLO-001: a UI começa com roleta, sem escolha manual. No multiplayer, o anfitrião inicia e não determina gênero/história.
- A partida simultânea multiplayer ainda não foi implementada; este incremento entrega lobby e sorteio compartilhado. O manual e a sala informam isso. Pontuação, XP e os eventos especiais também continuam pendentes, sem promessas de disponibilidade.
- `DECISION NEEDED`: filtros de nível, gêneros elegíveis, pesos, histórico e política de repetição entre novas aventuras. O sorteio atual usa todo o catálogo publicado, com chance uniforme por gênero e por história dentro do gênero, sem impedir replay. A UI informa catálogo de um único gênero e possibilidade de repetição.
- `DECISION NEEDED`: ciclo completo de revanche/reinício da mesma sala após o sorteio. Por ora, cada sala tem um resultado único; para outro sorteio, sair e criar outra sala. Checkpoints de lobbies anteriores com seleção manual continuam legíveis; um novo giro exige as validações atuais.
- A identificação de terror usa os nomes de gênero acima; um cadastro formal de gêneros/ambientações poderá substituir esse mapeamento.

## Evidência deste incremento

Restore em locked mode, build Release com zero warnings, 82 testes unitários e 32 testes de integração com PostgreSQL real passaram. `dotnet format --verify-no-changes` passou. QA automatizado no Chrome cobriu desktop e celular, ausência de overflow horizontal, manual/Escape, trilha, reprodução de narração, retomada, novo jogo após descarte do player, movimento reduzido e roleta compartilhada entre duas sessões de navegador. A cobertura visual de terror e da imagem em tela cheia usou um snapshot e uma ilustração de teste interceptados no navegador; não publicou uma nova história nem mídia de terror no catálogo.


## Correção do início solo e clareza da Home

### Diagnóstico antes da edição

Em 07/11/2026, o site publicado foi inspecionado no Chrome em 1440×1000 e 390×844. `GET /api/catalog` respondeu com histórias A2 publicadas e `POST /api/demo/session` respondeu 200 com um snapshot real na fase `Preloading`. O giro terminou em aproximadamente cinco segundos, porém a tela continuou na Home, mesmo após doze segundos. `CreateAsync` só preenchia `_pendingAdventure`; a transição para `_state` existia exclusivamente no segundo botão, “Entrar na história”. No celular, esse botão ficava abaixo da área visível (o cartão terminava perto de 890px numa tela de 844px). Esse é o defeito de navegação confirmado. O texto “A roleta está girando…” **não permaneceu indefinidamente** nas sessões limpas observadas; não há evidência suficiente para atribuir esse relato a uma falha específica do servidor.

Também foram reproduzidas, com interceptação apenas no navegador, duas falhas adicionais: JSON inválido causava `JsonException` sem mensagem útil na UI; falha no módulo visual causava uma exceção de renderização no componente da roleta. Essas situações agora têm tratamento próprio. O 404 inicial de `/api/demo/session` em um navegador sem cookie significa ausência de progresso salvo e é esperado. Ao percorrer a história completa, a regressão também encontrou uma referência ao player já descartado após `StoryResult`, que impedia retornar ao menu ou recomeçar. `SetState` agora limpa a referência sempre que o player deixa de ser renderizado.

### Fluxo e critérios de aceite atualizados

1. A Home explica cena narrada com texto, pergunta com feedback e escolhas nas bifurcações. Mostra apenas gêneros e níveis realmente retornados pelo catálogo. O nível descreve a complexidade do inglês; não há novo filtro de dificuldade.
2. O clique principal entra em `Requesting`; só uma resposta válida da API permite `Spinning`. A sessão continua sendo sorteada e persistida no servidor por `StoryRoulette` e `DemoSessions`.
3. A apresentação visual é limitada a três segundos, pode ser pulada e termina com a abertura automática da cena real. Não depende de `animationend`, JavaScript de rolagem ou movimento habilitado. O estado de ocupação é liberado no `finally`, com renderização explícita. Ao entrar ou recuperar uma aventura, o foco vai para o título da história, corrigindo a rolagem que permanecia na posição da roleta no celular. Falhas da rolagem opcional são registradas, sem impedir a abertura.
4. Cada operação de `AdventureClient` tem limite de quinze segundos, incluindo leitura de JSON. Timeout, rede, resposta inválida e falta de conteúdo produzem mensagens recuperáveis. O cancelamento por saída da página é preservado. Falha de catálogo fica separada da ausência de sessão; atualizar uma delas não apaga o erro da outra.
5. Sem histórias, o início tem explicação junto ao botão e há “Atualizar catálogo”. Após uma falha de início, é possível tentar novamente ou recuperar a sessão que o servidor possa ter criado. Não há repetição automática de POST.
6. `GET /api/catalog/round-preview` retorna um trecho de até 420 caracteres da cena inicial de uma versão publicada, com título, nível, gênero, enunciado e textos de alternativas. Não retorna gabarito, explicação, entidades Domain ou estado de sessão. Não cria cookie nem progresso; catálogo sem conteúdo publicado retorna 204. A prévia é explicitamente de leitura e não avalia respostas.
7. O motor permanece `Preloading → Narrating → QuestionOpen → QuestionReveal → DecisionOpen/Próxima cena → StoryResult`. Prazos e primeira resposta continuam validados pelo servidor. Áudio mantém pré-carregamento, reprodução por gesto e recuperação de erro. Escolhas seguem o grafo publicado e o progresso permanece no checkpoint e cookie existentes.
8. A paleta, tipografia, marca, áudio ambiente e componentes existentes foram preservados. Foram ajustados textos, hierarquia, contraste, contexto e legenda da roleta. A seleção do lobby multiplayer mantém seu giro de cinco segundos; não passa a abrir uma partida simultânea inexistente.

### Arquivos e responsabilidades

| Arquivo | Alteração |
|---|---|
| `Web/Pages/Home.razor` | Estados de início, transição automática, recuperação, texto e hierarquia da Home. |
| `Web/Services/AdventureClient.cs` | Timeout por operação, validação dos snapshots e erros HTTP/JSON recuperáveis. |
| `Web/Components/StoryWheel.razor` | Duração configurável, legenda legível, gêneros reais e rolagem opcional protegida contra erro. |
| `Web/Components/RoundPreview.razor` | Trecho real do catálogo, estado vazio/erro e nova tentativa de carregamento. |
| `Web/Components/NarrationPlayer.razor` | Rótulo neutro para narrações publicadas; não presume que todo áudio seja uma demonstração sintética. |
| `Web/wwwroot/css/game.css` | Ajustes sobre a identidade atual, hierarquia responsiva e apresentação da prévia. |
| `Contracts/RoundPreviewView.cs` | DTO público restrito à prévia, sem gabaritos. |
| `Api/Admin/AdminEndpoints.cs` | Endpoint público de leitura de uma rodada publicada; administração mantém autorização existente. |
| `tests/EnglishRoulette.IntegrationTests/AdventureClientTests.cs` | HTTP real via handler controlado: sucesso, retry, resposta inválida, timeout, cancelamento e catálogo indisponível. |
| `tests/EnglishRoulette.IntegrationTests/AdminCatalogTests.cs` | Publicação/arquivamento da prévia e ausência de gabaritos, cookies e progresso. |
| `tests/browser/adventure.cjs` | Regressão de início automático, percurso real, feedback, bifurcação, retomada, falhas, desktop/mobile e lobby. |

### Executar a regressão no navegador

Use uma instância **local de QA**, com PostgreSQL isolado e uma história publicada com áudio. O teste percorre o conteúdo real, aguarda o relógio do servidor, responde, escolhe um caminho e confirma a conclusão. Não substitui a API saudável por dados fabricados. Interceptações ficam restritas aos cenários explícitos de erro, catálogo vazio e recurso visual indisponível.

```sh
ENGLISHROULETTE_BASE_URL=http://127.0.0.1:5087 \
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/para/playwright \
ENGLISHROULETTE_CHROME=/caminho/para/chrome \
ENGLISHROULETTE_SCREENSHOTS=/tmp/englishroulette-qa \
node tests/browser/adventure.cjs
```

O Playwright pode ser fornecido por um ambiente de QA já instalado; o caminho é opcional quando `require('playwright')` está disponível. `ENGLISHROULETTE_ONLY_MULTIPLAYER=1` executa apenas a verificação de lobby em duas sessões. Screenshots são opcionais e ficam fora do Git. Execute também restore locked, build Release, testes com `ENGLISHROULETTE_TEST_CONNECTION` e verificação de formatação conforme os comandos acima.


### Evidência da correção

- Restore locked, build Release sem warnings, 82 testes unitários e 40 testes de integração/cliente passaram, sem testes ignorados, usando PostgreSQL isolado. Verificação de formatação e `git diff --check` passaram.
- No Chrome, uma aventura real foi concluída com três perguntas e uma bifurcação; feedback e progresso permaneceram após recarregar, e foi possível voltar ao menu e iniciar outra aventura após o final.
- A Home e a primeira cena foram conferidas visualmente em desktop (1440×1000) e celular (390×844), sem overflow horizontal. A prévia na tela foi comparada com a resposta real do endpoint.
- Falhas HTTP 409/503, JSON inválido, catálogo vazio/indisponível, timeout, nova tentativa, ausência do módulo visual e movimento reduzido foram exercitados no navegador. Falhas de início não produziram um giro com resultado inventado.
- O teste de chegada no celular confirmou foco no título e rolagem no topo; antes desse ajuste, o título ficava cerca de 400px acima da área visível.
- Duas sessões distintas de navegador validaram prontidão, roleta e resultado compartilhado do lobby. Os testes de administração, mídia privada e checkpoints permaneceram aprovados.

Essa primeira validação usou o catálogo local. Depois, a Antologia 01 foi produzida e publicada pela API administrativa, com os comprovantes em `content/anthology-01/` e o fluxo de mídia em `MEDIA_PRODUCTION.md`.

### Artes animadas e desfechos

O componente `SceneArtwork` mantém controles manuais nas prévias editoriais. No fundo da aventura, `Background=true` exibe a arte imediatamente, inclusive GIFs, com carregamento prioritário e sem controles escondidos atrás do jogo. Uma falha de imagem é informada no cartão da cena e não impede texto ou áudio. Com `prefers-reduced-motion`, os GIFs de fundo são ocultados e permanece a ambientação estática do tema; os assets atuais não possuem um poster estático separado.

A tela `StoryResult` apresenta o título e o texto do desfecho real, além de um player nativo livre para sua narração. O player de narração com prazo continua sendo descartado ao terminar a partida, evitando chamadas para referências JavaScript já removidas. O backend continua decidindo a fase final; essa apresentação não acrescenta perguntas nem altera pontuação ou progresso.

O roteiro de navegador confere uma aventura real com quatro perguntas e uma bifurcação usando The Blue Envelope, compara texto/áudio do final ao snapshot do servidor e verifica carregamento das artes em desktop e celular. Também cobre imagem indisponível, movimento reduzido, recuperação de erros e preservação do lobby multiplayer. Os 94 testes unitários e os 40 testes de integração passaram com PostgreSQL isolado; restore locked, build Release sem warnings e formatação foram conferidos.

### Leitura progressiva e cena desobstruída

Incremento solicitado pelo responsável com uma referência visual de The Second Cup: reduzir o player e os painéis para preservar a imagem de fundo, revelando as letras durante a narração.

- `NarrationPlayer` conserva o áudio nativo, controles, pré-carregamento, erro/retry e reprodução por gesto. Na aventura, a barra tem 34px de altura e no máximo 330px de largura; a mensagem de prazo permanece discreta.
- `Home` concentra a leitura na região inferior, recolhe o percurso em `details` e mantém perguntas, decisões e desfechos. O painel opaco e os slogans durante a escuta foram retirados. A administração e a Home inicial continuam com os mesmos componentes e fluxo.
- `ProgressiveNarration` apresenta o texto em inglês com uma cópia acessível completa para leitores de tela. Reserva o espaço do parágrafo para evitar saltos de layout e oferece “Mostrar texto completo”.
- `audio-player.js` usa `audio.currentTime / audio.duration` para revelar grafemas Unicode. O efeito pausa, recua/avança ao buscar no áudio e retoma pelo instante real após refresh. Listeners e frames são liberados ao trocar de cena/sair. O texto completo aparece depois da narração, quando solicitado ou com movimento reduzido no sistema/tema.

Os WAVs publicados não incluem alinhamento por palavra/fonema. A revelação é proporcional ao progresso real do áudio, não uma transcrição temporizada precisa. Não é guiada pelo contador da pergunta e não altera o relógio, as respostas, as fases ou o checkpoint do servidor. Pausar o áudio continua sem suspender o prazo do Solo Adventure.

Critérios de aceite: player compacto em desktop/celular; cenário visível sem o cartão opaco; letras acompanhando reprodução/pausa/busca/retomada; alternativa de texto completo e movimento reduzido; percurso expansível; pergunta, escolha, desfecho, retry e lobby preservados. `tests/browser/adventure.cjs` verifica esses comportamentos com narração real, incluindo a igualdade do deadline antes/depois de pausar. Para uma revisão visual do tema de terror, The Second Cup pode ser copiada ao catálogo isolado de QA pela API ADMIN, sem editar versões da VPS.
