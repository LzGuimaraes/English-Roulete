# The Mountain Post — B1

**Gênero:** Aventura · **Estado:** publicado na VPS, com narração e animação 2D dark

Uma voluntária recolhe a última correspondência de uma estação de montanha e decide como devolvê-la a quem a escreveu.

**Foco:** Inferência de motivação, comparação de rotas e consequências sociais sem resposta narrativa certa.

**Percursos:**

- last-collection → old-timetable → descent-choice → village-return
- last-collection → old-timetable → descent-choice → farm-return

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## The Last Collection (`last-collection`)

Jo reached the weather station along its maintained service track. The building would close the next day, and Nico, its retired observer, had asked her to collect the last mail from the porch postbox. He had lent her its key. Inside, she found one sealed packet with his name on it. She placed it in her canvas mail bag. Returning it seemed a small task, but she knew how much this final connection to the station mattered to him.

**Pergunta:** Why was collecting the packet important beyond the delivery itself?

- **A.** It would allow Jo to become the new weather observer
- **B.** It was Nico's final connection to the closing station
- **C.** It proved that the station would remain open indefinitely

**Gabarito editorial:** B
**Explicação:** The station is closing, and the last mail matters to Nico as a final connection to his former workplace.

**Pista e continuidade:** A entrega é pequena, mas tem significado pessoal estabelecido antes das escolhas.

**Próxima etapa:**

- Continue the story → `old-timetable`

## The Route on the Wall (`old-timetable`)

In the unlocked workroom, Jo studied a route map before descending. An old timetable marked the village cooperative as the mail collection point. A recent note added Nico's farm beside the hillside path. Both signed paths were maintained and open. The forest route would bring her to people who had worked with him; the hillside route would let her hand over the packet directly. Jo checked the sealed envelope once more, then folded the current route sheet into her pocket.

**Pergunta:** What was the main advantage of the hillside route?

- **A.** Jo could avoid using an open, maintained path
- **B.** Jo could leave the packet in the station workroom
- **C.** Jo could give the packet directly to Nico

**Gabarito editorial:** C
**Explicação:** The hillside route ends beside Nico's farm, allowing Jo to hand the packet to him directly.

**Pista e continuidade:** O registro antigo e a anotação recente explicam por que há dois destinos razoáveis.

**Próxima etapa:**

- Continue the story → `descent-choice`

## Different Ways to Return (`descent-choice`)

At the path junction, Jo met a cooperative worker who was returning through the forest. The worker remembered sharing meals with Nico during winter supply runs. She offered to carry news of the station's closure back to the village. The hillside path remained clear, leading towards the roof of Nico's farmhouse below. Jo could reconnect the packet with its wider community first, or preserve the quiet of a personal delivery. She did not need to open it to choose.

**Pergunta:** What information could the cooperative worker bring to the village?

- **A.** News that the weather station was closing
- **B.** The private contents of Nico's sealed packet
- **C.** A promise that Nico was returning to work

**Gabarito editorial:** A
**Explicação:** The worker offers to take news of the closure to the village. The packet stays sealed.

**Pista e continuidade:** A escolha diz respeito à forma da devolução, não ao direito de ler correspondência alheia.

**Próxima etapa:**

- Take the forest path to the village cooperative → `village-return`
- Take the hillside path to Nico's farm → `farm-return`

## A Table in the Village (`village-return`)

Jo followed the worker to the cooperative. They contacted Nico, who came down from his farm to collect the sealed packet. Around the familiar kitchen table, he opened it: former colleagues had sent photographs from his years at the station. The visit became an afternoon of shared memories. Jo left the borrowed key with him and walked home. Her route had taken longer, but it had returned a piece of the mountain to the village as well as to its keeper.

**Pergunta:** What consequence followed Jo's village route?

- **A.** The packet was opened before Nico arrived
- **B.** Nico shared the photographs and memories with other people
- **C.** Jo kept Nico's borrowed key as a souvenir

**Gabarito editorial:** B
**Explicação:** Nico opens the packet at the cooperative, and the visit becomes an afternoon of shared memories.

**Pista e continuidade:** O próprio destinatário abre a correspondência, e o trajeto cria um reencontro comunitário.

**Final da rota.**


## The Last Letter at Home (`farm-return`)

Jo followed the hillside path to the farm and gave Nico the sealed packet with his key. He opened it beside the kitchen window. Former colleagues had sent photographs of the station and a short letter thanking him for keeping its door open to travellers. Nico chose one photograph for the wall and put the others away carefully. Jo stayed for tea, then left him with his own memories. The station could close without taking every part of his work away.

**Pergunta:** Why did the personal delivery suit this ending?

- **A.** It let Jo decide which private photographs Nico should keep
- **B.** It required Nico to return the packet without opening it
- **C.** It gave Nico space to receive and choose his memories quietly

**Gabarito editorial:** C
**Explicação:** At home, Nico can choose a photograph and receive the memories in a quiet personal visit.

**Pista e continuidade:** O conteúdo é idêntico ao outro final, mas o acolhimento acontece em uma conversa privada.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-mountain-post-b1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-mountain-post-b1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
