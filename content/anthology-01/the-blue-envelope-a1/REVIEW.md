# The Blue Envelope — A1

**Gênero:** Mistério · **Estado:** publicado na VPS, com narração e animação 2D dark

Uma bibliotecária encontra um envelope sem nome e procura quem deixou uma fotografia dentro dele.

**Foco:** Localização, objetos e identificação de pessoas; presente simples.

**Percursos:**

- under-the-desk → old-photograph → two-places → bakery-answer
- under-the-desk → old-photograph → two-places → bench-answer

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## Under the Desk (`under-the-desk`)

Lena works in a small library. She wears a yellow cardigan. Today, she finds a blue envelope under the front desk. There is no name on it. She opens it carefully. A small photograph falls into her hand.

**Pergunta:** Where does Lena find the envelope?

- **A.** Under the front desk
- **B.** On a reading chair
- **C.** Beside the front door

**Gabarito editorial:** A
**Explicação:** The story says: "under the front desk".

**Pista e continuidade:** O envelope apresenta o objeto que Lena acompanha até os dois finais.

**Próxima etapa:**

- Continue the story → `old-photograph`

## An Old Photograph (`old-photograph`)

The photograph shows a red bench in a garden. Lena looks through the side door. The library courtyard has a red bench, too. On the back of the photograph, she sees two words: "For Dad." She wants to find the owner.

**Pergunta:** What does the photograph show?

- **A.** A blue chair in a kitchen
- **B.** A red bench in a garden
- **C.** A yellow table in a shop

**Gabarito editorial:** B
**Explicação:** The story says: "a red bench in a garden".

**Pista e continuidade:** O banco da fotografia corresponde ao banco visível no pátio.

**Próxima etapa:**

- Continue the story → `two-places`

## Two Places to Look (`two-places`)

Lena takes the photograph and the envelope. The baker across the street knows many people. In the courtyard, a woman is looking under the red bench. Lena can ask the baker or speak to the woman. Both places are close.

**Pergunta:** What is the woman doing?

- **A.** Opening the library window
- **B.** Carrying bread across the street
- **C.** Looking under the red bench

**Gabarito editorial:** C
**Explicação:** The story says: "looking under the red bench".

**Pista e continuidade:** A busca da mulher e o conhecimento do padeiro tornam as duas escolhas plausíveis.

**Próxima etapa:**

- Ask the baker about the photograph → `bakery-answer`
- Look for the red bench in the courtyard → `bench-answer`

## An Answer at the Bakery (`bakery-answer`)

Lena asks the baker. His daughter took the photograph in the library courtyard. He calls her on his phone. She comes across the street and collects the blue envelope. Her gift for Dad is ready, and Lena can return to work.

**Pergunta:** Who took the photograph?

- **A.** The baker's daughter
- **B.** Lena's sister
- **C.** The baker's teacher

**Gabarito editorial:** A
**Explicação:** The story says: "His daughter took the photograph".

**Pista e continuidade:** O padeiro identifica a destinatária indireta e encerra a busca sem contradizer a outra rota.

**Final da rota.**


## An Answer by the Bench (`bench-answer`)

Lena shows the photograph to the woman by the bench. "It is mine!" the woman says. "It is a gift for my father. He works in the bakery." Lena returns the blue envelope. The woman thanks her and goes across the street.

**Pergunta:** Where does the woman's father work?

- **A.** In the library
- **B.** In the bakery
- **C.** In the garden shop

**Gabarito editorial:** B
**Explicação:** The story says: "He works in the bakery".

**Pista e continuidade:** A dona recupera a fotografia e confirma a mesma relação familiar da rota da padaria.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-blue-envelope-a1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-blue-envelope-a1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
