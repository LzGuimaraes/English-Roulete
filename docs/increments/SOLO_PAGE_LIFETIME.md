# Aventura solo limitada à página

Decisão do responsável: sair de uma história individual deve exigir um novo jogo do zero, em vez de voltar à cena anterior. Essa decisão substitui a proposta de retomada de Adventure em RF-SOLO-006; não modifica a reconexão multiplayer.

## Critérios de aceite

- Uma visita a `/` abre o menu. Ao iniciar, o servidor sorteia uma nova aventura, na primeira cena e sem pontos/dicas consumidas.
- Sair pelo menu/logotipo ou navegar para outra tela descarta a credencial e solicita a exclusão do checkpoint individual. Uma tentativa de usar a credencial revogada recebe 404.
- Recarregar, fechar/reabrir ou retornar pelo histórico do navegador não restaura uma aventura solo. Nada persiste a credencial em cookies, localStorage, sessionStorage ou URL.
- Abas diferentes são independentes: abrir outra aba não encerra o jogo ainda aberto na primeira.
- Cookies antigos de `englishroulette.demo` não autenticam GET/comandos. Na entrada, são expirados e seu checkpoint é removido pelo endpoint de encerramento.
- Dentro da página aberta, polling, retry e reconexão usam os mesmos prazos, pontos e dicas do servidor. Administração, identidade do convidado, sala multiplayer e sua reconexão permanecem existentes.

## Causa e solução

O servidor emitia `englishroulette.demo` com validade de 30 dias. `Home.OnInitializedAsync` consultava automaticamente esse cookie e restaurava a história. O botão Menu apenas escondia o estado local, permitindo que uma visita posterior o recuperasse.

`SoloSessionHeaders.Credential` identifica um header opaco, emitido ao criar a aventura e mantido somente no campo privado de `AdventureClient`. Cada GET/comando individual inclui essa chave; o servidor armazena apenas seu hash. Ela não é aplicada como header global do HttpClient e não acompanha catálogo, administração, mídia ou multiplayer. O contrato de cena continua sem gabaritos ou novas informações privadas.

`AdventureClient.EndAsync` remove a chave da memória antes do I/O e invalida criações atrasadas. `DemoSessions.EndAsync` trata `DELETE /api/demo/session`, com a mesma proteção de header contra requisições indevidas usada pelos comandos solo. `PostgresSoloSessionStore.DeleteAsync` remove somente o checkpoint identificado. DELETE repetido é inofensivo. Não há migração de esquema nem limpeza ampla de históricos.

`Home` encerra a aventura na entrada, na saída ao menu e no descarte da rota. O pedido de limpeza tem prazo curto; uma falha de rede não recoloca a chave na memória. Fechar uma janela não garante entrega de uma requisição ao servidor: a ausência de retomada depende da chave não persistida, e não de um evento de unload. Checkpoints que não puderam receber limpeza ficam inacessíveis ao navegador que perdeu sua chave; este incremento não cria uma política de retenção geral.

`solo-lifetime.js` trata o cache de páginas inteiras do navegador (BFCache): uma página solo restaurada pelo histórico é recarregada para não reviver a memória WASM antiga. O seletor é exclusivo de Home, sem aplicar essa regra à página de grupo. Os textos da abertura, manual, rodapé e erro de recarregamento explicam a nova regra.

## Validação

- Testes de API com PostgreSQL: credencial exigida, cookie antigo recusado para jogo, exclusão/retry, isolamento de páginas, limpeza do cookie antigo e novo início com pontuação/dicas zeradas.
- Testes do cliente: nova instância sem credencial não faz retomada; limpeza sem rede descarta a credencial; resposta de criação atrasada não reabre uma aventura encerrada.
- `tests/browser/solo-lifetime.cjs`: desktop/celular, recarregar após pontuar e avançar, menu, logotipo, saída SPA e retorno, histórico de documento, fechar/reabrir, duas abas e migração de cookie antigo. Usa histórias, áudio e perguntas reais de QA.
- As regressões de solo usam `solo-page.cjs` para observar a chave da página somente no runner de testes. A escolha de uma história de QA altera o POST real de criação; não injeta snapshot, gabarito ou estado no frontend. Nenhum helper restaura a chave depois de navegar/recarregar.

```sh
dotnet restore EnglishRoulette.sln --locked-mode
dotnet build EnglishRoulette.sln --configuration Release --no-restore
dotnet test EnglishRoulette.sln --configuration Release --no-build
dotnet format EnglishRoulette.sln --verify-no-changes --no-restore
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/playwright node tests/browser/solo-lifetime.cjs
```

Resultados brutos, capturas e credenciais de QA permanecem fora do Git. As descrições de testes de retomada solo nos incrementos anteriores registram o comportamento histórico, substituído por esta decisão explícita.

Validação concluída: restore locked, build Release sem warnings, 141 testes unitários e 50 de integração passaram, assim como os 15 testes Python de autoria e dez de deploy. O roteiro de encerramento passou em 1366 px e 390 px, incluindo avanço à segunda cena após pontuar no desktop. Passou também uma aventura solo completa com dicas e a primeira rodada de outra aventura; o grupo concluiu a história, recarregou com sucesso e jogou a primeira rodada da revanche mantendo seus pontos. Nenhuma dessas verificações registrou erro de JavaScript.
