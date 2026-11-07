# The Inventory of Empty Rooms — B1

**Gênero:** Terror psicológico · **Estado:** publicado na VPS, com narração e animação 2D dark

Uma vistoriadora precisa certificar uma pensão vazia, mas o livro de hóspedes começa a registrá-la como moradora.

**Foco:** Contraste entre observação e registro, inferências cautelosas e escolha diante de evidência incerta.

**Percursos:**

- inspection-kit → six-empty-rooms → her-name → ledger-outside
- inspection-kit → six-empty-rooms → her-name → survey-only

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## A Form for an Empty Building (`inspection-kit`)

Dalia arrived to inspect a boarding house that had been closed for years. Her agency wanted a simple statement that its six rooms were empty. She carried the survey in a blue envelope and a brass key on a yellow tag. Inside, a black guest ledger lay on the reception desk. Dust covered the counter around it, but not the book. She put down her envelope without opening the ledger. First, she would check the rooms.

**Pergunta:** What made the ledger stand out from its surroundings?

- **A.** It had no dust while the counter around it did
- **B.** It was the only object mentioned on the survey form
- **C.** It had been locked inside one of the six rooms

**Gabarito editorial:** A
**Explicação:** The book is clean even though dust covers the counter around it. That difference catches Dalia's attention.

**Pista e continuidade:** O livro deslocado do ambiente é a primeira anomalia; envelope e registro nunca são o mesmo objeto.

**Próxima etapa:**

- Continue the story → `six-empty-rooms`

## Six Doors, Six Names (`six-empty-rooms`)

Dalia checked all six rooms. The beds were bare, the cupboards open, and no personal belongings remained. Back at reception, she opened the ledger. Six names filled the current page, each followed by the word "present." She checked the dates against her survey: they were today's. Perhaps someone had recently used the building. Yet the rooms had contained none of the small signs of ordinary life. She left the ledger open while writing down what she had actually observed.

**Pergunta:** Why did Dalia hesitate to accept the ledger as proof of residents?

- **A.** The ledger listed fewer rooms than the building had
- **B.** The rooms contained no signs of ordinary life
- **C.** Her agency had forbidden her to inspect the cupboards

**Gabarito editorial:** B
**Explicação:** The ledger lists current guests, but the empty rooms do not support that account.

**Pista e continuidade:** Há conflito entre evidência física e registro; o texto não confirma moradores invisíveis como fato.

**Próxima etapa:**

- Continue the story → `her-name`

## A Place Already Written (`her-name`)

When Dalia looked up from the survey, one entry had changed. Her own full name now occupied the last line of the guest ledger. The handwriting resembled hers, although her pen had touched only the survey. The yellow-tag key was still beside the blue envelope. Both the front door and courtyard exit were open. She could take the book to her agency or leave it behind. Either way, she would not sign a claim that she could no longer support.

**Pergunta:** What had Dalia decided before choosing what to take?

- **A.** To sign the guest ledger as a new resident
- **B.** To lock both exits and inspect the rooms again
- **C.** Not to certify a claim she could not support

**Gabarito editorial:** C
**Explicação:** Dalia says she will not sign a claim she can no longer support, before deciding whether to take the book.

**Pista e continuidade:** O nome envolve a protagonista, mas as saídas continuam disponíveis e a decisão profissional é explícita.

**Próxima etapa:**

- Take the ledger to the agency as evidence → `ledger-outside`
- Leave the ledger and submit only the survey → `survey-only`

## A Book beyond the Gate (`ledger-outside`)

Dalia placed the ledger and survey envelope under her arm, took the key, and walked through the front door. Outside the gate, every page of the ledger was blank. At the agency, she showed the book to a colleague and described the six names and her own. He recorded her account without asking her to return alone. The inspection remained incomplete. For Dalia, leaving a form unfinished felt less frightening than pretending she had understood the building.

**Pergunta:** Why did Dalia leave the inspection incomplete?

- **A.** She could not honestly explain or certify what she had found
- **B.** She had forgotten to bring the survey envelope
- **C.** Her colleague asked her to sign the blank guest pages

**Gabarito editorial:** A
**Explicação:** Dalia cannot honestly explain the conflicting evidence, so she does not certify the inspection.

**Pista e continuidade:** A anomalia do registro é levada a outra pessoa e o contrato fica sem certificação; não se inventa uma explicação ou ameaça nova.

**Final da rota.**


## An Address She Did Not Accept (`survey-only`)

Dalia left the guest ledger open and took only her survey envelope and key. At the courtyard gate, she checked the form: it still contained her careful notes, not a list of residents. She submitted a report marked "occupancy unverified" and declined another visit without a colleague. At home, she put the yellow-tag key in the agency return box. Whatever the ledger had called her, she had chosen not to make its address her own.

**Pergunta:** What did Dalia take away from the building?

- **A.** The guest ledger and its six names
- **B.** Her survey envelope and key, but not the ledger
- **C.** Only a blank form from the reception desk

**Gabarito editorial:** B
**Explicação:** She leaves the ledger inside and takes her own survey envelope and key through the courtyard exit.

**Pista e continuidade:** A rota mantém os registros separados e encerra a experiência com um limite pessoal claro.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-inventory-of-empty-rooms-b1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-inventory-of-empty-rooms-b1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
