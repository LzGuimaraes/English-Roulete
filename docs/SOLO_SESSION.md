# Solo Adventure — sessão em memória

## Critérios deste incremento

- Um único jogador por sessão, com uma versão publicada de história fixada na criação (RF-SOLO-008).
- Abrir pergunta após a janela da narração definida pelo servidor e aceitar uma única alternativa em `[opensAtUtc, closesAtUtc)` (RF-SOLO-002, RF-029; ENG-004).
- Fechar a pergunta ao aceitar resposta, inclusive errada, e revelar a explicação; alternativa inválida não consome a tentativa.
- Retomar sem reiniciar prazo; se a janela já venceu, registrar timeout e permitir continuar a narrativa (RF-SOLO-006; ENG-015 e ENG-016, somente em memória).
- Repetir um comando aceito sem executar novamente; rejeitar reutilização da chave para outro conteúdo (ENG-005 e ENG-006).
- Não permitir pular narração, pergunta ou escolha pendente. Proteger a execução simultânea sobre o mesmo agregado (parte em memória de ENG-018).

## Papéis e conceitos C#

| Arquivo | Papel |
|---|---|
| `Domain/Sessions/SoloAdventureSession.cs` | Agregado com proprietário, cena atual, fase, versão, prazos e recibos dos comandos |
| `Domain/Sessions/SoloAdventureCommand.cs` | Records tipados para os quatro comandos aceitos |
| `Domain/Sessions/SoloAdventureSnapshot.cs` | Enum de fases, tempos congelados e records imutáveis de pergunta, resultado e snapshot |
| `Application/Solo/SoloAdventureCoordinator.cs` | Obtém UTC por `TimeProvider` e entrega comandos ao domínio |
| `SoloAdventureSessionTests.cs` | Exercita o fluxo com relógio manual, sem esperas reais nem pacotes adicionais |

O domínio recebe o horário explicitamente e não consulta relógio global. O coordenador é o ponto de entrada do código servidor: a futura API não deverá aceitar horário, acerto ou duração enviados pelo navegador. O novo projeto Application depende somente de Domain; não foi adicionada dependência NuGet.

As durações de todas as cenas são copiadas na criação da sessão e precisam ser positivas, com exatamente uma configuração para cada cena. `NarrationDuration` representa a janela total de narração, incluindo a margem que o servidor vier a definir. A futura integração com mídia deverá obter esse valor dos metadados validados. Hoje ainda não há reprodução ou verificação de áudio.

## Fluxo implementado

```text
Preloading → StartSoloNarration → Narrating
  → prazo da narração → QuestionOpen
  → resposta aceita ou timeout → QuestionReveal
  → ContinueSoloAdventure
      → próxima cena: Preloading
      → bifurcação: DecisionOpen → ChooseSoloPath → Preloading
      → cena final: StoryResult
```

A versão de história já foi escolhida no construtor, portanto esta implementação começa em `Preloading`. Seleção da história e os resultados transitórios de cena não são fases persistidas aqui. `StoryResult` indica que a narrativa terminou; ainda não executa `MatchResult`, `Completed`, pontos ou XP.

Uma nova visita à cena recebe outro `QuestionInstanceId`, mesmo quando o desafio é o mesmo. A resposta precisa citar essa instância, impedindo que um envio de uma cena anterior seja usado na pergunta atual. Escolhas de caminho usam a chave da **transição**, conforme `StoryFlowEngine`.

Snapshots em `Narrating` escondem a pergunta. Em `QuestionOpen`, expõem apenas prompt, opções, ID e prazos; o resultado e a explicação aparecem depois do fechamento. Os snapshots são visões internas imutáveis; endpoints futuros ainda precisarão de DTOs em Contracts.

## Prazos e retomada

Com uma janela de narração de 30 segundos e uma pergunta de 15 segundos, iniciar a narração em `12:00:00Z` fixa a pergunta em `[12:00:30Z, 12:00:45Z)`. Retomar em `12:02:00Z` encontra timeout com fechamento em `12:00:45Z`; não cria mais 15 segundos.

Os prazos são aplicados na próxima leitura de snapshot ou execução de comando. Não existe worker nem timer de background neste incremento. Cada mudança de fase incrementa `StateVersion`, inclusive mudanças causadas pelo tempo; uma retomada tardia pode atravessar abertura e expiração da pergunta em uma chamada.

## Comandos, versões e retry

Todo comando inclui `RequestId` não vazio e `ExpectedStateVersion`. A identidade do ator é passada separadamente pelo servidor e deve corresponder ao proprietário da sessão.

1. Validar proprietário, antes de consultar recibos ou atualizar prazos.
2. Se já existe recibo desse `RequestId`, comparar o record completo do comando, incluindo tipo, versão esperada e payload. Se for igual, devolver o snapshot original; se diferir, retornar `IDEMPOTENCY_CONFLICT`.
3. Aplicar prazos vencidos; validar instância e tentativa de resposta, quando aplicável.
4. Exigir a versão atual do estado e a fase correta, executar a transição e guardar o recibo.

Depois de um replay, a resposta pode representar uma cena antiga: é o recibo original. Para obter o estado corrente, chamar `GetSnapshot`. Não transformar um retry no mesmo comando com outro payload ou outra versão esperada; isso exige um novo `RequestId`.

Rejeições não geram recibo de sucesso. Atualizações de prazo são autoritativas mesmo quando o novo comando é rejeitado. Por exemplo, uma resposta recebida no limite registra timeout e retorna `DEADLINE_EXPIRED`. A futura transação de persistência precisará preservar a atualização de prazo mesmo que rejeite o envio.

| Código | Situação |
|---|---|
| `NOT_PARTICIPANT` | Ator não é o proprietário |
| `STATE_CONFLICT` | Estado mudou desde o snapshot usado para um comando novo |
| `INVALID_GAME_PHASE` | Comando incompatível com a fase |
| `INVALID_QUESTION` | Instância diferente da pergunta atual |
| `INVALID_ANSWER` | Alternativa inexistente |
| `ALREADY_ANSWERED` | A tentativa válida já foi consumida |
| `DEADLINE_EXPIRED` | Pergunta atual encerrada por tempo |
| `IDEMPOTENCY_CONFLICT` | Mesmo RequestId reutilizado para outro comando |
| `INVALID_CHOICE` | Transição indisponível na decisão atual |

O `lock` no agregado torna validação, mutação e recibo atômicos para chamadas que compartilham **o mesmo objeto de sessão**. Isso não substitui índices únicos, controle otimista e transações no PostgreSQL. Objetos distintos ou processos separados ainda não compartilham proteção nem recibos.

## Validar e estudar

Com o SDK .NET 10, execute as verificações completas do README. Para rodar apenas este incremento:

```sh
dotnet test EnglishRoulette.sln --filter FullyQualifiedName~SoloAdventureSessionTests
```

No Test Explorer/debugger, acompanhe:

- `Continue_FollowsThreeScenesAndStopsAtStoryResult`: cena, pergunta, resposta e avanço até o final.
- `GetSnapshot_DelayedResumeDoesNotRestartQuestionTimer_Eng015_Eng016`: retomada após o prazo.
- `SubmitAnswer_ReplayReturnsOriginalReceiptEvenAfterAdvancing_Eng005`: retry sem efeitos repetidos.
- `Execute_ConcurrentDifferentAnswersAcceptsOnlyOne`: dois envios concorrentes e somente uma resposta aceita.

Ainda não há `dotnet run`, tela ou recuperação após reiniciar o processo. A próxima etapa é persistir estado, prazos, respostas e recibos em PostgreSQL e testar concorrência contra banco real.

`DECISION NEEDED`: seguem pendentes as fórmulas de pontuação/XP e a política de ciclos intencionais do grafo. Esta sessão não concede recompensas nem define silenciosamente essas regras.
