# Antologia 02 — quatro novas histórias publicadas

Criada e publicada na VPS em 08/11/2026, com autorização direta do responsável. O lote acrescenta quatro histórias independentes ao catálogo real da roleta, usando os fluxos existentes de solo e multiplayer.

| História | Nível proposto | Gênero | Premissa |
|---|---|---|---|
| [The Blue Reel](the-blue-reel-a1/REVIEW.md) | A1 | Mistério | Iris encontra uma bobina azul no cinema e escolhe entre assistir ao filme e procurar quem o deixou. |
| [The Tide Window](the-tide-window-a2/REVIEW.md) | A2 | Aventura | Bea precisa entregar sementes em uma ilha e decide entre a passagem da maré baixa e o barco de suprimentos. |
| [The Voice in Carriage Nine](the-voice-in-carriage-nine-a2/REVIEW.md) | A2 | Terror psicológico | Uma ferroviária encontra o próprio casaco em um vagão vazio, enquanto uma voz anuncia seus próximos movimentos. |
| [The Seed That Remembered Rain](the-seed-that-remembered-rain-b1/REVIEW.md) | B1 | Ficção científica | Uma muda reage a uma gravação de chuva; Lena decide como preservar e investigar essa observação. |

São **20 cenas, 20 perguntas, 40 dicas contextuais, 67 trechos de diálogo e oito finais completos**. Cada história tem três cenas compartilhadas e dois finais; cada percurso visita quatro cenas. As posições corretas estão distribuídas em sete `a`, sete `b` e seis `c`. As perguntas dependem somente da cena atual. Nenhuma história exige inventário, combate ou outro recurso novo do motor.

## Texto, mídia e origem

Cada `story.json` contém a história completa, perguntas, gabaritos editoriais, bifurcações, dicas, diálogos e instruções de produção. Os documentos `REVIEW.md` permitem ler as cenas e conferir as perguntas. Esses arquivos contêm respostas e não devem ser copiados para `wwwroot`; o servidor continua responsável pela revelação, prazo e pontuação.

As **20 ilustrações originais** foram geradas com a ferramenta integrada `image_gen`, em modo de criação de novas imagens, usando os prompts finais registrados em `scenes[].productionNotes.imagePrompt` de cada `story.json`. O estilo é animação 2D cinematográfica dark, com adultos desenhados, cenários pintados e sombras marcadas. Foram revisados o elenco presente em cada cena, idade aparente, objetos e continuidade. Os textos alternativos em português também estão nos JSONs.

Os arquivos locais estão em `media/anthology-02/<slug>/`:

- `images/<scene-key>-dark.png`: ilustração selecionada; variantes anteriores podem permanecer como `-dark-v1.png`.
- `audio/<scene-key>.wav`: narração sintética Piper 1.8.0, voz `en_US-ljspeech-high`; o JSON ao lado registra duração, modelo, texto e tempos medidos dos diálogos.
- `animations/<scene-key>.gif`: loop de câmera discreto, com 48 quadros e seis segundos. Não é animação de fala ou atuação dos personagens.
- `media.json`: manifesto de arquivos e origem.

Na raiz local da mídia, `image-production.json` relaciona as saídas originais da geração às artes selecionadas e `review.json` identifica os hashes exatos revisados. Essa pasta é ignorada pelo Git. As mídias publicadas ficam no volume persistente da VPS, vinculadas às histórias pelo banco.

Uma única voz sintética narra também as falas de personagens; a identificação dos interlocutores aparece nas caixas de diálogo. Os níveis são propostas editoriais, sem certificação CEFR. A comparação por transcrição automática e a inspeção visual não constituem revisão auditiva humana ou validação pedagógica independente.

## Publicação e validação

O envio usou a API administrativa existente, com validação oficial antes da publicação, sem alterar o banco diretamente. Os IDs, estado `Published` e hashes dos textos estão em [vps-import.json](vps-import.json); os 40 uploads de WAV/GIF e hashes conferidos por download estão em [vps-media.json](vps-media.json). Versões publicadas são imutáveis; uma correção posterior exige nova versão pelo fluxo administrativo.

Os WAVs foram conferidos quanto a integridade, duração, volume e cobertura dos diálogos. Os loops têm primeiro e último quadros iguais e respeitam os limites de mídia. As transcrições automáticas das 20 narrações foram comparadas aos roteiros; diferenças de nomes próprios, grafia e homófonos foram examinadas, com revisão pontual dos trechos divergentes.

As quatro histórias foram abertas no site público pelo navegador: a primeira rodada reproduziu áudio real, exibiu diálogo progressivo, cobrou uma dica contextual e registrou uma resposta correta com 135 pontos líquidos. Foram conferidas capturas em desktop (1440 px) e celular (390 px), sem erros de JavaScript ou transbordamento horizontal. As duas rotas de cada história foram percorridas pelo motor real nos testes de conteúdo; isso não equivale a quatro campanhas completas jogadas manualmente em produção.

Antes do commit, passaram restore com `--locked-mode`, build Release sem warnings, 141 testes unitários, 46 testes de integração com PostgreSQL isolado e `dotnet format --verify-no-changes`. Também passaram 14 testes Python de autoria e dez de deploy.

## Papel das alterações de código

`AuthoredStoryCatalogTests` agora descobre `story.json` em toda a pasta `content`, para validar este lote e os próximos com `CatalogRules` e `StoryFlowEngine`. O motor percorre as rotas declaradas e verifica que chegam aos finais esperados. Nenhuma regra de jogo mudou.

`scripts/upload_anthology_media.py` aceita a identificação do pacote nos créditos e usa o texto alternativo editorial quando presente. Mantém a comparação de bytes, a validação e a proteção contra substituição de mídia publicada.

Para conferir o pacote sem enviar dados:

```sh
python3 scripts/import_story.py content/anthology-02/the-blue-reel-a1/story.json --check
python3 -m unittest discover -s tests/authoring -v
dotnet test tests/EnglishRoulette.UnitTests/EnglishRoulette.UnitTests.csproj --configuration Release --filter FullyQualifiedName~AuthoredStoryCatalogTests
```

Repita o primeiro comando para cada `story.json`. O teste C# cobre os dois lotes. Os testes Python de autoria existentes incluem o formato de importação e os critérios do primeiro lote; a cobertura dos novos grafos fica no teste C# e na conferência editorial descrita acima.
