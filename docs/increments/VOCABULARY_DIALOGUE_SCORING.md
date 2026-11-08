# Vocabulário assistido, pontuação solo e cenas com diálogos

Atualização do responsável em 08/11/2026: a retomada solo após sair/recarregar foi removida. Checkpoints continuam garantindo consistência da página aberta; uma nova visita exige começar do zero. Veja [SOLO_PAGE_LIFETIME.md](SOLO_PAGE_LIFETIME.md). A reconexão multiplayer continua existente.

Solicitação: dicas contextuais para palavras menos conhecidas, com desconto na pontuação que seria ganha na rodada, pontos no modo individual, cenas mais fluidas e suporte a caixas de diálogo.

## Critérios de aceite

- Vocabulário é conteúdo editorial por cena: palavra/expressão em inglês e tradução contextual em português. A tradução só é enviada ao próprio jogador após consumo autorizado da dica.
- A mesma dica consultada novamente na mesma rodada não causa nova cobrança. Servidor valida participação, cena, fase, prazo e resposta já enviada. Refresh/retry não duplica uso nem pontos.
- Desconto se aplica somente aos pontos da rodada, nunca cria saldo negativo e não altera a ordem dos acertos do multiplayer. Erro/timeout continua zero.
- Solo apresenta pontos da rodada, desconto, total e resumo do percurso, persistidos e calculados no backend.
- Cenas podem ter falas com personagem, texto e intervalos explícitos no áudio da cena. Publicação valida cobertura do texto, ordem dos intervalos e limites da gravação. Narração comum continua funcionando.
- Caixas de diálogo e transições visuais respeitam movimento reduzido, pause/seek solo, linha do tempo compartilhada multiplayer e os limites responsivos existentes.
- Rascunhos/editor/importador permitem incluir vocabulário e falas. Versões publicadas permanecem imutáveis; partidas em andamento mantêm sua versão.

## DECISION NEEDED

Perguntas enviadas ao responsável nesta sessão, ainda pendentes:

1. Custo de cada palavra: proposta de 10% dos pontos da rodada, até 50%, ou custo fixo de 10 pontos, ou 20% até 60%. Reabrir a mesma dica é gratuito.
2. Pontuação solo: proposta já documentada de 150/125/100 por acerto conforme o tempo restante (70%/40%/demais acertos), ou valor fixo de 150.

Valores provisórios configuráveis, comunicados durante a implementação: 10% por palavra, limite 50%, solo 150/125/100. A escolha final permanece DECISION NEEDED; não tratar estes defaults como regra definitiva de produto. `Gameplay:Hints:*` e `Gameplay:Solo:*` podem ajustar a proposta em novas partidas. Uma partida congela a política com que começou. Descontos percentuais fracionários são arredondados para cima; nunca passam do ganho da rodada. As faixas solo usam tempo restante no servidor (≥70%, ≥40%, >0). Partidas anteriores, sem política no checkpoint, mantêm a regra original e só recebem pontuação/dicas em uma nova aventura.

## Desenho do incremento

`SceneSupport` agrega vocabulário e falas, independente de arquivos/HTTP/EF. A cena e os checkpoints privados capturam esses dados imutáveis. Uma coluna JSON por cena mantém as anotações editoriais; a mudança de esquema é somente aditiva. Contratos administrativos recebem o conteúdo completo; contratos de jogo filtram as traduções por jogador.

Dicas e pontuação são comandos/regras do domínio, persistidos nas transações/CAS existentes. Política de pontos e descontos é congelada por partida para que um ajuste futuro não altere uma rodada já iniciada. O cliente apenas mostra custos e resultados do snapshot.

Para diálogos, continuar usando o áudio publicado da cena e cues explícitos. Nomes de personagens identificam falas; isso não implica novas vozes na gravação. Para maior fluidez de personagens, recomendar produção offline de loops WebM/MP4 com poster; este incremento melhora a passagem entre artes e falas existentes, sem prometer movimento que a mídia não contém.

Validação: testes de conteúdo, arredondamento, limites, cobrança única, idempotência, deadline, concorrência e checkpoint antigo; integração PostgreSQL/editor/API sem vazamento de traduções; percurso real solo e multiplayer com dicas e falas em QA; revisão de desktop/mobile e movimento reduzido com Impeccable.

## Pódio e continuação da sala

Solicitação adicional explícita: pódio com primeiro, segundo e terceiro lugares, demais participantes e opção de jogar outra história mantendo pontos ou sair ao menu.

- Pódio calculado no servidor pelo total acumulado da sala; a UI também mostra pontos ganhos apenas na história concluída. Empates usam classificação competitiva (1, 1, 3), sem desempate arbitrário pelo nome. Participantes que saíram aparecem na lista, sem colocação ativa.
- O anfitrião prepara a próxima partida após `StoryResult`. A mesma sala/código/participantes e pontuações continuam; prontidão, sorteio, cenas, dicas, respostas e votos da nova partida começam novos. Cada jogador confirma prontidão de novo.
- `PrepareNext` é transacional sob o lock de sala, exige anfitrião, versão e ID da partida concluída. Retry no lobby já preparado é inofensivo; um pedido antigo não reinicia uma partida nova.
- Pontos são do encontro nesta sala, não um ranking global. Jogadores novos entram com zero. Sair ao menu chama a saída autenticada antes da navegação. A expiração e a troca de anfitrião existentes continuam válidas.
- `DECISION NEEDED`: confirmar classificação competitiva nos empates e política para carry-over de jogadores que saem/voltam. Implementação inicial preserva por identidade enquanto o histórico dessa pessoa está no placar privado da sala; preparar a próxima partida conserva apenas membros atuais, e novas identidades não herdam pontos por apelido.

## Conteúdo inicial

30 traduções contextuais distribuídas nas 15 cenas de `The Second Cup`, `A Visitor Made of Light` e `The Inventory of Empty Rooms`. Três cenas têm falas: `two-cups`, `courtyard-exit` e `old-workshop`. Duas cenas têm áudio sintetizado offline por fala e concatenado; intervalos são medidos dos frames PCM. Em `two-cups`, preservou-se a gravação original já verificada, pois a síntese isolada de “Hello?” se mostrou ambígua no reconhecimento. Seus intervalos editoriais foram alinhados à palavra no áudio original (base.en), sem estimar pela extensão do texto. O mesmo modelo/voz lê narrador e personagens; não há múltiplas vozes anunciadas. `produce_dialogue_audio.py` permite repetir essa produção. Mídias/relatórios permanecem fora do Git; narração textual e gabaritos são privados no pacote de autoria.

## Como validar e responsabilidades

- `SceneSupport`: conteúdo editorial e validação de termos/falas; `AssistanceRules` e `SoloScoringRules`: cálculo puro no domínio. `SoloAdventureSession` e `MultiplayerGame` registram consumo, pontos e recibos. `LobbyRoom.PrepareNext` mantém totais e prepara nova prontidão sob a transação existente.
- `GameAssistance`: projeção pública, com tradução apenas para o jogador que a consumiu. Contratos não contêm gabaritos. A migração `SceneSupport.Up` só acrescenta `support_json` com default vazio.
- `WordHints`, `SoloScore`, `MatchPodium`, `ProgressiveNarration` e `audio-player.js`: apresentação; nenhum cálculo de ganho ou prazo é autoritativo no navegador.
- `publish_scene_support.py` duplica a versão publicada, verifica que os textos continuam iguais e valida a nova publicação antes de trocar a versão. `--publish-reviewed` publica o conteúdo já revisado. Secrets entram por ambiente/arquivo local e nunca são registrados no comando nem no Git.

Com PostgreSQL de testes configurado, executar restore locked, build Release, testes e format verify conforme `AGENTS.md`. Com QA local narrado:

```sh
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/playwright node tests/browser/assistance.cjs
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/playwright node tests/browser/adventure.cjs
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/playwright ENGLISHROULETTE_LAYOUT_MODE=solo node tests/browser/layout.cjs
```

`assistance.cjs` percorre diálogos/dicas reais e uma história completa com oito identidades, valida os nove viewports, sai ao menu, prepara outra partida e confere o total preservado. `ENGLISHROULETTE_FEATURE_MODE=solo|multiplayer` permite repetir um trecho após uma correção localizada. `ENGLISHROULETTE_AUDIT_PODIUM=1` abre CDP 9223 e mantém o pódio por até dois minutos para uma auditoria do Impeccable; a aba tem fragmento `qa-podium-host`. Capturas, credenciais, áudio e relatórios brutos permanecem em diretórios ignorados.

## Resultado da validação

- Restore com `--locked-mode`, build Release sem warnings e format verify aprovados. **137 testes de domínio + 46 de integração = 183 testes .NET**, incluindo JSON de checkpoint antigo sem campos de assistência. **14 testes de autoria + 10 de deploy** aprovados.
- Regressão completa de `adventure.cjs`: solo concluído (quatro perguntas e uma decisão), recuperação de criação, conteúdo/JSON inválido, timeout, retomada, mídia indisponível, mobile, autoplay bloqueado e multiplayer sem SignalR, com quatro perguntas e uma votação. Um teste foi estabilizado lendo o snapshot durável após o POST: Chromium pode descartar o corpo de uma resposta finalizada durante percursos com muita mídia; a UI continua precisando aceitar o POST e exibir a aventura real.
- **90 combinações de fase/viewport consolidadas**, aprovadas: 54 do percurso solo, 18 de diálogo/resultado solo e 18 de pódio/lobby seguinte. Nove dimensões de 1920×1080 a 320×568. Desktop/tablet saudável sem rolagem de página; mobile permite leitura vertical. Os controles essenciais permanecem dentro do viewport no desktop.
- Percurso real de oito identidades até o pódio; um jogador saiu ao menu, sete conservaram seus totais e abriram outra partida, com respostas/votos/dicas limpos e nova prontidão. Recarregar o resultado preservou a classificação. Diálogo validado com pause/seek, texto completo e movimento reduzido; pontuação solo e dica permaneceram após reload.
- Foram eliminadas mensagens idênticas de prontidão que apareciam na barra de início e abaixo do botão. A orientação completa fica junto à ação principal. Em tablets baixos, presença e pontos usam uma apresentação compacta com descrição acessível, evitando que o lobby invada a barra de início.
- Impeccable: detector estático dos componentes/CSS sem achados; revisão renderizada corrigiu bordas de destaque dos novos elementos e texto auxiliar abaixo de 12 px. A paleta, tipografia e sombra do botão existentes foram mantidas por serem parte da identidade solicitada; o aviso de repetição de “pontos” no pódio foi mantido como unidade dos três dados comparáveis, para não tornar os números ambíguos. Não houve achado de contraste no pódio auditado. Capturas e relatórios estão em `.local`, fora do Git.
