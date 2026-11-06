# English Roulette — documentação técnica

Especificação inicial para revisão antes da implementação. **Nenhum dos arquivos descreve um sistema já implementado.** Regras ainda propostas estão marcadas para validação.

## Ordem de leitura

1. **`REQUIREMENTS.md`** — o que o produto faz, modos solo e multiplayer, conteúdo, mídia e critérios de aceite.
2. **`ARCHITECTURE.md`** — monólito modular, projetos .NET, Game Engine compartilhado, VPS e storage.
3. **`DATABASE.md`** — entidades e relacionamentos PostgreSQL, restrições, índices, concorrência e migrations.
4. **`GAME_RULES.md`** — máquina de estados, engine, cronômetros, pontuação, votos, XP e testes.
5. **`CODING_STANDARDS.md`** — padrões para escrever C# com qualidade e usar IA sem perder aprendizado.

## Primeiro incremento de implementação

**Story → Scene → Narration → Question → Answer → Next Scene** no modo Solo Adventure.

A engine começa como **classes C# testáveis** na solução, não como programa separado ou SDK externo. Depois de existir uma aventura solo funcional, a mesma lógica de cena/pergunta será aproveitada no multiplayer com SignalR.

## Arquivos de implementação futuros

`API.md` (contratos REST/SignalR) e `DEPLOY.md` (passo a passo da VPS e recuperação) podem ser detalhados quando o primeiro fluxo vertical estiver pronto. Não são necessários para iniciar o domínio.
