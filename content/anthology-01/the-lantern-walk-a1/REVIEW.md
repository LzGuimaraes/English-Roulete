# The Lantern Walk — A1

**Gênero:** Aventura · **Estado:** publicado na VPS, com narração e animação 2D dark

Omar leva uma lanterna ao encontro do bairro e escolhe entre dois caminhos do parque.

**Foco:** Direções, pessoas, objetos e finalidade explícita; presente simples.

**Percursos:**

- park-entrance → map-paths → people-on-paths → picnic-table
- park-entrance → map-paths → people-on-paths → picnic-stage

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## At the Park Entrance (`park-entrance`)

Omar wears a blue jacket and carries a green lantern. Tonight, his neighbours have a picnic in the park. The lights at their food table are not working. He wants to help. At the park entrance, he opens a paper map.

**Pergunta:** What does Omar carry?

- **A.** A blue basket
- **B.** A yellow umbrella
- **C.** A green lantern

**Gabarito editorial:** C
**Explicação:** The story says: "carries a green lantern".

**Pista e continuidade:** A lanterna resolve uma necessidade apresentada desde o início.

**Próxima etapa:**

- Continue the story → `map-paths`

## Two Paths on the Map (`map-paths`)

The map shows two paths to the picnic. The left path goes beside a pond. The right path goes through an orchard. Both paths are open. Omar folds the map and puts it in his pocket. He can see the pond from the entrance.

**Pergunta:** Where does the left path go?

- **A.** Beside a pond
- **B.** Through an orchard
- **C.** Across a car park

**Gabarito editorial:** A
**Explicação:** The story says: "The left path goes beside a pond".

**Pista e continuidade:** Os dois trajetos são apresentados e permanecem disponíveis, sem condição de código.

**Próxima etapa:**

- Continue the story → `people-on-paths`

## People on the Paths (`people-on-paths`)

A neighbour waits on a bench beside the pond. Two musicians stand by the orchard path. They also want to reach the picnic. Omar can walk with the neighbour or the musicians. He holds the lantern high so they can see him.

**Pergunta:** Who stands by the orchard path?

- **A.** One neighbour on a bench
- **B.** Two musicians
- **C.** Three people with food

**Gabarito editorial:** B
**Explicação:** The story says: "Two musicians stand by the orchard path".

**Pista e continuidade:** A escolha altera a companhia e o lugar onde a ajuda será aproveitada.

**Próxima etapa:**

- Take the path beside the pond → `picnic-table`
- Take the path through the orchard → `picnic-stage`

## Light for the Table (`picnic-table`)

Omar takes the pond path with his neighbour. They reach the picnic together. He puts the green lantern on the food table. Now everyone can see the plates. His neighbour gives him some bread. Omar sits down, happy to share the evening.

**Pergunta:** Where does Omar put the lantern?

- **A.** On a pond bench
- **B.** Beside the stage
- **C.** On the food table

**Gabarito editorial:** C
**Explicação:** The story says: "on the food table".

**Pista e continuidade:** O objetivo inicial é cumprido diretamente: iluminar a mesa do encontro.

**Final da rota.**


## Light for the Music (`picnic-stage`)

Omar walks through the orchard with the musicians. At the picnic, another neighbour brings a lamp for the food table. Omar puts his green lantern beside the small stage. The musicians begin to play. He stays to listen, glad that both places have light.

**Pergunta:** Where does Omar put his lantern?

- **A.** Beside the small stage
- **B.** On the food table
- **C.** Under an orchard tree

**Gabarito editorial:** A
**Explicação:** The story says: "beside the small stage".

**Pista e continuidade:** A lanterna apoia outra parte do mesmo encontro; a outra lâmpada só existe nesta rota e é apresentada no texto.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-lantern-walk-a1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-lantern-walk-a1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
