# Chat, tempo de resposta e tradução pós-resposta

## Critérios de aceite

- O anfitrião escolhe no lobby multiplayer um tempo de pergunta entre 10 e 60 segundos; a escolha fica congelada quando a partida começa.
- No solo, a pessoa escolhe o mesmo intervalo antes de iniciar a aventura. O valor é enviado ao servidor e fica gravado nos timings do checkpoint.
- O servidor abre/fecha perguntas e rejeita respostas fora do deadline; o contador do navegador é apenas uma representação do prazo oficial.
- Jogadores da mesma sala podem enviar mensagens de até 300 caracteres durante o lobby e a partida. O servidor identifica o autor, limita o histórico às 100 mais recentes e notifica a sala após persistir.
- Depois de uma resposta correta, errada ou expirada, o resultado mostra a explicação e a tradução completa editorial da cena antes de liberar o avanço.
- Traduções não cadastradas não são inventadas pelo cliente: o resultado informa que o conteúdo editorial ainda não está disponível.

## Decisões técnicas

`QuestionTiming` concentra a faixa válida (10–60 s), com padrão de 20 s no solo e 15 s no multiplayer. `LobbyState.QuestionSeconds` e o `SceneTiming.QuestionDuration` são duráveis no snapshot, então reconexões não reiniciam o relógio.

O chat fica no snapshot JSON da sala porque já existe controle transacional por sala e o frontend reconstrói a tela a partir do snapshot. Cada envio usa `RequestId` para retry idempotente; a mensagem é projetada em `RoomView` sem expor estado interno.

`SceneSupport.FullTranslation` é conteúdo editorial opcional, persistido no JSON de suporte da cena. Ele só aparece no `AnswerView` quando a fase é `QuestionReveal` ou `StoryResult`; não há resposta correta, explicação ou tradução no contrato de pergunta aberta.

## DECISION NEEDED

- Confirmar se toda cena em inglês deverá ser bloqueada na publicação quando não tiver tradução completa. Este incremento mantém o campo opcional para não invalidar histórias já publicadas; a interface informa a ausência.
- Confirmar se o chat deve ser mantido para a revanche ou limpo ao preparar a próxima história. Atualmente ele permanece na sala, como histórico curto da sessão.

## Como validar

1. No solo, selecione 10 s e confirme que o snapshot abre a pergunta com esse prazo; responda após o limite e verifique o timeout.
2. No multiplayer, escolha 30 s como anfitrião, confirme que o outro jogador vê a configuração, inicie e verifique que a pergunta recebe 30 s.
3. Envie uma mensagem em uma janela e confirme a chegada na outra sem recarregar; reenvio do mesmo `RequestId` não duplica a mensagem.
4. Responda certo e errado e confirme que a tradução aparece antes do botão de continuar.
