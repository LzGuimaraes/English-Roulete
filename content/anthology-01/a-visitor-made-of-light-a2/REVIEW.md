# A Visitor Made of Light — A2

**Gênero:** Ficção científica · **Estado:** publicado na VPS, com narração e animação 2D dark

Na desmontagem de uma estação espacial, Sana encontra a mensagem holográfica deixada por uma antiga construtora.

**Foco:** Identificação de quem fala, finalidade de uma mensagem e contraste entre registros; passado simples.

**Percursos:**

- old-workshop → window-memory → what-to-save → archive-light
- old-workshop → window-memory → what-to-save → workshop-voice

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## Someone above the Desk (`old-workshop`)

Sana was checking an old workshop before the station was taken apart. Above a desk projector, a blue figure appeared: an older woman in a work shirt. "I helped build this place," the recording said. Sana stopped by the window bench. The visitor was only light, but its voice sounded pleased to have company again. She decided to hear the whole message.

**Pergunta:** Who did the projected woman say she was?

- **A.** Someone who arrived to take Sana home
- **B.** Someone who helped build the station
- **C.** Someone who owned the transport ship

**Gabarito editorial:** B
**Explicação:** The story says: "I helped build this place".

**Pista e continuidade:** A figura é uma gravação, não uma personagem interativa oferecida pelo motor do jogo.

**Próxima etapa:**

- Continue the story → `window-memory`

## The Bench by the Window (`window-memory`)

The builder's recording described the bench beneath the workshop window. After long shifts, she and her team had sat there to watch the stars. She had saved the message so future workers would know that the station was once a home. Sana opened the desk drawer and found a white memory card. It could hold a copy before the equipment was removed.

**Pergunta:** Why had the builder saved the message?

- **A.** To ask future workers to build another window
- **B.** To warn that the workshop tools were missing
- **C.** To show that the station had once been a home

**Gabarito editorial:** C
**Explicação:** The story says: "the station was once a home".

**Pista e continuidade:** O banco ancora a memória em um local concreto e o cartão permite as duas rotas.

**Próxima etapa:**

- Continue the story → `what-to-save`

## What Is Worth Keeping? (`what-to-save`)

The white card had room for the complete projection or a simpler voice recording. The station archive could preserve the full scene. Sana could also save the voice, then send the projector to the new workshop for reuse. The builder's words would survive either way. Looking at the worn bench, Sana considered what she wanted the next workers to receive.

**Pergunta:** What would survive in either choice?

- **A.** The builder's words
- **B.** The old bench itself
- **C.** The entire workshop

**Gabarito editorial:** A
**Explicação:** The story says: "The builder's words would survive either way".

**Pista e continuidade:** A decisão contrapõe preservação integral e reutilização, sem perder a mensagem principal.

**Próxima etapa:**

- Save the complete projection for the station archive → `archive-light`
- Save the voice and reuse the projector → `workshop-voice`

## A Place in the Archive (`archive-light`)

Sana copied the complete projection onto the white card and labelled it for the archive. Before leaving, she watched the builder beside the window bench one last time. Later, in the station museum, visitors could see that modest workshop and hear why it mattered. Sana had not saved the building, but she had kept a way for other people to enter its memory.

**Pergunta:** Where would visitors be able to see the saved projection?

- **A.** In Sana's transport cabin
- **B.** In the station museum
- **C.** In the new workshop's tool cupboard

**Gabarito editorial:** B
**Explicação:** The story says: "in the station museum".

**Pista e continuidade:** A cópia integral tem um destino claro e encerra a tarefa de preservação sem salvar fisicamente a estação.

**Final da rota.**


## A Voice in the New Workshop (`workshop-voice`)

Sana saved the voice onto the white card and prepared the projector for reuse. In the new workshop, she played the builder's message for her team. They listened beside an ordinary workbench, then left a place where everyone could sit after a shift. The old window was gone, but the reason for its bench had travelled with them. Sana felt ready to begin again.

**Pergunta:** What did the new team create after listening?

- **A.** A copy of the old station window
- **B.** A private room for Sana alone
- **C.** A place to sit together after a shift

**Gabarito editorial:** C
**Explicação:** The story says: "a place where everyone could sit after a shift".

**Pista e continuidade:** A voz transforma uma prática de convivência no novo lugar; não aparece a projeção integral da outra rota.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/a-visitor-made-of-light-a2`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/a-visitor-made-of-light-a2/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
