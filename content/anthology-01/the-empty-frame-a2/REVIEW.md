# The Empty Frame — A2

**Gênero:** Mistério · **Estado:** publicado na VPS, com narração e animação 2D dark

Uma curadora encontra uma moldura vazia antes de uma exposição e segue pistas de uma retirada inesperada.

**Foco:** Sequência de acontecimentos, causa e localização; passado simples e conectores comuns.

**Percursos:**

- gallery-morning → restoration-log → two-checks → restorer-explanation
- gallery-morning → restoration-log → two-checks → attic-storage

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## An Empty Place on the Wall (`gallery-morning`)

Etta opened the community museum before the exhibition. In the river gallery, the wooden frame was still on the wall, but the painting was gone. She checked the entrance desk. The workshop key hung there on its red cord. Nothing else seemed missing. She needed to find the painting before inviting visitors inside.

**Pergunta:** What was missing from the gallery?

- **A.** The wooden frame
- **B.** The painting
- **C.** The workshop key

**Gabarito editorial:** B
**Explicação:** The story says: "the painting was gone".

**Pista e continuidade:** A ausência é delimitada: a pintura sumiu, mas moldura e chave continuam nos lugares.

**Próxima etapa:**

- Continue the story → `restoration-log`

## A Page with a Blue Clip (`restoration-log`)

A blue clip marked a page in the restoration log on the desk. Etta read, "River painting moved after the rain. See storage notes." Beside the gallery wall, fresh water marks reached the floor. She remembered the heavy rain during the night. Perhaps someone had moved the painting to protect it, rather than steal it.

**Pergunta:** Why might someone have moved the painting?

- **A.** To change the wooden frame
- **B.** To prepare the entrance desk
- **C.** To protect it from water

**Gabarito editorial:** C
**Explicação:** The story says: "moved the painting to protect it".

**Pista e continuidade:** O registro e as marcas de água sustentam a hipótese de preservação.

**Próxima etapa:**

- Continue the story → `two-checks`

## Two Ways to Check (`two-checks`)

Etta took the red-cord key. The workshop door was on her left. She could use it and ask the restorer, who often arrived early. Behind the gallery, stairs led to the attic. Wet marks continued towards those stairs. She could also check the roof and storage area herself. She kept the log on the desk and considered both routes.

**Pergunta:** Where did the wet marks lead?

- **A.** Towards the attic stairs
- **B.** Towards the entrance door
- **C.** Towards the workshop window

**Gabarito editorial:** A
**Explicação:** The story says: "Wet marks continued towards those stairs".

**Pista e continuidade:** As duas rotas investigam a mesma causa; o registro não viaja indevidamente entre locais.

**Próxima etapa:**

- Use the key and speak to the restorer → `restorer-explanation`
- Follow the wet marks up to the attic → `attic-storage`

## The Restorer's Explanation (`restorer-explanation`)

Etta unlocked the workshop and found the restorer checking a damaged corner of the painting's protective cover. He had carried the painting to a dry storage box in the attic during the rain. Together they checked that it was safe. Etta delayed the exhibition and arranged a roof repair. The empty frame could wait; the painting could not risk another wet night.

**Pergunta:** Why did Etta delay the exhibition?

- **A.** The restorer needed to make a new painting
- **B.** The roof needed repair to protect the painting
- **C.** The entrance desk had been stolen

**Gabarito editorial:** B
**Explicação:** The story says: "arranged a roof repair".

**Pista e continuidade:** O restaurador confirma a retirada protetiva e a prioridade muda de abrir a exposição para preservar a obra.

**Final da rota.**


## Safe above the Gallery (`attic-storage`)

Etta climbed to the attic. On its dry side, she found the river painting inside a wide storage box. A note from the restorer explained that rain had entered above the frame. She closed the box again and took photographs of the leaking roof for a repair request. The painting was safe. Instead of opening the exhibition, she helped protect its future.

**Pergunta:** What did Etta photograph for the repair request?

- **A.** The wooden frame
- **B.** The entrance desk
- **C.** The leaking roof

**Gabarito editorial:** C
**Explicação:** The story says: "photographs of the leaking roof".

**Pista e continuidade:** A investigação direta confirma a mesma explicação sem depender de visitar a oficina.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-empty-frame-a2`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-empty-frame-a2/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
