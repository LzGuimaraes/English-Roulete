# Persistência de sessões solo

O primeiro incremento de PostgreSQL salva o agregado solo como checkpoint JSONB. Ele permite reconstruir uma sessão em outro processo, preservando conteúdo, instância da pergunta, prazos e recibos idempotentes (RF-SOLO-006, RF-SOLO-008; ENG-005, ENG-015, ENG-016 e ENG-018).

## Desenho implementado

| Componente | Papel |
|---|---|
| `StoryDefinition` | Cópia explícita do conteúdo da versão, incluindo gabaritos; somente no servidor |
| `SoloAdventureCheckpoint` | Estado completo e recibos de comandos aceitos |
| `SoloAdventureSession.Capture/Restore` | Exporta e reconstrói o agregado, validando fase, cena e propriedade dos recibos |
| `ISoloSessionStore` | Porta específica de leitura, criação e gravação condicional |
| `PersistentSoloAdventure` | Aplica domínio, conserva transições de prazo e resolve conflitos de gravação |
| `PostgresSoloSessionStore` | EF Core/Npgsql, JSONB e escrita atômica por revisão |
| `AdventureDbContext` | Mapeamento e migration inicial |

A versão completa da história é congelada junto ao checkpoint. Uma sessão salva não depende da versão atual do conteúdo de demonstração no código. Esses objetos contêm gabaritos e não pertencem a Contracts; a API continua mapeando apenas os campos públicos.

`checkpoint_json` inclui estado e recibos na mesma gravação. Não há replay de eventos para reconstruir o jogo: trata-se de um snapshot do agregado, não event sourcing.

## Concorrência e erros

Uma leitura devolve o checkpoint e sua `revision`. Depois de executar a regra, a gravação usa `UPDATE ... WHERE credential_hash = ... AND revision = ...`, incrementando a revisão. Apenas um concorrente vence. Se houver conflito, a aplicação recarrega o banco e reaplica o comando; um request já aceito encontra seu recibo e não executa novamente.

Uma resposta expirada pode causar uma transição válida para timeout e depois ser rejeitada. A aplicação grava essa transição **antes** de devolver `DEADLINE_EXPIRED`. Não descarta o fechamento por causa do erro do envio.

Os testes usam DbContexts diferentes e PostgreSQL real, sem compartilhar o lock em memória da sessão. A criação de uma nova aventura e a revogação da credencial anterior pertencem à mesma transação.

O banco armazena SHA-256 de uma credencial aleatória, não o token do cookie. O hash é chave primária; constraints exigem 64 caracteres hexadecimais e revisão positiva. A API deve validar a credencial antes de fornecer acesso ao agregado.

## Migration e ambiente

- PostgreSQL 17 em container exclusivo, porta `127.0.0.1:55432`, volume nomeado `englishroulette_postgres_data`.
- `infra/.env` contém credenciais locais, fica fora do Git e é criado com permissão 0600. `infra/.env.example` não contém senha.
- Connection string recebida por `ConnectionStrings__Adventure`, nunca gravada em appsettings versionado.
- A migration inicial cria apenas `solo_session_checkpoints` e o histórico do EF. A API não executa migrations automaticamente.
- `dotnet-tools.json` fixa `dotnet-ef` 10.0.14; EF Core 10.0.14 e Npgsql provider 10.0.3 estão registrados nos lockfiles.

Para gerar/aplicar migrations manualmente, configure a connection string por ambiente, execute `dotnet tool restore` e use `dotnet ef database update --project src/EnglishRoulette.Infrastructure --configuration Release`. Para o fluxo local completo, consulte `LOCAL_DEMO.md` e `scripts/dev.py`.

## Testes

Configure `ENGLISHROULETTE_TEST_CONNECTION` para um banco de testes separado. Cada teste cria um schema com UUID próprio, aplica a migration e remove somente esse schema ao terminar. Sem essa variável, os testes PostgreSQL são marcados como **skipped**, não aprovados silenciosamente. A CI configura PostgreSQL e executa esses testes.

Cobertura: nova instância recupera pergunta e deadline; recibo resiste ao round-trip JSONB; timeout rejeitado continua salvo; pedidos duplicados simultâneos executam uma vez; respostas concorrentes aceitam apenas uma; revisão antiga não sobrescreve dados; credencial anterior é revogada; hash fora do formato falha na constraint real.

## Limites deste incremento

O catálogo editorial normalizado e Identity foram acrescentados pela migration `AdminIdentityAndCatalog`; veja [ADMIN.md](ADMIN.md). As tabelas analíticas de perguntas respondidas, decisões e recompensas de `DATABASE.md` continuam planejadas. A consistência atual de respostas é protegida pelo agregado e pela revisão atômica do checkpoint; ainda não existem índices relacionais por pergunta/jogador nem consultas analíticas desse histórico.

Prazos vencidos são aplicados no próximo snapshot/comando; ainda não há worker de varredura. Retenção, expiração no banco e idempotência da criação de aventura permanecem para a evolução. Nenhuma pontuação ou recompensa foi introduzida.

Referências: [concorrência no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [provider PostgreSQL do Npgsql](https://www.npgsql.org/efcore/).
