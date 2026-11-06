# Desenvolvimento — primeiro incremento do domínio

## Objetivo e critérios

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

Os construtores fazem cópias defensivas e expõem coleções somente de leitura. Alterar arrays usados para construir uma história não modifica o conteúdo já carregado. Isso prepara a imutabilidade em memória; o versionamento no PostgreSQL ainda será implementado.

O gabarito e a explicação não são propriedades públicas do desafio. Mesmo assim, entidades Domain **não devem ser serializadas pela futura API**: ela usará DTOs próprios. O avaliador entrega a explicação ao código servidor que o chamou; a camada de aplicação ainda deverá controlar quando revelá-la.

## Validar

Execute os comandos do README com SDK .NET 10. Para acompanhar apenas a história de três cenas no debugger ou no Test Explorer:

```sh
dotnet test EnglishRoulette.sln --filter FullyQualifiedName~GetNextScene_FollowsThreeSceneStoryToItsEnding
```

Coloque um breakpoint em `StoryFlowEngine.GetNextScene`. A fixture original `The Notebook at Platform Three` passa por `platform → notebook → reunion`. Os outros testes exercitam respostas certas/erradas, alternativas inválidas, bifurcações, destinos inválidos e grafos sem final. Esta fixture é conteúdo de teste, sem narração de áudio produzida.

Ainda não há comando `dotnet run` nem validação visual no navegador: os projetos atuais são biblioteca e testes.

## Limites e decisões pendentes

- A validação implementada é **estrutural**, não um serviço completo de publicação. Direitos, revisão editorial, áudio, existência de mídia, dificuldade e metadados serão verificados quando catálogo e mídia forem implementados.
- Não existe sessão autoritativa neste incremento. A API futura não poderá usar `GetNextScene` como autorização para pular pergunta ou narração: o coordenador precisará validar identidade, fase, versão do estado e comandos idempotentes antes de navegar.
- Pontuação, XP, prazos, concorrência, retomada, autenticação, persistência, eventos e multiplayer ainda não foram implementados.
- `DECISION NEEDED`: política de ciclos intencionais e limite de cenas percorridas (RF-010). O validador atual garante que todas as cenas tenham uma rota para um final, mas permite ciclos que tenham saída. Antes de publicar conteúdo real, definir como impedir percursos infinitos durante uma sessão.
- `DECISION NEEDED`: confirmar as tabelas de pontuação, bônus e XP propostas antes de implementá-las; este incremento não aplica nenhuma dessas fórmulas.

## Próximos incrementos

1. Sessão Solo Adventure em Domain/Application: fases explícitas, relógio injetável, versão do estado e idempotência com testes.
2. Persistência PostgreSQL/EF Core e migrations mínimas, com integração contra banco real.
3. API REST, identidade de convidado e contratos públicos sem gabaritos.
4. História original com três cenas, áudio e metadados de publicação.
5. Blazor com narração, pergunta, feedback e retomada da sessão.

Os projetos são acrescentados conforme forem necessários. A mesma regra de navegação e avaliação será usada por solo e multiplayer.
