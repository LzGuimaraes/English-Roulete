# The Red Violin Case — A2

**Gênero:** Aventura · **Estado:** publicado na VPS, com narração e animação 2D dark

Joana precisa levar um violino emprestado ao salão de música e descobre duas formas de atravessar o rio.

**Foco:** Rotas, comparação de planos e compreensão de motivos; passado simples.

**Percursos:**

- ferry-delay → other-crossings → a-person-to-help → bridge-story
- ferry-delay → other-crossings → a-person-to-help → boat-arrival

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## A Delay at the Landing (`ferry-delay`)

Joana reached the ferry landing with a red violin case. Inside was the violin she had borrowed from her teacher. She needed to return it at the music hall across the river. A notice said the ferry was delayed for repairs. The water was calm, but the large boat would not leave soon. She looked for another plan.

**Pergunta:** Why did Joana need to reach the music hall?

- **A.** To return her teacher's violin
- **B.** To repair the delayed ferry
- **C.** To collect a new violin case

**Gabarito editorial:** A
**Explicação:** The story says: "needed to return it at the music hall".

**Pista e continuidade:** A tarefa é devolver um instrumento emprestado, não vencer uma corrida imposta ao jogador.

**Próxima etapa:**

- Continue the story → `other-crossings`

## Other Ways across (`other-crossings`)

A landing worker pointed upstream. A riverside path led to a pedestrian bridge, then back along the other bank to the hall. Beside the landing, a licensed operator was preparing a smaller boat for a scheduled crossing. He had space for Joana and the closed case. She thanked the worker and checked both routes on the town map.

**Pergunta:** Where did the riverside path lead first?

- **A.** Directly to the music hall
- **B.** To a pedestrian bridge
- **C.** To the ferry repair shed

**Gabarito editorial:** B
**Explicação:** The story says: "led to a pedestrian bridge".

**Pista e continuidade:** As alternativas são oferecidas por pessoas presentes, sem criar uma travessia improvisada.

**Próxima etapa:**

- Continue the story → `a-person-to-help`

## A Person to Help (`a-person-to-help`)

An older musician stood at the start of the riverside path. He was walking to the hall and offered to guide Joana. The boat operator called that his crossing was ready, too. Joana could walk with the musician or travel with the operator. Neither route changed her promise to return the violin safely. She held the case by its handle.

**Pergunta:** What did the older musician offer?

- **A.** To repair her violin
- **B.** To carry the ferry's passengers
- **C.** To guide Joana to the hall

**Gabarito editorial:** C
**Explicação:** The story says: "offered to guide Joana".

**Pista e continuidade:** As companhias mudam a experiência, e a responsabilidade pelo instrumento é comum às duas escolhas.

**Próxima etapa:**

- Take the riverside path to the bridge → `bridge-story`
- Share the operator's scheduled boat crossing → `boat-arrival`

## A Story on the Bridge (`bridge-story`)

Joana followed the musician along the path and over the bridge. He told her that her teacher had once played at the hall with him. At the north-bank entrance, they found the teacher waiting. Joana returned the closed red case and introduced her guide. The two musicians recognised each other. Her longer journey had brought more than a violin back to the hall.

**Pergunta:** How did the older musician know Joana's teacher?

- **A.** They had played at the hall together
- **B.** They had repaired the ferry together
- **C.** They had borrowed Joana's violin together

**Gabarito editorial:** A
**Explicação:** The story says: "her teacher had once played at the hall with him".

**Pista e continuidade:** A caminhada conclui a devolução e produz um reencontro preparado pela conversa do percurso.

**Final da rota.**


## A Seat by the Window (`boat-arrival`)

Joana crossed in the operator's boat, keeping the closed case beside her seat. At the north landing, he showed her the short path to the music hall. Her teacher was waiting inside. After Joana returned the violin, he invited her to stay for the rehearsal. She chose a seat by the window and listened, relieved that the borrowed instrument was safely home.

**Pergunta:** What did the teacher invite Joana to do?

- **A.** Help repair the ferry
- **B.** Stay for the rehearsal
- **C.** Return the operator's boat

**Gabarito editorial:** B
**Explicação:** The story says: "invited her to stay for the rehearsal".

**Pista e continuidade:** A travessia direta conclui a mesma promessa e permite acompanhar o ensaio, sem exigir a rota da ponte.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-red-violin-case-a2`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-red-violin-case-a2/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
