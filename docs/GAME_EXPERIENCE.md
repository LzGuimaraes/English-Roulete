# Abertura, roleta e ambientação

Incremento de RF-006, RF-008 e RF-015 solicitado em 07/11/2026: o jogador inicia antes de descobrir o tema. A escolha manual de história foi removida das telas solo e multiplayer. A seleção explícita da API solo continua disponível para compatibilidade com integrações e testes editoriais.

## Critérios de aceite

- Home com abertura de jogo, roleta animada, acesso equivalente a solo e grupo, instruções e trilha opcional acionada pelo usuário.
- Iniciar solo solicita uma história ao servidor sem enviar escolha. O servidor sorteia primeiro o gênero e depois uma versão publicada desse gênero. A animação apresenta o resultado; não decide o sorteio.
- O resultado aparece após o giro; “Entrar na história” leva ao pré-carregamento. A narração continua exigindo um clique próprio. Recarregar recupera a aventura já persistida, sem sortear novamente.
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
