# The Listening Booth — A2

**Gênero:** Terror psicológico · **Estado:** publicado na VPS, com narração e animação 2D dark

Celia vai copiar uma gravação do pai e encontra uma voz que parece responder antes de ela falar.

**Foco:** Identificação de vozes, ordem temporal e intenção explícita; suspense compreensível sem imagem.

**Percursos:**

- archive-visit → unfamiliar-voice → copying-choice → full-recording
- archive-visit → unfamiliar-voice → copying-choice → fathers-message

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## A Recording to Take Home (`archive-visit`)

Celia visited the sound archive to copy her father's old recording. She brought a black memory drive. Inside the listening booth, an amber lamp lit the desk. Her father's tape was already on the player. She put on the headphones and heard his familiar words: "Take your time." For a moment, the small room felt welcoming.

**Pergunta:** Why did Celia bring a memory drive?

- **A.** To repair the desk lamp
- **B.** To leave a message for the receptionist
- **C.** To copy her father's recording

**Gabarito editorial:** C
**Explicação:** The story says: "to copy her father's old recording".

**Pista e continuidade:** O objetivo afetivo e os aparelhos necessários são apresentados antes da anomalia.

**Próxima etapa:**

- Continue the story → `unfamiliar-voice`

## A Voice before Her Own (`unfamiliar-voice`)

After her father's words, another voice said, "Leave the door open." It sounded like Celia, although she had not spoken. She removed the headphones. The same words came quietly from them again. She opened the booth door and looked into the empty corridor. Returning to the desk, she decided that she would not close the door again.

**Pergunta:** What did the unfamiliar voice tell Celia to do?

- **A.** Leave the door open
- **B.** Turn off the amber lamp
- **C.** Return the drive to reception

**Gabarito editorial:** A
**Explicação:** The story says: "Leave the door open".

**Pista e continuidade:** A voz antecede a fala da protagonista; abrir a porta é uma reação concreta e prudente.

**Próxima etapa:**

- Continue the story → `copying-choice`

## What to Carry Away (`copying-choice`)

The copying terminal showed two parts: her father's short message and the strange voice after it. Celia could keep the whole recording as evidence, or copy only the words she had come to hear. Both copies could fit on her black drive. With the door still open behind her, she reached towards the terminal and chose what to take home.

**Pergunta:** What could Celia copy onto her drive?

- **A.** Only the strange voice, without her father's message
- **B.** Either the full recording or just her father's message
- **C.** Nothing, because her drive was already full

**Gabarito editorial:** B
**Explicação:** The story says: "Both copies could fit on her black drive".

**Pista e continuidade:** A escolha altera o conteúdo levado, não uma regra de pontuação ou um minijogo.

**Próxima etapa:**

- Keep the full recording and leave → `full-recording`
- Keep only the father's message and leave → `fathers-message`

## A Copy with a Witness (`full-recording`)

Celia saved both parts and left the booth. At reception, she asked the clerk to listen with her. Her father's message played clearly. After it, they heard a click and a faint breath, but no words. The clerk noted the problem. Celia kept the full copy and left the archive with him. She had the recording, and someone else knew what she had heard.

**Pergunta:** What did Celia and the clerk hear after her father's message?

- **A.** The clear words "Leave the door open"
- **B.** A greeting from the receptionist
- **C.** A click and a faint breath

**Gabarito editorial:** C
**Explicação:** The story says: "a click and a faint breath".

**Pista e continuidade:** A cópia integral preserva a busca por evidência e permite que Celia saia acompanhada; a discrepância foi preparada pela voz instável.

**Final da rota.**


## Only the Words She Wanted (`fathers-message`)

Celia copied only her father's message onto the black drive. She left the tape in the player and walked out through the open door. At home, she played the copy while calling her sister. "Take your time," their father said. Celia told her sister about the booth, too. She had not brought its strange voice home, but she did not have to keep the experience to herself.

**Pergunta:** What did Celia leave in the archive?

- **A.** The original tape
- **B.** Her black memory drive
- **C.** Her father's copied message

**Gabarito editorial:** A
**Explicação:** The story says: "left the tape in the player".

**Pista e continuidade:** A rota preserva somente a mensagem escolhida e encerra o objetivo pessoal; ela compartilha o medo em vez de permanecer isolada.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-listening-booth-a2`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-listening-booth-a2/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
