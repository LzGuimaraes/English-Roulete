# Desenvolvimento — incrementos do núcleo

O primeiro incremento implementou conteúdo e regras de cenas/perguntas. O segundo acrescentou uma sessão Solo Adventure em memória e o projeto Application, com relógio injetável. O contrato e os testes da sessão estão em [SOLO_SESSION.md](SOLO_SESSION.md). O terceiro conecta uma demonstração Blazor + API para experimentar no navegador, descrita em [LOCAL_DEMO.md](LOCAL_DEMO.md).

O incremento atual adiciona [persistência PostgreSQL](PERSISTENCE.md), concorrência entre instâncias do agregado e narração pré-gravada com duração validada. A API recupera checkpoints; o Blazor reproduz arquivos WAV, sem síntese de voz ou IA durante a partida.

## Primeiro incremento: objetivo e critérios

Iniciar o Slice 1 de `GAME_RULES.md`, seção 13, com as duas primeiras regras independentes de infraestrutura:

- Percorrer uma versão de história publicada até uma cena final, sem sair do grafo daquela versão (RF-010, RF-013; ENG-001, ENG-002, ENG-003 e ENG-024).
- Avaliar uma pergunta `MULTIPLE_CHOICE`, rejeitando uma alternativa inexistente (RF-018 e RF-019).
- Proteger o conteúdo carregado contra alterações nas coleções originais (parte estrutural de ENG-020).

Estes critérios cobrem o núcleo de regras, não o aceite completo de uma aventura solo. Os testes de ramificações validam a navegação; escolhas persistidas de jogador pertencem a outro incremento.

## Classes e fluxo

| Classe | Papel | Conceito C# |
|---|---|---|
| `StoryVersion` | Identidade da versão, status editorial, cena inicial e cenas | Classe selada, enum e propriedades somente de leitura |
| `StoryScene` | Texto, questão e transições de uma cena | Cópia defensiva das coleções |
| `SceneTransition` | Chave da escolha, rótulo e destino | Record imutável |
| `MultipleChoiceChallenge` | Prompt, alternativas e gabarito interno | Invariantes no construtor; membros `internal` |
| `AnswerOption` | Chave e texto de alternativa | Record imutável |
| `StoryFlowEngine` | Entrada, validação estrutural e próxima cena | Serviço puro sem I/O e sem estado compartilhado mutável |
| `ChallengeEvaluator` | Correção e explicação de alternativa válida | Função determinística e record de resultado |
| `GameRuleException` | Código estável e mensagem de violação de regra | Exceção específica de domínio |

O fluxo testado é `GetInitialScene → Evaluate → GetNextScene`, repetido até `GetNextScene` retornar `null` no final. Uma única transição pode ser seguida automaticamente; em uma bifurcação é preciso informar a **chave da transição**, não a chave da cena de destino.

`ValidateForPlay` rejeita rascunhos, destinos ausentes, cenas de outra versão, cenas desconectadas, finais com saídas, cenas não finais sem saídas e qualquer caminho sem possibilidade de atingir um final. As chaves são comparadas exatamente, com diferenciação entre maiúsculas e minúsculas.

Os construtores fazem cópias defensivas e expõem coleções somente de leitura. O checkpoint PostgreSQL também congela a definição completa usada pela sessão; novas versões podem ser publicadas pelo [painel editorial](ADMIN.md), preservando as anteriores.

O gabarito e a explicação não são propriedades públicas do desafio. Mesmo assim, entidades Domain **não devem ser serializadas pela futura API**: ela usará DTOs próprios. O avaliador entrega a explicação ao código servidor que o chamou; a camada de aplicação ainda deverá controlar quando revelá-la.

## Validar

Execute os comandos do README com SDK .NET 10. Para acompanhar apenas a história de três cenas no debugger ou no Test Explorer:

```sh
dotnet test EnglishRoulette.sln --filter FullyQualifiedName~GetNextScene_FollowsThreeSceneStoryToItsEnding
```

Coloque um breakpoint em `StoryFlowEngine.GetNextScene`. A fixture original `The Notebook at Platform Three` passa por `platform → notebook → reunion`. Os outros testes exercitam respostas certas/erradas, alternativas inválidas, bifurcações, destinos inválidos e grafos sem final. Esta fixture é conteúdo de teste, sem narração de áudio produzida.

A demonstração local é preparada com `python3 scripts/dev.py setup` e iniciada com `python3 scripts/dev.py run`, em http://localhost:5080. Os testes do domínio continuam independentes da interface.

## Limites e decisões pendentes

- A validação implementada é **estrutural**, não um serviço completo de publicação. Direitos, revisão editorial, áudio, integridade de mídia, dificuldade e metadados são verificados pela camada editorial, descrita em [ADMIN.md](ADMIN.md).
- A sessão acrescentada no segundo incremento controla proprietário, fase, versão do estado e comandos idempotentes antes de navegar. A API futura deverá autenticar o convidado; informar um `PlayerId` não comprova identidade.
- Prazos, retomada persistente, cookie opaco e concorrência entre instâncias já funcionam na prévia. Login ADMIN e catálogo editorial também estão implementados. Pontuação, XP, eventos de jogo e multiplayer ainda não foram implementados.
- `DECISION NEEDED`: política de ciclos intencionais e limite de cenas percorridas (RF-010). O validador atual garante que todas as cenas tenham uma rota para um final, mas permite ciclos que tenham saída. A publicação V1 rejeita ciclos conforme `DATABASE.md`; ciclos intencionais continuam sendo uma decisão para versões futuras.
- `DECISION NEEDED`: confirmar as tabelas de pontuação, bônus e XP propostas antes de implementá-las; este incremento não aplica nenhuma dessas fórmulas.

## Próximos incrementos

1. Validar fórmulas de pontuação e XP antes de implementá-las.
2. Evoluir o histórico relacional de partidas mantendo os checkpoints e a compatibilidade das sessões salvas.
3. Evoluir o painel editorial e o pipeline de mídia conforme o uso real.
4. Acrescentar worker de prazos, retenção e contratos HTTP de produção.
5. Reutilizar o motor em Practice e depois em multiplayer.

Os projetos são acrescentados conforme forem necessários. A mesma regra de navegação e avaliação será usada por solo e multiplayer.
