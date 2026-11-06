# Demonstração local no navegador

Este incremento permite experimentar a sessão existente por uma interface Blazor WebAssembly. Atende à solicitação de uma prévia jogável; não representa o aceite completo de Solo Adventure (CA-000).

## Executar

Requer SDK .NET 10 e acesso ao NuGet no primeiro restore.

```sh
dotnet run --project src/EnglishRoulette.Api --configuration Release --launch-profile http
```

Abra http://localhost:5080. O perfil `http` usa `Development` e escuta somente no computador local. API e frontend são servidos na mesma origem, sem configurar CORS. Para encerrar no terminal, use Ctrl+C.

## Experimentar o fluxo

1. Clique em **Começar minha aventura**, depois em **Ler esta cena**.
2. Leia o texto em inglês. A janela de leitura dura 22 segundos; a pergunta abre automaticamente.
3. Responda em até 20 segundos. O servidor avalia a alternativa e entrega o feedback.
4. Continue até a livraria e escolha café ou biblioteca. Cada percurso possui três cenas.
5. Responda à última pergunta e veja o desfecho. Recomece para descobrir o outro caminho.
6. Recarregue a página durante uma pergunta: o prazo continua contando. Se expirar, é possível continuar sem uma nova tentativa.

A história demonstrativa foi escrita para esta prévia. Não há áudio nem recompensa/XP. As durações são configurações da demonstração e não homologam as tabelas de pontuação propostas.

## Código e dependências

| Projeto | Responsabilidade |
|---|---|
| Contracts | Records de entrada/saída sem entidades, gabarito ou IDs de outros jogadores |
| Api/Demo | História, catálogo mínimo e armazenamento em memória; mapeia snapshots para DTOs explícitos |
| Api/Program.cs | Rotas, cookie e proteção de requisições mutáveis; somente habilita a API demo em Development |
| Web/Services | Cliente HTTP tipado; consulta o servidor para transições e conflitos |
| Web/Pages e Components | Tela inicial, cena, alternativas, feedback e finais |

O navegador referencia somente Web e Contracts. As avaliações continuam em Domain/Application no servidor. Não há segunda implementação de regras no frontend. O relógio local apenas desenha a contagem; a aceitação da resposta depende dos prazos UTC do servidor.

Foram adicionados os pacotes oficiais Microsoft.AspNetCore.Components.WebAssembly, DevServer e WebAssembly.Server, na versão 10.0.14 fornecida pelo template do SDK instalado. Eles executam e hospedam o cliente WebAssembly; os arquivos `packages.lock.json` registram a resolução. Não há dependências JavaScript de runtime, fontes remotas ou serviços de IA.

Referência de hospedagem: [documentação oficial do Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0).

## HTTP e isolamento

| Método | Rota | Comportamento |
|---|---|---|
| GET | `/health/live` | Disponibilidade do host |
| GET | `/api/demo/session` | Snapshot da sessão associada ao cookie; 404 se não houver |
| POST | `/api/demo/session` | Cria uma aventura e substitui a anterior desse navegador |
| POST | `/api/demo/commands` | Executa `start`, `answer`, `continue` ou `choose` |

Comandos enviam `RequestId` e `ExpectedStateVersion`; respostas citam `QuestionInstanceId` e `OptionKey`; escolhas citam `TransitionKey`. Identidade, horário e gabarito não são recebidos do cliente. Violações de regra retornam ProblemDetails (409); entrada inválida retorna 400. O cliente recupera o snapshot ao encontrar conflito.

O cookie contém um token aleatório opaco, é HttpOnly e SameSite=Strict e está restrito a `/api/demo`. É Secure quando a conexão é HTTPS; nesta prévia o perfil usa HTTP em loopback. Requisições POST exigem `X-English-Roulette: demo`, um cabeçalho customizado da mesma origem. Não há CORS permissivo. Snapshots recebem `Cache-Control: no-store`.

Sessões e recibos existem somente na memória do processo. Dois navegadores possuem identidades separadas; abas do mesmo navegador compartilham a aventura. Reiniciar o host perde o progresso. Este armazenamento não substitui autenticação de produção, retenção, rate limits e persistência PostgreSQL; os endpoints de demonstração não são habilitados em Production.

## Verificar

Os comandos de build, testes e formatação continuam no README. Para validação visual, execute o percurso acima no desktop e com viewport de celular, confira acesso por teclado e confirme que o cronômetro continua visível durante a pergunta. Teste também uma alternativa errada e um prazo expirado.
