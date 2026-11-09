# Antologia 03 — quatro gêneros novos

Criada e publicada em 08/11/2026 (America/Cuiaba), com autorização para envio direto à VPS. O pacote amplia o catálogo com **comédia, romance, esporte e fantasia**, diferentes dos gêneros das antologias anteriores. Reutiliza os fluxos existentes de solo e multiplayer.

| História | Nível proposto | Gênero | Premissa |
|---|---|---|---|
| [A Very Important Rehearsal](a-very-important-rehearsal-a1/REVIEW.md) | A1 | Comédia | Um rei de teatro descobre um pato de borracha no palco e decide como lidar com a interrupção. |
| [A Table for Two Beginners](a-table-for-two-beginners-a2/REVIEW.md) | A2 | Romance | Dois adultos se aproximam em uma aula de cerâmica e escolhem como marcar o próximo encontro. |
| [The Captain's Quiet Match](the-captains-quiet-match-a2/REVIEW.md) | A2 | Esporte | Uma capitã de futsal aprende a simplificar suas instruções e a ouvir outra jogadora. |
| [The Tailor of the Turning Year](the-tailor-of-the-turning-year-b1/REVIEW.md) | B1 | Fantasia | Uma alfaiate precisa terminar o casaco da primavera sem apagar a memória do guardião do inverno. |

São **20 cenas, 20 perguntas, 40 dicas contextuais, 74 trechos de diálogo e oito finais completos**. Cada história tem três cenas compartilhadas e dois finais alternativos; cada percurso visita quatro cenas. As alternativas corretas estão distribuídas em sete `a`, sete `b` e seis `c`. Cada pergunta tem evidência na cena atual, sem depender de uma rota anterior. Não há novas mecânicas de inventário, combate ou minijogos.

## Arquivos e mídia

Os quatro `story.json` contêm roteiros completos, perguntas, respostas editoriais, diálogos, dicas, bifurcações e instruções de produção. Os `REVIEW.md` permitem conferir as cenas e seus dois percursos. Esses arquivos contêm gabaritos e permanecem fora de `wwwroot`; o servidor continua responsável pelas respostas, prazos e pontuação.

As **20 ilustrações originais** foram criadas com a ferramenta integrada `image_gen`, em estilo de animação 2D cinematográfica dark: personagens adultos desenhados, cenários pintados e luz seletiva. Os prompts estão em `scenes[].productionNotes.imagePrompt`. Duas imagens receberam edições pela mesma ferramenta para corrigir continuidade: as botas do ator no final cômico e uma lousa que aparecia antes de ser introduzida na história esportiva. Os prompts dessas edições estão em `imageRevisionPrompt`.

As mídias locais ficam em `media/anthology-03/<slug>/`:

- `images/<scene-key>-dark.png`: arte final selecionada; variantes substituídas têm sufixo `-v1`.
- `audio/<scene-key>.wav`: narração Piper 1.8.0, voz `en_US-ljspeech-high`; o JSON ao lado registra texto, duração, modelo, hashes e tempos medidos dos diálogos.
- `animations/<scene-key>.gif`: loop discreto de câmera, com 48 quadros e seis segundos. Não há atuação ou animação labial dos personagens.
- `media.json`: manifesto com arquivos e créditos.

Na raiz dessa pasta, `image-production.json` relaciona os caminhos originais de saída da ferramenta às artes selecionadas, incluindo hashes e referências das duas edições; `review.json` identifica os textos e os 40 arquivos WAV/GIF revisados. A licença e a configuração da voz estão em `licenses/`. Toda a pasta de mídia é ignorada pelo Git. As mídias publicadas ficam no volume persistente da VPS e são vinculadas pelo banco.

Uma única voz sintética narra todas as falas; as caixas de diálogo identificam os personagens. Os níveis são propostas editoriais, sem certificação CEFR. A inspeção visual e a comparação por transcrição automática não equivalem a revisão auditiva humana ou validação pedagógica independente.

## Publicação e validação

O pacote foi enviado pela API administrativa existente, com validação oficial antes da publicação. [vps-import.json](vps-import.json) registra as quatro versões `Published` e os hashes dos roteiros; [vps-media.json](vps-media.json) registra os 40 uploads, conferidos por download e comparação dos bytes. Os horários dos comprovantes estão em UTC. Versões publicadas são imutáveis.

Foram conferidos duração, integridade, volume, limites dos arquivos e cobertura dos diálogos nos WAVs. Os loops têm primeiro e último quadros iguais. As 20 narrações passaram por comparação automática com os roteiros e revisão pontual de divergências. Todas as ilustrações selecionadas foram inspecionadas quanto a elenco, objetos e continuidade.

As quatro histórias foram abertas no Chrome no site público, usando o botão real de início e a credencial solo apenas em memória. Cada primeira rodada reproduziu áudio, exibiu diálogo progressivo, traduziu uma palavra por clique e registrou resposta correta com 135 pontos líquidos após uma dica. Capturas de desktop (1366 px) e celular (390 px) foram inspecionadas, incluindo a roleta com oito gêneros. Não houve erro de JavaScript ou transbordamento horizontal; as cenas de desktop couberam em 900 px de altura. Isso não equivale a jogar manualmente as oito rotas completas em produção.

Antes do commit passaram restore com `--locked-mode`, build Release sem warnings, 145 testes unitários, 50 testes de integração com PostgreSQL isolado e `dotnet format --verify-no-changes`. Após os últimos ajustes editoriais, passaram novamente os 20 casos do catálogo no motor real, 17 testes Python de autoria e dez de deploy.

O teste `AuthoredStoryCatalogTests` usa `CatalogRules` e `StoryFlowEngine` para validar os textos e percorrer os dois caminhos de cada história até seus finais. O novo `tests/authoring/test_anthology03.py` verifica que os quatro gêneros e títulos são novos, que as perguntas têm evidência na cena atual e que narração, falas e roteiro de áudio coincidem. Nenhuma classe ou regra de jogo foi alterada.

Para validar o conteúdo sem publicar:

```sh
python3 scripts/import_story.py content/anthology-03/a-very-important-rehearsal-a1/story.json --check
python3 -m unittest discover -s tests/authoring -v
dotnet test tests/EnglishRoulette.UnitTests/EnglishRoulette.UnitTests.csproj --configuration Release --filter FullyQualifiedName~AuthoredStoryCatalogTests
```

Repita o primeiro comando para os demais `story.json`. O teste C# descobre todas as antologias no diretório `content` e percorre suas rotas declaradas.
