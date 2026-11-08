# Multiplayer jogável — incremento

Solicitação: completar o fluxo que hoje termina no lobby/roleta. A regra de pontuação por ordem dos acertos, com janela ancorada de 300ms, foi confirmada pelo responsável nesta sessão.

## Critérios de aceite

- De 2 a 8 identidades distintas confirmam prontidão; o anfitrião tem uma ação de início visível. A roleta sorteia no servidor e leva todos à mesma versão de história.
- Cada cena exige áudio carregado e um gesto de ativação dos conectados. Todos recebem a mesma contagem regressiva e os mesmos instantes de narração/pergunta. Falha de mídia oferece retry e uma preparação vencida termina com motivo explícito.
- Cada participante responde uma vez por pergunta. O servidor registra ordem e acerto e só revela respostas/explicação/pontos depois do fechamento. Duplicatas não pontuam novamente.
- Pontos por posição: 200/180/160/140/120/100/80/60; acertos em até 300ms da primeira resposta de cada faixa compartilham sua posição. Não há agrupamento por efeito dominó.
- Bifurcações recebem um voto por participante; maioria vence e empate/abstenção são resolvidos e registrados no servidor. Todos seguem o mesmo caminho até um final real e um placar persistido.
- Refresh, perda do SignalR e reinício da API recuperam snapshots. Relógios/deadlines não reiniciam. Sem quórum após a tolerância de presença, a partida é cancelada sem declarar vencedor.
- Solo, catálogo/administração, mídia e checkpoints antigos de lobby continuam funcionando. Nenhum gabarito ou checkpoint interno é serializado nos contratos públicos.

## Incremento e decisões operacionais

Reutilizar `StoryFlowEngine`, `ChallengeEvaluator`, catálogo, entrega de mídia, autenticação, CSRF, notificações e locks existentes. O checkpoint da partida fica como campo opcional no JSON privado da sala, mantendo compatibilidade com salas anteriores e sem nova tabela/migração para este incremento.

Defaults deste incremento: roleta de 5s; preparação de áudio até 45s; contagem de 3s; pergunta de 15s; resultado de pergunta por 5s; votação de 15s; tolerância de presença de 90s. Esses prazos são capturados na partida e documentados como parâmetros operacionais, não regras definitivas de produto. A linha do tempo continua autoritativa no servidor.

Escopo jogável: uma história por partida, perguntas de múltipla escolha publicadas, votação, pontos da partida e placar. XP, eventos especiais e revanche na mesma sala não são apresentados como disponíveis. `DECISION NEEDED`: política editorial desses recursos e eventuais filtros de nível/gênero para partidas futuras.

## Responsabilidades e validação

O domínio implementa estados, aceitação idempotente, prazos, apuração e votação sem HTTP/EF/Blazor. A infraestrutura serializa o checkpoint sob o lock da sala. A API mapeia somente snapshots públicos filtrados por participante. O cliente usa os componentes de cena/áudio e informa falhas; nunca decide resposta, pontos, sorteio ou prazo.

Validar regras com relógio e aleatoriedade controlados, concorrência/persistência com PostgreSQL isolado e uma história real em duas sessões independentes de navegador, cobrindo do lobby ao placar, votação empatada, retry, áudio bloqueado, refresh e fallback sem SignalR.

## Contrato e recuperação

`RoomView.Match` é opcional para compatibilidade. `POST /api/rooms/{id}/commands` aceita `requestId`, `expectedStateVersion` da **fase da partida**, `sceneInstanceId`, `kind` (`audio-ready`, `answer`, `vote`) e `key` quando necessário. Requer cookie Player, participação, CSRF e rate limit. Nenhum campo enviado pelo cliente define horário, acerto ou pontos. A versão de fase não muda para prontidões/respostas/votos independentes na mesma fase; o lock serializa esses envios sem rejeitar concorrência legítima. `requestId` repetido com mesmo comando é idempotente; reutilização com outro conteúdo é conflito.

O snapshot pessoal confirma o envio sem acerto/explicação/pontos da rodada enquanto a pergunta está aberta. No fechamento, publica feedback e pontos; placar final declara vencedores somente em `StoryResult`, permitindo empate. Transições públicas têm chave/rótulo, nunca destino nem gabarito. `MatchDecisionView` torna desempates/abstenções auditáveis no percurso já resolvido.

`LobbyStore` faz leitura/alteração e avanço sob `FOR UPDATE`, recarrega entidades já rastreadas após adquirir o lock e só reconstrói memberships quando a composição muda. `RoomMatchWorker` avança partidas a cada segundo mesmo sem clientes; notifica somente depois do commit. GETs também avançam fases e renovam presença. O SignalR comunica apenas sala/versão; snapshots por consulta continuam funcionando quando ele está indisponível. Refresh/reinício usa o mesmo JSON, sem reiniciar prazos; conteúdo arquivado ainda é resolvido pela versão. Salas antigas com sorteio e sem partida são vinculadas à versão sorteada sob o mesmo lock.

O player sincronizado oferece mute e retomada por gesto se autoplay for bloqueado. Não oferece pause/seek competitivo. A apresentação se alinha aos instantes do snapshot; o progresso das letras segue o relógio real do áudio. Falha de download oferece recarregar; falta de confirmação no limite de preparação cancela com motivo, sem loop infinito. Perguntas já abertas expiram normalmente durante reconexão. Entre cenas, menos de dois conectados aguarda a tolerância de presença; abandono explícito ou expiração que deixe menos de dois participantes cancela sem vencedores.

Validação manual: criar sala em dois navegadores/perfis, marcar ambos prontos e iniciar pelo anfitrião; confirmar áudio nos dois, responder, conferir resultado após fechamento, votar em caminhos diferentes, continuar até placar. Atualizar durante narração/pergunta/final deve recuperar os mesmos instantes e pontuação. Desligar SignalR deve manter as fases por consulta; falhar mídia deve exibir retry e prazo de preparação. `node tests/browser/multiplayer.cjs` percorre esse fluxo contra QA local com histórias reais publicadas e narradas; `adventure.cjs` também o inclui na regressão solo.

Limite deste incremento: a preparação exige confirmação por cena, facilitando a liberação de áudio nos navegadores e a detecção de mídia indisponível. Durações são parâmetros congelados no checkpoint (`MultiplayerRules`), e não valores controlados pela UI. Para jogar outra história, os participantes saem e criam uma sala nova; o fluxo não anuncia revanche automática nem recompensas de perfil.
