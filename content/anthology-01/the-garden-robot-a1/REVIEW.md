# The Garden Robot — A1

**Gênero:** Ficção científica · **Estado:** publicado na VPS, com narração e animação 2D dark

Em uma estufa em Marte, Imani ajuda um pequeno robô a entregar as últimas sementes do dia.

**Foco:** Cores, localização e ações simples; presente simples. A tecnologia pertence à ficção.

**Percursos:**

- greenhouse-door → open-panel → delivery-choice → voice-again
- greenhouse-door → open-panel → delivery-choice → quiet-team

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## Inside the Greenhouse (`greenhouse-door`)

Imani works in a greenhouse on Mars. She wears a grey shirt. A small white robot waits by the work table. Its name is Pip. It has a blue light but no voice today. A yellow packet of seeds is on the table.

**Pergunta:** What is on the work table?

- **A.** A yellow packet of seeds
- **B.** A blue packet of tools
- **C.** A white packet of food

**Gabarito editorial:** A
**Explicação:** The story says: "A yellow packet of seeds".

**Pista e continuidade:** Apresenta-se a tarefa, o robô e o pacote sem exigir nenhum conhecimento de Marte.

**Próxima etapa:**

- Continue the story → `open-panel`

## An Open Panel (`open-panel`)

Imani looks at Pip. A small panel on its side is open. Inside, one cable is loose. Her tools are on the shelf. Pip turns towards the seeds. It can still move. The plants at the back of the greenhouse need these seeds today.

**Pergunta:** What is loose inside Pip?

- **A.** A wheel
- **B.** A cable
- **C.** A seed packet

**Gabarito editorial:** B
**Explicação:** The story says: "one cable is loose".

**Pista e continuidade:** A falha de voz não impede movimento; ambas as soluções são possíveis.

**Próxima etapa:**

- Continue the story → `delivery-choice`

## A Delivery to Make (`delivery-choice`)

Imani puts the yellow seed packet on Pip. The planting table is at the back of the greenhouse. She can fix the loose cable first. She can also guide Pip with her hand. The packet is light, and both plans are possible.

**Pergunta:** Where is the planting table?

- **A.** Beside the inner door
- **B.** Outside the greenhouse window
- **C.** At the back of the greenhouse

**Gabarito editorial:** C
**Explicação:** The story says: "at the back of the greenhouse".

**Pista e continuidade:** A escolha distingue reparar a voz de concluir a entrega por gestos, sem inventário jogável.

**Próxima etapa:**

- Fix Pip's cable before the delivery → `voice-again`
- Guide Pip to the planting table without speech → `quiet-team`

## A Voice Again (`voice-again`)

Imani fixes the cable and closes the panel. "Hello, Imani," Pip says. They take the seeds to the planting table. She opens the yellow packet. Pip holds it steady. As she plants, she tells the little robot about her first garden on Earth.

**Pergunta:** What does Imani close?

- **A.** Pip's panel
- **B.** The greenhouse door
- **C.** The seed packet

**Gabarito editorial:** A
**Explicação:** The story says: "closes the panel".

**Pista e continuidade:** A voz volta após a correção descrita; a entrega termina e abre espaço para um vínculo pessoal.

**Final da rota.**


## A Quiet Team (`quiet-team`)

Imani guides Pip to the planting table with her hand. Pip follows her slowly. She opens the yellow packet and plants the seeds. Pip cannot speak, but its blue light is bright. The delivery is complete. They can fix the cable after their work.

**Pergunta:** What can Pip still not do?

- **A.** Move
- **B.** Speak
- **C.** Carry the seeds

**Gabarito editorial:** B
**Explicação:** The story says: "Pip cannot speak".

**Pista e continuidade:** A entrega termina sem reparar a voz; o estado do painel não é misturado com o outro final.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-garden-robot-a1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-garden-robot-a1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
