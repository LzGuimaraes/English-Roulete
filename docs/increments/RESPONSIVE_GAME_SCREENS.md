# Telas de jogo responsivas e contagem animada

Solicitação: caber no viewport em desktop/tablet, permitir rolagem vertical natural no celular, melhorar a contagem com linguagem de animação e verificar no navegador com Impeccable.

## Critérios de aceite

- Solo e multiplayer: preparação, narração, pergunta, feedback, votação e final sem rolagem da página nos viewports desktop/tablet verificados, incluindo 1024×600, 1280×720, 1366×768 e tablet em retrato. Sem overflow horizontal desde 320px.
- Não cortar conteúdo nem bloquear o scroll global: textos extensos, zoom e janelas excepcionalmente pequenas continuam acessíveis. Revisão da cena, regras e percurso usam expansão explícita.
- Celular mantém fluxo vertical, alvos de toque e controles acessíveis; teclado e movimento reduzido continuam funcionando.
- Contagem visual deriva exclusivamente dos deadlines/snapshots já existentes. A animação não cria espera nem avança fases.
- Preservar imagens, paletas, player compacto, texto progressivo, perguntas, votação, pontos e progresso. Sem mudanças de regras, tabelas ou dependências do produto.

## Incremento

`game.css` adapta a área de jogo à altura disponível e distribui conteúdo por fase. `GameCountdown.razor` apresenta os segundos recebidos com um único pulso elástico por atualização; reduz movimento conforme preferência. `ProgressiveNarration` mantém a narração progressiva e oferece revisão recolhível nas demais fases. `QuestionPanel` e `MultiplayerAdventure` reorganizam o feedback e informações secundárias. As páginas conectam esses componentes aos estados públicos já recebidos.

Impeccable instalado globalmente pela skill-installer: `pbakaus/impeccable`, `.agents/skills/impeccable`, skill 4.8.0. Contexto automático não executou por ausência inicial do bit de execução; contexto foi lido nos documentos e implementação existentes. As orientações `audit`, `adapt`, `animate` e `craft-floor` foram lidas. A revisão preserva marca, paletas e conteúdo existentes conforme a solicitação; avisos estéticos do detector não autorizam redesenho fora do escopo.

Verificação: medições DOM, screenshots e interações reais em QA local com conteúdo publicado. Relatório de auditoria registra resultados e limites de emulação; arquivos de screenshots e saídas da ferramenta ficam em `.local`, fora do Git.

## Auditoria Impeccable e revisão visual

O detector estático inicial não apontou problemas nos arquivos selecionados. A auditoria **renderizada via CDP**, em feedback real B1, encontrou sete avisos: texto funcional de 9–10px, tracking amplo e um aviso consultivo no rótulo da pergunta. No celular, a revisão adicional encontrou dois controles de menu com 10px e uma palavra isolada no fim do feedback. Foram ajustados tamanhos, espaçamento e `text-wrap: pretty`, sem alterar as explicações. O resultado posterior foi **zero apontamentos nos recortes renderizados de desktop e celular**. Isso não é uma certificação de acessibilidade de todo o site.

A revisão `polish` também conferiu screenshots e o percurso real. A primeira rodada de medidas encontrou overflow e sobreposição em salas de oito jogadores: o placar passou a distribuir participantes em duas colunas, com painel expansível em tablets baixos, e o lobby usa uma roleta menor e prontidão compacta com ícones, nome acessível e tooltip. Nomes e pontos permanecem disponíveis. A revisão da cena, o link de convite, regras, resultados individuais e percurso permanecem acessíveis por expansão; não há `overflow: hidden` no documento para mascarar conteúdo.

| Dimensão | Nota de revisão (0–4) | Evidência e limite |
|---|---:|---|
| Acessibilidade | 3 | Rótulos/timer, foco preservado, movimento reduzido, menu e revisão por toque sintetizado; sem teste em leitor de tela físico. |
| Performance | 3 | Contagem reutiliza atualização existente; anima somente transform em região pequena, 360ms por mudança. Sem nova biblioteca; sem medição de FPS em aparelho físico. |
| Temas | 2 | Paletas arcade/terror e arte existentes preservadas; CSS histórico ainda contém cores diretas fora deste incremento. |
| Responsividade | 4 | Medidas de viewport, limites das ações e sobreposição de cabeçalho em fases reais, desde 320px; smartphone mantém fluxo vertical. |
| Integridade | 4 | Conteúdo publicado, áudio real, relógio do servidor e placar existentes; sem gabaritos ou dados de exemplo introduzidos. |
| **Total** | **16/20** | Boa qualidade no escopo verificado; nenhuma questão bloqueante conhecida ao concluir a validação. |

Movimento: cada número recebe um único squash/stretch curto; os últimos três segundos ganham destaque estático. `prefers-reduced-motion` e a preferência editorial `motion-none` removem o movimento, mantendo os números e os rótulos. O componente não possui timer, atraso, callback de avanço nem regra de pontuação própria.

Limites: Chrome com viewports emulados e toque sintetizado, não Safari/iPhone/Android físicos. Janelas excepcionalmente baixas, zoom que produza viewport estreito e mensagens extensas de erro/reconexão mantêm reflow/rolagem acessível, em vez de cortar conteúdo.

## Como repetir

Com o QA local e conteúdo real publicado:

```sh
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/para/playwright node tests/browser/layout.cjs
```

O roteiro percorre uma história solo (B1 quando disponível) e uma sala real com oito identidades; duas sessões possuem UI/áudio e seis clientes auxiliares carregam mídia e enviam comandos válidos para testar a lotação máxima. Nove viewports: 1920×1080, 1366×768, 1280×720, 1024×768, 1024×600, 768×1024, 800×600, 390×844 e 320×568. Verifica overflow horizontal, altura do documento, botões fora do viewport, invasão do cabeçalho pelo placar, movimento reduzido e acesso ao texto. `ENGLISHROULETTE_LAYOUT_MODE=solo|multiplayer` limita o percurso; `ENGLISHROULETTE_LAYOUT_PHASE=preloading` permite repetir a preparação de oito jogadores após um ajuste localizado.

`tests/browser/adventure.cjs` continua validando início, narração progressiva, pause/seek solo, áudio sincronizado multiplayer, perguntas, escolhas, conclusão, retomada, timeouts, catálogo, mídia falhando, JSON inválido e consulta sem SignalR.

## Resultado da validação

- Matriz final consolidada: **117 combinações de fase/viewport, sem overflow nem invasão do cabeçalho**. Os percursos completos foram seguidos de rechecagens dirigidas às fases ajustadas; a última repetiu lobby e preparação de oito jogadores em 18 combinações, aprovadas.
- Regressão completa de `adventure.cjs` aprovada: história solo concluída e multiplayer real com quatro perguntas e uma votação, retry de mídia, recuperação de autoplay, refresh, consulta sem SignalR e resposta durante consulta lenta.
- Toque sintetizado em Chromium mobile (390×844): iniciar áudio, responder, expandir o texto, deslizar verticalmente e abrir/fechar ajuda aprovados. Movimento reduzido mantém a contagem legível sem animação.
- Restore locked, build Release **sem warnings**, **107 unitários + 42 de integração PostgreSQL** e verificação de formatação aprovados. Os testes de deploy (10) e ferramentas editoriais (11) também passaram.
- Foi corrigida uma condição de corrida pré-existente em `AdminCatalogTests.Multiplayer.cs`: o teste supunha que sua chamada de `TickMatchesAsync` necessariamente venceria o worker. Agora verifica diretamente o checkpoint já commitado no PostgreSQL, sem uma nova leitura que avance a fase. As regras de execução e pontuação permanecem intactas.

Arquivos do incremento: `Web/Components/GameCountdown.razor`, `MultiplayerAdventure.razor`, `ProgressiveNarration.razor`, `QuestionPanel.razor`, `StoryWheel.razor`, `Web/Pages/Home.razor`, `Multiplayer.razor`, `Web/wwwroot/css/game.css`, `tests/browser/layout.cjs`, o teste concorrente acima e esta documentação. Capturas, relatórios brutos do Impeccable, importações QA e medições ficam em `.local`, sem secrets ou mídias no Git.
