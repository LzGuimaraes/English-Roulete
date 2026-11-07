# The Second Cup — A1

**Gênero:** Terror psicológico · **Estado:** publicado na VPS, com narração e animação 2D dark

Ao fechar um café vazio, Nora encontra uma segunda xícara quente e precisa decidir por qual saída partir.

**Foco:** Quantidades, temperatura, localização e ações explícitas; presente simples.

**Percursos:**

- closing-time → two-cups → two-exits → across-the-street
- closing-time → two-cups → two-exits → courtyard-exit

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## Closing Time (`closing-time`)

Nora closes the café at night. She puts on her brown coat. The street is quiet. She makes one cup of tea and puts it on a table near the front door. Steam rises from the cup. She goes to get her keys.

**Pergunta:** How many cups of tea does Nora make?

- **A.** Two cups
- **B.** One cup
- **C.** Three cups

**Gabarito editorial:** B
**Explicação:** The story says: "one cup of tea".

**Pista e continuidade:** A quantidade inicial é explícita e sustenta a estranheza seguinte.

**Próxima etapa:**

- Continue the story → `two-cups`

## Two Cups on the Table (`two-cups`)

Her keys are on the table. Now there are two white cups beside them. Steam rises from both cups. Nora stops. The front door is closed. She calls, "Hello?" No one answers. She does not want to stay alone.

**Pergunta:** Where are Nora's keys?

- **A.** Behind the counter
- **B.** Inside her coat
- **C.** On the table

**Gabarito editorial:** C
**Explicação:** The story says: "Her keys are on the table".

**Pista e continuidade:** Duas xícaras quentes surgem no lugar onde havia apenas uma; as chaves não mudam de posição.

**Próxima etapa:**

- Continue the story → `two-exits`

## A Way Out (`two-exits`)

Nora takes her keys. She leaves both cups on the table. The bakery across the street is open. Behind her, the kitchen door leads to a small courtyard. Its gate is open. She can use the front door or the kitchen.

**Pergunta:** What does Nora leave on the table?

- **A.** Both cups
- **B.** Her keys
- **C.** Her coat

**Gabarito editorial:** A
**Explicação:** The story says: "She leaves both cups on the table".

**Pista e continuidade:** As duas saídas existem antes da escolha, e nenhuma exige uma ação imprudente.

**Próxima etapa:**

- Leave through the front door → `across-the-street`
- Leave through the kitchen and courtyard → `courtyard-exit`

## Across the Street (`across-the-street`)

Nora locks the front door and enters the bakery. The baker sees her shaking hands. He gives her a chair. From the bakery window, she can see her café table. There are still two cups. She stays with the baker until morning.

**Pergunta:** Where does Nora stay until morning?

- **A.** In the café kitchen
- **B.** In the bakery
- **C.** In the courtyard

**Gabarito editorial:** B
**Explicação:** The story says: "She stays with the baker until morning".

**Pista e continuidade:** Nora encontra companhia e conclui sua saída; a causa da segunda xícara continua incerta.

**Final da rota.**


## The Courtyard Gate (`courtyard-exit`)

Nora walks through the empty kitchen and leaves by the courtyard gate. Outside, she calls the café owner. "I only made one cup," she says. He stays on the phone. She reaches the main street and goes home. Tomorrow, they will return together.

**Pergunta:** Who stays on the phone with Nora?

- **A.** The baker
- **B.** A kitchen worker
- **C.** The café owner

**Gabarito editorial:** C
**Explicação:** The story says: "He stays on the phone".

**Pista e continuidade:** A protagonista sai e combina uma volta acompanhada, encerrando sua decisão sem explicar cedo o mistério.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-second-cup-a1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-second-cup-a1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
