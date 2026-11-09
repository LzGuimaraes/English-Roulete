# Catálogo em português brasileiro

**22 adaptações completas** das 22 histórias inglesas publicadas, com 108 cenas, 108 perguntas, 216 dicas de significado e 44 finais. São catálogos separados: o jogador escolhe Inglês ou Português antes do solo; a sala multiplayer fixa o idioma para o grupo.

| Versão portuguesa | Original | Gênero |
|---|---|---|
| [Uma Mesa para Dois Iniciantes](a-table-for-two-beginners-a2-pt-br/REVIEW.md) | A Table for Two Beginners — A2 | Romance |
| [Um Ensaio Muito Importante](a-very-important-rehearsal-a1-pt-br/REVIEW.md) | A Very Important Rehearsal — A1 | Comédia |
| [Uma Visitante Feita de Luz](a-visitor-made-of-light-a2-pt-br/REVIEW.md) | A Visitor Made of Light — A2 | Ficção científica |
| [O Envelope Azul](the-blue-envelope-a1-pt-br/REVIEW.md) | The Blue Envelope — A1 | Mistério |
| [O Rolo Azul](the-blue-reel-a1-pt-br/REVIEW.md) | The Blue Reel — A1 | Mistério |
| [A Partida Silenciosa da Capitã](the-captains-quiet-match-a2-pt-br/REVIEW.md) | The Captain's Quiet Match — A2 | Esporte |
| [A Moldura Vazia](the-empty-frame-a2-pt-br/REVIEW.md) | The Empty Frame — A2 | Mistério |
| [O Robô do Jardim](the-garden-robot-a1-pt-br/REVIEW.md) | The Garden Robot — A1 | Ficção científica |
| [A Fita Verde](the-green-ribbon-a2-pt-br/REVIEW.md) | The Green Ribbon — A2 | Mistério |
| [O Inventário dos Quartos Vazios](the-inventory-of-empty-rooms-b1-pt-br/REVIEW.md) | The Inventory of Empty Rooms — B1 | Terror psicológico |
| [O Caminho da Lanterna](the-lantern-walk-a1-pt-br/REVIEW.md) | The Lantern Walk — A1 | Aventura |
| [O Último Turno](the-last-shift-a2-pt-br/REVIEW.md) | The Last Shift — A2 | Terror psicológico |
| [O Farol Além da Terra](the-lighthouse-beyond-earth-b1-pt-br/REVIEW.md) | The Lighthouse beyond Earth — B1 | Ficção científica |
| [A Cabine de Escuta](the-listening-booth-a2-pt-br/REVIEW.md) | The Listening Booth — A2 | Terror psicológico |
| [A Correspondência da Montanha](the-mountain-post-b1-pt-br/REVIEW.md) | The Mountain Post — B1 | Aventura |
| [O Estojo Vermelho do Violino](the-red-violin-case-a2-pt-br/REVIEW.md) | The Red Violin Case — A2 | Aventura |
| [A Segunda Xícara](the-second-cup-a1-pt-br/REVIEW.md) | The Second Cup — A1 | Terror psicológico |
| [A Semente que Lembrava da Chuva](the-seed-that-remembered-rain-b1-pt-br/REVIEW.md) | The Seed That Remembered Rain — B1 | Ficção científica |
| [A Sétima Fotografia](the-seventh-photograph-b1-pt-br/REVIEW.md) | The Seventh Photograph — B1 | Mistério |
| [A Alfaiate da Virada do Ano](the-tailor-of-the-turning-year-b1-pt-br/REVIEW.md) | The Tailor of the Turning Year — B1 | Fantasia |
| [A Passagem da Maré](the-tide-window-a2-pt-br/REVIEW.md) | The Tide Window — A2 | Aventura |
| [A Voz do Vagão Nove](the-voice-in-carriage-nine-a2-pt-br/REVIEW.md) | The Voice in Carriage Nine — A2 | Terror psicológico |

## Conteúdo e fidelidade

Cada `story.json` contém a adaptação completa: título, sinopse, narração, falas, pergunta, alternativas, explicação, duas dicas por cena e nomes das escolhas. As chaves, gabaritos e conexões do original são preservados. Vinte histórias têm cinco cenas físicas e percursos de quatro cenas; as duas histórias legadas têm quatro cenas e percursos de três. Cada história oferece dois finais.

Os documentos `REVIEW.md` apresentam o texto inteiro e os percursos. Eles contêm gabaritos editoriais e permanecem fora de `wwwroot`. [source-catalog.json](source-catalog.json) relaciona as adaptações às versões inglesas imutáveis e registra o grafo original. A classificação A1/A2/B1 permanece apenas como referência editorial do original, sem ser apresentada como certificação de português.

As 108 cenas têm `visualDescription` em português. A descrição pertence à versão da cena e usa o JSON de apoio existente; a ilustração compartilhada e seus créditos originais permanecem preservados. Em português, palavras sublinhadas oferecem significado contextual em português, com o mesmo custo e privacidade das hints inglesas. Não há tradução ao vivo nem escolha de respostas ou pontos pelo navegador.

## Áudios e ilustrações locais

Os arquivos de produção estão em `media/portuguese-01/<slug>/`, pasta ignorada pelo Git:

- `audio/<scene-key>.wav`: uma narração brasileira por cena, PCM mono de 16 bits. O JSON ao lado identifica modelo, hashes, parâmetros e intervalos medidos das falas.
- `animations/<scene-key>.gif`: cópia sem alterações do loop da ilustração original. Na VPS, a versão portuguesa reutiliza o mesmo asset visual; a descrição textual pode variar por idioma.
- `media.json`: manifesto e créditos. A raiz contém `visual-reuse.json`, com IDs e hashes dos visuais, e `review.json`, com os roteiros e mídias exatos revisados.

A voz sintética é `pt_BR-cadu-medium`, produzida offline com Piper 1.8.0. O [cartão oficial da voz Cadu](https://huggingface.co/rhasspy/piper-voices/blob/main/pt/pt_BR/cadu/medium/MODEL_CARD) declara português brasileiro e dataset CC0. A configuração e o cartão estão arquivados em `media/portuguese-01/licenses/`. Piper é ferramenta de produção, sem dependência no runtime do jogo.

A produção usa escala de duração 1,04 e ajuste de fonema em encontros consonantais e finais de sílaba, preservando o som breve entre vogais. Esse ajuste foi comparado em frases portuguesas antes da produção final e está registrado em cada comprovante de áudio. Uma única voz narra também os personagens; os nomes nas caixas de diálogo são identificadores editoriais.

Reprodução da produção local, com o modelo obtido da fonte oficial e as dependências opcionais Piper instaladas:

```sh
python scripts/produce_story_pack_audio.py --pack content/portuguese-01 --output media/portuguese-01 --model /caminho/pt_BR-cadu-medium.onnx --pace 1.04 --portuguese-rhotic-compat
```

## Revisão e publicação

As 22 versões estão publicadas na VPS, com 108 novos WAVs e os 108 IDs de ilustrações originais preservados. O catálogo público mantém 22 histórias inglesas e oferece 22 portuguesas separadamente.

A revisão confere cobertura das falas, correspondência entre texto e WAV, duração, volume, ausência de clipping e integridade dos GIFs reutilizados. As 108 narrações foram comparadas aos roteiros por reconhecimento de fala local multilíngue. A transcrição automática pode divergir em nomes próprios, acentos e pontuação; não equivale a revisão auditiva humana ou validação pedagógica independente.

A publicação usa a API administrativa, sem SQL direto. Os comprovantes `vps-import.json` e `vps-media.json` registram IDs, estado, uploads e hashes conferidos por download. Os horários são UTC. A aplicação só coloca versões publicadas e do idioma escolhido na roleta.

O motor real percorre os dois caminhos de cada adaptação em `AuthoredStoryCatalogTests`. Os testes Python verificam a cobertura das 22 originais, as 108 cenas, gabaritos, rotas, evidências, descrições, textos de áudio, diálogos e palavras clicáveis. Os testes de integração e navegador do modo estão descritos em [PORTUGUESE_MODE.md](../../docs/PORTUGUESE_MODE.md).

Antes do commit, passaram build Release sem warnings, 169 testes unitários, 53 testes de integração com PostgreSQL, 23 testes Python de autoria, dez de deploy e verificação de formatação. O teste direcionado percorreu novamente os 42 catálogos de autoria local após os últimos ajustes. No site público, quatro rodadas solo em desktop e celular reproduziram áudio, mostraram a descrição portuguesa, cobraram uma definição e registraram 135 pontos após uma resposta correta.

Para validar o conteúdo sem enviar dados:

```sh
python3 -m unittest discover -s tests/authoring -v
dotnet test tests/EnglishRoulette.UnitTests/EnglishRoulette.UnitTests.csproj --configuration Release --filter FullyQualifiedName~AuthoredStoryCatalogTests
```
