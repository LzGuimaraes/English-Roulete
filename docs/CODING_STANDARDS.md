# English Roulette — Padrões de Codificação e Desenvolvimento

> **Versão:** 0.1 — proposta para validação | **Stack:** C# / .NET 10, ASP.NET Core, Blazor WASM, EF Core + PostgreSQL  
> Aplicável ao código produzido manualmente **e** com auxílio de IA. Vinculado a `REQUIREMENTS.md` v1.3 e `ARCHITECTURE.md`.

## 1. Objetivos

- Código legível e previsível, com regras de negócio fáceis de testar.
- **Aprender C#**, não apenas aceitar grandes blocos gerados por IA.
- Crescer como **monólito modular**, sem abstrações antecipadas e sem acoplamento entre UI e persistência.
- Evitar duplicação entre solo e multiplayer; compartilhar catálogo, motor de narrativa, validação de questões e publicação.

## 2. Regras obrigatórias de C#

- `Nullable` habilitado e warnings corrigidos antes de merge.
- Convenções: **PascalCase** para tipos/métodos/propriedades; **camelCase** para parâmetros/variáveis; `_camelCase` para campos privados.
- Usar `async/await` para I/O, com `CancellationToken` em métodos assíncronos de serviços/repositórios sempre que relevante.
- Classes/métodos com responsabilidade clara; preferir composição a herança desnecessária.
- `enum` para estados/modos restritos; `record` para DTOs e valores imutáveis onde isso melhorar a leitura.
- Evitar `dynamic`, null-forgiving (`!`) indiscriminado, `catch(Exception)` silencioso e `Task.Result`/`.Wait()`.
- Não usar `DateTime.Now` para deadlines e persistência: usar UTC e uma abstração de tempo (`TimeProvider` em serviços testáveis).
- Evitar números mágicos: configurações nomeadas, com validação no startup.
- `EditorConfig` / `dotnet format` para consistência de estilo; formatar antes de PR.

## 3. Fronteiras arquiteturais

- `Domain`: entidades, invariantes, regras, enums e funções puras. **Sem EF Core, SignalR, controllers ou acesso a arquivos.**
- `Application`: casos de uso, políticas por modo, DTOs internos, validações transacionais e interfaces mínimas para infraestrutura.
- `Infrastructure`: EF Core, migrations, PostgreSQL, implementações de storage, consultas e integrações externas.
- `Api`: REST, autenticação, autorização, SignalR, DI e tratamento HTTP de erros. **Sem regras de pontuação dentro de controllers/hubs.**
- `Web`: componentes Blazor, rotas, formulários, clientes tipados HTTP/SignalR e estado de UI. **Não conter gabaritos ocultos nem regras autoritativas.**
- `Contracts`: contratos públicos. Não retornar entidades rastreadas do EF ou campos administrativos sensíveis.

## 4. Fluxo de implementação

Para cada funcionalidade:

1. Especificar **regra e critérios de aceite** (referenciar RF/CA dos requisitos).
2. Desenhar dados envolvidos e estados possíveis.
3. Escrever ou ajustar testes do domínio.
4. Implementar regra em `Domain`/`Application`.
5. Acrescentar persistência/API apenas se necessário.
6. Integrar tela Blazor.
7. Testar caso feliz, erro, concorrência/retry quando relevante.
8. Revisar diff e fazer commit pequeno.

Implementar **vertical slices**, por exemplo: `Criar história → anexar áudio → publicar → exibir e reproduzir` antes de começar dezenas de CRUDs separados.

## 5. API, validação e erros

- Usar rotas REST previsíveis, métodos HTTP corretos e DTOs explícitos; documentar via OpenAPI.
- Entrada sempre validada no backend (intervalos, IDs, relacionamento, publicação, tamanho/arquivo, permissões).
- Retornar erros padronizados (`ProblemDetails`) sem stack trace ou segredo em produção.
- Respostas de comandos suscetíveis a retry precisam ser **idempotentes**, especialmente envio de resposta, escolha narrativa, pontuação e XP.
- Não retornar gabarito ou explicação antes do fechamento da pergunta quando estiver em modo competitivo.
- Toda operação deve confirmar identidade/permissão; `roomCode` não autentica um jogador.
- Login administrativo e upload com rate limiting apropriado.

## 6. EF Core e PostgreSQL

- Migrations versionadas; não usar `EnsureCreated` em produção.
- Não usar migrations destrutivas automáticas em deploy.
- Proibir `SELECT *` ou consultas sem paginação em telas administrativas grandes.
- Evitar consultas N+1 e rastreamento desnecessário (ex.: `AsNoTracking()` para leitura).
- Índices para consultas frequentes; restrições únicas para bloquear respostas/votos duplicados.
- Transações explícitas para mudança de fase + pontos/XP quando necessário; considerar controle otimista de concorrência.
- Timestamps importantes em UTC. `GameSession` aponta para versão imutável de história.
- Arquivos de áudio/imagem ficam **fora do banco**, vinculados por `MediaAsset.Id`.
- Revisar SQL e plano de execução antes de otimizações especulativas.

## 7. Regras de jogo e testes

- Nenhuma pontuação, resposta correta ou sorteio é decidido pelo cliente.
- Estados com transições explícitas; comandos incompatíveis com a fase são rejeitados.
- `SOLO_ADVENTURE`: acerto e faixas de tempo; `SOLO_PRACTICE`: prática sem velocidade; `MULTIPLAYER`: ordem dos acertos entre participantes.
- A avaliação de questão e o motor de cenas são **compartilhados**; diferenças ficam em políticas pequenas e testáveis.
- Usar semente/injeção de aleatoriedade em testes para sorteios determinísticos.
- Casos mínimos de testes: empate, prazo expirado, alternativa inválida, replay, reconexão, resposta duplicada, decisão sem caminho, fallback de conteúdo, XP duplicado, mídia ausente, arquivo não autorizado.
- Criar testes de integração com PostgreSQL real isolado (container em testes/CI quando disponível), não depender só de mocks do DbContext.

## 8. Frontend e UX

- Componentes Blazor pequenos por comportamento; evitar um único `.razor` gigante com toda a partida.
- UI consome snapshots/campos públicos do backend, sem manter segunda fonte de verdade para a partida.
- Em solo Practice, controles de áudio e texto livres; em solo Adventure e multiplayer, respeitar política de fase.
- Carregar áudio previamente, tratar autoplay bloqueado e exibir erro recuperável caso mídia falhe.
- Evitar animações que atrapalhem o entendimento; respeitar `prefers-reduced-motion`, acessibilidade por teclado e touch.
- Para mídia, usar `audio` nativo e JavaScript interop localizado apenas quando necessário; **não enviar bytes pelo SignalR**.

## 9. Segurança e privacidade

- Nunca guardar secrets em Git, logs, URL, frontend ou `appsettings.json` versionado.
- Validar upload por conteúdo real e gerar nomes no servidor; armazenar em volume dedicado sem execução de arquivos.
- Separar mídia privada/rascunho da publicada; respeitar licenças e autorizações por versão.
- Proteção contra XSS em textos/editor; rate limit para login, jogo e uploads.
- Se usar cookies, observar `HttpOnly`, `Secure`, `SameSite` e proteção CSRF adequada.
- Não expor banco na rede pública; TLS e backups fora da VPS.

## 10. Commits, PR e revisão

Padrão de commits sugerido: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.

Um PR ou incremento deve incluir:

- Problema e RF/critério de aceite atendido.
- Alteração proposta e decisão técnica breve.
- Migração e impacto no banco, se houver.
- Evidência de testes e caso de falha relevante.
- Impacto em segurança/mídia quando aplicável.
- Screenshot de UI apenas quando necessário.

## 11. Instruções para assistência de IA

Ao solicitar implementação ao agente:

1. Ler `REQUIREMENTS.md`, `ARCHITECTURE.md` e este documento antes de editar.
2. Limitar a tarefa a um incremento pequeno; listar arquivos que serão alterados.
3. Explicar resumidamente **o papel de cada classe, o fluxo e a regra C# aplicada**.
4. Preferir APIs nativas do .NET; justificar novas dependências.
5. Não criar interfaces, repositórios genéricos ou novos projetos sem necessidade observada.
6. Não executar migrations destrutivas nem mudanças infra sem instrução específica.
7. Adicionar testes do comportamento implementado.
8. Apresentar como validar manualmente e quais comandos executar.
9. Quando houver ambiguidade de regra de negócio, marcar `DECISION NEEDED` em vez de inventar comportamento irreversível.
10. Não adicionar IA generativa ao runtime do jogo.

## 12. Definition of Done

- [ ] Critério de aceite atendido, inclusive erros previsíveis.
- [ ] Código compila sem novos warnings relevantes.
- [ ] Testes aplicáveis passam.
- [ ] Migrations/requisitos de ambiente documentados, quando houver.
- [ ] Backend valida segurança e consistência; cliente não decide resultados.
- [ ] Alteração foi verificada no navegador/celular quando atinge UI.
- [ ] Nenhum segredo ou mídia grande entrou no Git.
- [ ] É possível explicar o código sem depender da IA.

**Nota:** este documento orienta o processo; `GAME_RULES.md` deve fixar fórmulas de pontos, estado e prazos antes de implementar scoring final.
