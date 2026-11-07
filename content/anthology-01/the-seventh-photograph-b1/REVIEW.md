# The Seventh Photograph — B1

**Gênero:** Mistério · **Estado:** publicado na VPS, com narração e animação 2D dark

Ao preparar uma exposição, Nadia descobre uma fotografia que contradiz o crédito dado à construção de uma ponte.

**Foco:** Comparação de evidências, distinção entre financiamento e execução, e inferência de motivação.

**Percursos:**

- sorting-table → beam-and-ledger → two-sources → carpenters-account
- sorting-table → beam-and-ledger → two-sources → construction-records

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## One Photograph Too Many (`sorting-table`)

Nadia was preparing a museum display about the town's first footbridge. Six glossy photographs showed the mayor at its opening. A seventh picture, printed on rougher paper, showed workers fitting a wooden beam. Their faces were clear, but the mayor was absent. The museum label credited him with building the bridge. Nadia set the picture aside. Before repeating that claim, she wanted to know whether the seventh photograph belonged to the same bridge.

**Pergunta:** Why did Nadia set the seventh photograph aside?

- **A.** She wanted to remove every photograph without the mayor
- **B.** She thought rough paper could not preserve a real photograph
- **C.** She wanted to check the claim on the museum label

**Gabarito editorial:** C
**Explicação:** The label credits the mayor, but the photograph shows other workers. Nadia wants to check the claim before repeating it.

**Pista e continuidade:** A diferença de suporte e conteúdo desperta uma pergunta investigável, sem concluir fraude antes das evidências.

**Próxima etapa:**

- Continue the story → `beam-and-ledger`

## A Mark in the Wood (`beam-and-ledger`)

In the gallery, Nadia compared the seventh photograph with a detail photograph of the bridge. Both showed a small triangular notch in the same beam. The picture did belong to this project. A grant ledger recorded the mayor's payment, but said nothing about who did the work. Beside it, a file guide listed construction records and permission letters in the downstairs records room. Nadia realised that paying for a bridge and building it were different kinds of contribution.

**Pergunta:** What did the matching triangular notch establish?

- **A.** The construction photograph showed the same bridge
- **B.** The mayor had personally repaired the beam
- **C.** The grant payment had never been made

**Gabarito editorial:** A
**Explicação:** The same notch appears in both photographs, linking the construction scene to the bridge in the gallery.

**Pista e continuidade:** A marca confirma identidade; o registro de doação não prova autoria material.

**Próxima etapa:**

- Continue the story → `two-sources`

## People and Paper (`two-sources`)

An assistant recognised one worker in the photograph as Rosa Vale, a retired carpenter who lived across the square. Nadia could visit her and ask what she remembered. She could also follow the file guide to the construction records downstairs. The photograph offered a question, not a complete answer. Nadia wanted to correct the display without replacing one unsupported story with another, so she chose how to gather the missing evidence.

**Pergunta:** What did Nadia want to avoid?

- **A.** Learning the names of the bridge workers
- **B.** Replacing one unsupported account with another
- **C.** Keeping any record of the mayor's payment

**Gabarito editorial:** B
**Explicação:** Nadia wants evidence, rather than another account that has not been checked.

**Pista e continuidade:** As rotas separam entrevista e pesquisa documental, ambas voltadas a confirmar a mesma história.

**Próxima etapa:**

- Visit Rosa Vale and ask about the photograph → `carpenters-account`
- Compare the photograph with the construction records → `construction-records`

## Rosa's Side of the Bridge (`carpenters-account`)

Rosa recognised the photograph immediately. She had cut that beam, while neighbours supplied wood and the mayor paid for the foundations. She showed Nadia her old work diary and agreed to lend both the diary and the photograph to the museum. The revised display gave each contribution its place. Watching Rosa read the new label, Nadia understood that the mystery had not been a missing bridge. It had been the people hidden behind an easy sentence.

**Pergunta:** What did the revised display change?

- **A.** It claimed that Rosa had paid for all the foundations
- **B.** It removed the mayor despite evidence of his payment
- **C.** It recognised several different contributions to the bridge

**Gabarito editorial:** C
**Explicação:** The display recognises the work, donated wood and foundation funding as different contributions.

**Pista e continuidade:** A entrevista ganha confirmação material e autorização dentro da ficção, sem apagar a contribuição financeira documentada.

**Final da rota.**


## The Names below the Grant (`construction-records`)

Downstairs, Nadia found signed construction records naming Rosa and the neighbours who had supplied timber. A permission letter allowed the museum to display their work records. The mayor's grant covered the foundations, not the whole project. Nadia prepared a new panel that separated those contributions and sent Rosa a copy before the exhibition. The seventh photograph became its centre. A familiar town story now had enough space for the people who had actually held the tools.

**Pergunta:** Why could Nadia distinguish the mayor's role from Rosa's?

- **A.** The records separated foundation funding from construction work
- **B.** The photograph alone listed every payment
- **C.** The permission letter said that the grant was imaginary

**Gabarito editorial:** A
**Explicação:** The construction records name the workers, while the grant covers the foundations. They show different roles.

**Pista e continuidade:** A rota documental confirma a mesma divisão de trabalho e permite uma conclusão completa sem a entrevista.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-seventh-photograph-b1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-seventh-photograph-b1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
