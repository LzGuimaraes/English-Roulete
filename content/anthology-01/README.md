# Antologia 01 — 12 histórias originais

Pacote editorial criado para ampliar a variedade da roleta, com quatro gêneros em A1, A2 e B1. São histórias independentes, não versões da mesma premissa em níveis diferentes. O conteúdo é compartilhável pelo motor solo e pelo futuro gameplay multiplayer, sem duplicar narrativas por modo.

**Entrega:** 12 histórias completas, 60 cenas, 60 perguntas com três alternativas e explicação, 12 bifurcações e 24 finais. Cada percurso visita quatro cenas: três compartilhadas e um dos dois finais. Há um roteiro de narração e prompts específicos de imagem/animação para cada cena.

**Publicado na VPS:** as 12 histórias estão disponíveis no catálogo público. **Produção concluída:** 60 narrações WAV, 60 ilustrações 2D dark e 60 loops GIF foram produzidos e enviados às 12 histórias na VPS. Cada upload foi relido e comparado por SHA-256. O estado de publicação e os IDs estão em [vps-import.json](vps-import.json); os comprovantes de mídia estão em [vps-media.json](vps-media.json). O responsável autorizou o envio e a publicação direta deste lote.

**Atualização de vocabulário (08/11/2026):** todas as 60 cenas agora têm duas dicas contextuais. Nove histórias receberam novas versões com 90 traduções, preservando textos e mídias. Os comprovantes acima registram o lote original; os IDs dessas atualizações estão em [vps-publication.json](../vocabulary/vps-publication.json). O clique nas palavras e o isolamento entre partidas estão descritos em [INLINE_WORD_HINTS.md](../../docs/increments/INLINE_WORD_HINTS.md).

| Nível proposto | Gênero | História completa para leitura | Premissa |
|---|---|---|---|
| A1 | Aventura | [The Lantern Walk](the-lantern-walk-a1/REVIEW.md) | Omar leva uma lanterna ao encontro do bairro e escolhe entre dois caminhos do parque. |
| A1 | Ficção científica | [The Garden Robot](the-garden-robot-a1/REVIEW.md) | Em uma estufa em Marte, Imani ajuda um pequeno robô a entregar as últimas sementes do dia. |
| A1 | Mistério | [The Blue Envelope](the-blue-envelope-a1/REVIEW.md) | Uma bibliotecária encontra um envelope sem nome e procura quem deixou uma fotografia dentro dele. |
| A1 | Terror psicológico | [The Second Cup](the-second-cup-a1/REVIEW.md) | Ao fechar um café vazio, Nora encontra uma segunda xícara quente e precisa decidir por qual saída partir. |
| A2 | Aventura | [The Red Violin Case](the-red-violin-case-a2/REVIEW.md) | Joana precisa levar um violino emprestado ao salão de música e descobre duas formas de atravessar o rio. |
| A2 | Ficção científica | [A Visitor Made of Light](a-visitor-made-of-light-a2/REVIEW.md) | Na desmontagem de uma estação espacial, Sana encontra a mensagem holográfica deixada por uma antiga construtora. |
| A2 | Mistério | [The Empty Frame](the-empty-frame-a2/REVIEW.md) | Uma curadora encontra uma moldura vazia antes de uma exposição e segue pistas de uma retirada inesperada. |
| A2 | Terror psicológico | [The Listening Booth](the-listening-booth-a2/REVIEW.md) | Celia vai copiar uma gravação do pai e encontra uma voz que parece responder antes de ela falar. |
| B1 | Aventura | [The Mountain Post](the-mountain-post-b1/REVIEW.md) | Uma voluntária recolhe a última correspondência de uma estação de montanha e decide como devolvê-la a quem a escreveu. |
| B1 | Ficção científica | [The Lighthouse beyond Earth](the-lighthouse-beyond-earth-b1/REVIEW.md) | Em uma estação lunar, Anik encontra um sinal que parece escutar e escolhe como responder antes de automatizar o posto. |
| B1 | Mistério | [The Seventh Photograph](the-seventh-photograph-b1/REVIEW.md) | Ao preparar uma exposição, Nadia descobre uma fotografia que contradiz o crédito dado à construção de uma ponte. |
| B1 | Terror psicológico | [The Inventory of Empty Rooms](the-inventory-of-empty-rooms-b1/REVIEW.md) | Uma vistoriadora precisa certificar uma pensão vazia, mas o livro de hóspedes começa a registrá-la como moradora. |

## Estilo visual

As 60 cenas usam direção de **animação 2D cinematográfica dark**, com personagens adultos desenhados, contornos limpos, sombras em cel shading e cenários pintados. Pessoas fotorrealistas, atores filmados e texturas fotográficas de pele são excluídos. Todos os gêneros usam paleta contida, sombras profundas e pistas legíveis; o terror acentua a tensão. As artes foram produzidas com a ferramenta `image_gen` e inspecionadas por cena. Foram corrigidos elencos extras, pistas e vistas incompatíveis com os cenários espaciais.

Os arquivos estão preservados em `media/anthology-01/<história>/`: `images/` guarda as artes selecionadas, `audio/` guarda as narrações e `animations/` guarda os loops. Cada pasta tem um `media.json` real com créditos e evidências de origem. A pasta `media/` é ignorada pelo Git; o jogo serve as cópias enviadas ao volume de mídia da VPS, vinculadas ao banco. Os prompts finais permanecem em `productionNotes` dos JSONs editoriais.

## O que existe em cada pasta

- `story.json`: texto em inglês, metadados, perguntas, gabaritos editoriais, transições, temas e instruções de produção de mídia. Compatível com o importador existente.
- `REVIEW.md`: leitura de todas as cenas, duas rotas, perguntas, explicações e pontos de continuidade para revisão.
- `media.example.json`: manifesto de destinos para cinco WAVs e cinco GIFs/APNGs, com créditos e direitos da mídia em branco para preenchimento após a produção. A evidência do texto registra a autorização real recebida nesta sessão.

**Não copie `story.json` nem `REVIEW.md` para `wwwroot`.** Eles contêm gabaritos editoriais; a API do jogo continua responsável pelos contratos públicos e pela revelação das respostas. `productionNotes` e `authoringNotes` não são persistidos pelo importador nem enviados como snapshots.

## Critérios de aceite e validação

- Três histórias por gênero e quatro por nível, com títulos, protagonistas e situações distintos.
- Inglês e questões seguem as metas editoriais de tamanho do prompt: A1, 30–50 palavras; A2, 45–75; B1, 65–95 por cena. Essas faixas não certificam um nível CEFR; compreensão e naturalidade precisam de revisão humana.
- Todas as cenas são alcançáveis; não há ciclos nem destinos ausentes. Os dois percursos completos estão registrados em cada pacote.
- Toda cena, inclusive finais, contém uma pergunta com alternativas `a/b/c`, uma chave correta, uma explicação e um trecho literal de evidência editorial. As respostas corretas são distribuídas entre as três posições: 20 em cada uma.
- `audioText` é cópia exata de `narration`. Os prompts visuais delimitam personagens, objetos, luz e continuidade; os loops usam apenas um movimento de câmera periódico discreto, sem depender de animação para compreender ou responder.
- Os objetos carregados e as tecnologias fazem parte da ficção. Não exigem inventário, combate, gravação de voz do jogador, minijogos ou outro recurso ainda inexistente no motor.
- Cada mídia tem créditos e origem registrados. As narrações usam Piper 1.8.0, voz `en_US-ljspeech-high`, com modelo identificado pelo hash; cartão do modelo e metadados de licença foram preservados no pacote local. Transcrições automáticas foram comparadas aos 60 roteiros, incluindo inspeção das diferenças de nomes, grafia e reconhecimento. Isso não declara uma revisão auditiva humana nem uma certificação pedagógica.

Da raiz do repositório:

```sh
python3 -m unittest discover -s tests/authoring -v
python3 scripts/import_story.py content/anthology-01/the-blue-envelope-a1/story.json --check
dotnet test tests/EnglishRoulette.UnitTests/EnglishRoulette.UnitTests.csproj --configuration Release --filter FullyQualifiedName~AuthoredStoryCatalogTests
```

`test_anthology.py` verifica formato de importação, alcance, ausência de ciclos, dois finais por história, evidências literais, correspondência de áudio, variedade, tamanho editorial e contraste. Essas verificações não substituem revisão de qualidade literária ou pedagógica. `AuthoredStoryCatalogTests` passa os doze JSONs por `CatalogRules` e `StoryFlowEngine`, e percorre as duas rotas declaradas de cada história com o motor real, sem conexão ou publicação.

## Retomar um rascunho já importado

```sh
python3 scripts/import_story.py content/anthology-01/the-blue-envelope-a1/story.json \
  --version-id ID_DA_VERSAO_EM_VPS_IMPORT_JSON \
  --url https://english.lzguimaraes.com.br \
  --email SEU_EMAIL_ADMIN
```

Use esse comando somente se o comprovante ainda indicar `Draft`. Substitua o ID pelo `versionId` correspondente e informe a senha no prompt protegido. O importador não publica a história. Uma versão `Published` é imutável: duplique-a pelo painel antes de editar. Cada pasta deste lote já corresponde a uma história independente; não execute uma nova criação para reenviar o mesmo conteúdo.

O manifesto `media.example.json` continua sendo um modelo com créditos vazios e caminhos sem arquivos na pasta editorial. O pacote real fica na pasta ignorada de mídia. O exemplo deve continuar sendo recusado como pacote pronto. O fluxo reproduzível e a retomada de uploads estão em [MEDIA_PRODUCTION.md](../../docs/MEDIA_PRODUCTION.md).

A publicação requer narração válida e confirmação editorial de direitos no painel. As especificações e os limites de mídia estão em [STORY_AUTHORING.md](../../docs/STORY_AUTHORING.md). A geração permanece fora do runtime do jogo.

## Decisões editoriais pendentes

- Níveis, perguntas e tom continuam sujeitos a revisão editorial. O responsável já autorizou o envio deste lote sem nova aprovação; essa autorização não equivale a afirmar que houve uma revisão humana completa.
- A seleção de voz e a produção deste lote foram resolvidas: Piper/LJ Speech para narração, `image_gen` para artes e FFmpeg para os loops de câmera discretos. Geração e transcrição continuam fora do runtime do jogo.

## Verificações executadas

Os 12 JSONs passaram em `import_story.py --check` e os onze testes Python de autoria passaram. Passaram também os 94 testes unitários e os 40 testes de integração C# com PostgreSQL local isolado. O build Release terminou sem warnings e a formatação foi verificada. Os parsers reais da aplicação aceitaram os 60 WAVs e os 60 GIFs. Os GIFs têm 48 quadros, seis segundos e o mesmo enquadramento inicial/final. Os 120 uploads foram relidos e comparados aos arquivos locais; a API oficial continua validando cada versão antes da publicação.
