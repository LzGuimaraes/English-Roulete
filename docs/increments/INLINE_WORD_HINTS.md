# Dicas nas palavras e isolamento entre partidas

Solicitação: clicar nas palavras da história para consultar uma tradução e conferir que as dicas funcionam em partidas consecutivas no solo e no grupo.

## Critérios de aceite

- As palavras/expressões cadastradas são botões sublinhados no texto da cena, no diálogo progressivo, no texto completo e na revisão durante a pergunta. Mantêm caixa e pontuação originais; palavras parcialmente reveladas não recebem cliques nem foco.
- O custo informado vem do snapshot do servidor. A tradução só aparece após o comando aceito; há estado de registro, tentativa novamente e explicação quando o prazo para novas dicas terminou.
- Uma tradução já comprada pode ser aberta sem novo comando/cobrança. A lista de palavras existente permanece disponível como alternativa para toque e consulta.
- Botões têm nomes acessíveis, foco visível e uso por Enter. Ao abrir a tradução, o foco vai para o painel; Escape/Fechar retorna à palavra. A animação não recria os botões a cada quadro.
- Refresh/reconexão conserva as compras da rodada. Outra cena ou aventura começa sem compras. Uma nova aventura solo zera os pontos; a revanche do grupo preserva somente os totais existentes, com novas dicas individuais.
- Comandos que apontem para uma cena da partida anterior são rejeitados. Uma compra do anfitrião não revela a tradução nem desconta pontos dos convidados; na revanche, o convidado pode comprar a mesma chave editorial sem herdar a compra anterior.
- As histórias publicadas do catálogo atual possuem vocabulário em todas as cenas. Textos, perguntas e arquivos de mídia permanecem intactos.

## Alterações e responsabilidades

`HintedText` identifica expressões editoriais completas no texto e renderiza botões Blazor. Somente recebe contratos públicos, sem traduções não compradas ou gabaritos. `ProgressiveNarration` apresenta a tradução e encaminha a ação ao mesmo comando de dica já usado por `WordHints`. A seleção do painel é vinculada à instância da cena; a chave do componente solo também usa essa instância, para não reutilizar estado visual quando outra aventura repete a mesma cena.

`audio-player.js` atualiza somente os trechos de texto dos elementos já renderizados. Mantém os eventos Blazor, esconde as falas inativas e libera o botão quando a palavra aparece inteira. A revelação segue a posição do áudio e respeita movimento reduzido; não passa a ser alinhamento fonético.

O servidor continua calculando prazo, consumo e desconto pelas regras existentes. Nenhum novo esquema de banco, pacote ou política de pontos foi introduzido. Os valores provisórios e decisões pendentes continuam em [VOCABULARY_DIALOGUE_SCORING.md](VOCABULARY_DIALOGUE_SCORING.md).

## Cobertura editorial e publicação

Nove histórias da Antologia 01 ainda não tinham vocabulário. Foram acrescentadas 90 traduções contextuais nas 45 cenas correspondentes. Mais 16 traduções completam as oito cenas de `The Green Ribbon` e `The Last Shift`. Os JSONs da antologia guardam as novas palavras; [initial-stories.json](../../content/vocabulary/initial-stories.json) vincula as anotações das duas histórias iniciais ao hash exato de cada narração.

As onze atualizações foram publicadas como novas versões pela API administrativa, com validação oficial e reaproveitamento dos mesmos arquivos. Foi conferida a igualdade de narração, perguntas, opções, gabaritos, explicações, transições, diálogos e referências de áudio/imagem antes/depois. A conferência administrativa de todas as versões do catálogo público encontrou **18 histórias, 88 cenas e 176 traduções**, sem cena sem vocabulário. Os IDs anteriores/novos estão em [vps-publication.json](../../content/vocabulary/vps-publication.json). Comprovantes dos lotes anteriores são históricos; este arquivo registra as versões atuais das onze atualizações.

Partidas já iniciadas continuam usando sua versão imutável. As anotações novas aparecem em novas aventuras e sorteios; não são injetadas em checkpoints antigos.

## Como validar

`AdminCatalogTests.Assistance` percorre duas aventuras da mesma história no mesmo cliente e duas partidas da mesma sala no PostgreSQL real. Confere comandos antigos, tradução privada, cobrança única, desconto líquido e inversão do comprador na revanche. Relógio de testes é `TimeProvider`, sem prazos decididos pelo cliente.

`tests/browser/hints.cjs` usa mídias e fases reais de QA: clica numa palavra durante a revelação, consulta o texto completo, usa teclado, fecha/reabre, recarrega, responde, termina uma história e inicia outra; no grupo, conclui a história, exibe o pódio e compra dicas na revanche por outra identidade. Os gabaritos usados na automação são lidos dos arquivos editoriais locais, nunca do contrato público antes da revelação.

```sh
python3 -m unittest discover -s tests/authoring -v
dotnet test EnglishRoulette.sln --configuration Release
ENGLISHROULETTE_PLAYWRIGHT_PATH=/caminho/playwright node tests/browser/hints.cjs
```

O teste de navegador recusa origem externa e deve usar o catálogo narrado em QA isolado, com `A Visitor Made of Light` e histórias da antologia. `ENGLISHROULETTE_FEATURE_MODE=solo|multiplayer` permite repetir um dos percursos. `ENGLISHROULETTE_FEATURE_MODE=errors` injeta uma falha HTTP 503 na primeira compra e comprova que só a tentativa seguinte, enviada à API real, cobra e libera a tradução. Capturas, logs, credenciais e resultados brutos ficam em `.local/`, fora do Git.

## Resultado da validação

Restore locked, build Release sem warnings, 141 testes unitários, 46 de integração, 15 testes Python de autoria, dez de deploy e format verify passaram. O percurso solo completo e a primeira rodada de sua nova aventura passaram, assim como a partida de grupo até o pódio e a primeira rodada da revanche com troca de comprador. A recuperação da falha HTTP também passou, sem erros de JavaScript.

A conferência visual usou desktop e celular, incluindo a tradução no texto progressivo e no diálogo completo. O teste de layout passou em 54 combinações de fase/tamanho de tela, e a regressão de diálogos em mais 18 combinações, preservando pausa, seek e movimento reduzido. O detector mecânico do Impeccable não apontou ocorrências nos componentes e CSS verificados; as capturas também foram inspecionadas.
