# Orientações do projeto

- Leia `docs/REQUIREMENTS.md`, `docs/ARCHITECTURE.md` e `docs/CODING_STANDARDS.md` antes de implementar.
- Trabalhe em incrementos pequenos, com critérios de aceite e testes de comportamento.
- Use C# / .NET 10, nullable habilitado e build sem warnings.
- Mantenha Domain independente de HTTP, EF Core, SignalR, Blazor e arquivos.
- Acrescente os demais projetos somente quando o incremento precisar deles.
- Não exponha entidades de domínio ou gabaritos em contratos públicos.
- Use TimeProvider para regras dependentes do relógio; mantenha horários em UTC.
- Nenhuma resposta correta, pontuação ou prazo é decidido pelo cliente.
- Documente ambiguidades de produto como `DECISION NEEDED`; não transforme propostas em regras definitivas silenciosamente.
- Não versione secrets, mídias grandes, binários ou resultados de testes.
- Antes de commitar: restore com `--locked-mode`, build Release, testes e `dotnet format --verify-no-changes`.
- Explique brevemente o papel das classes e como validar o incremento, conforme os padrões de aprendizado.
- Use commits pequenos com prefixos `feat:`, `fix:`, `test:`, `docs:` ou `chore:`.
