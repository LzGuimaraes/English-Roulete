# Demonstração local com narração e PostgreSQL

A prévia conecta o motor solo a uma interface Blazor WebAssembly, arquivos de narração e checkpoints PostgreSQL. É um incremento jogável de CA-000; pontuação, XP e catálogo editorial completo continuam pendentes.

## Preparar e executar

Requer Linux com SDK .NET 10, Docker Compose, Python 3, `libespeak-ng1` e os dados de voz `espeak-ng-data`. A biblioteca de voz é usada somente na preparação, não pela API durante as partidas. O primeiro restore precisa acessar o NuGet.

```sh
python3 scripts/dev.py setup
python3 scripts/dev.py run
```

Abra **http://localhost:5080**. A API e o frontend usam a mesma origem. O perfil `http` configura Development e escuta somente no computador local. Ctrl+C encerra a API; o banco e o progresso continuam no volume do Docker.

`setup` gera `infra/.env` com uma senha aleatória (sem imprimi-la), sobe somente o container `englishroulette-postgres-1`, cria um banco separado para testes, restaura a ferramenta EF, aplica a migration inicial e prepara a narração. Não acessa o PostgreSQL do sistema. Para selecionar um SDK fora do PATH, use `DOTNET_HOST_PATH=/caminho/para/dotnet`.

Comandos adicionais:

```sh
python3 scripts/dev.py db       # Iniciar o PostgreSQL local e preparar o banco de testes
python3 scripts/dev.py migrate  # Aplicar migrations explicitamente
python3 scripts/dev.py test     # Incluir os testes com banco real
python3 scripts/dev.py run --no-build  # Executar o build Release já existente
```

## Jogar e testar retomada

1. Clique em **Começar minha aventura** e aguarde o aviso **Áudio pronto**.
2. Escolha **Ouvir e iniciar cena**. O clique libera a reprodução no navegador; o servidor define o prazo pela duração do arquivo mais 2 segundos de margem.
3. Ouça em inglês e acompanhe o texto. A pergunta abre após essa janela, sem depender de `audio.ended` enviado pelo cliente.
4. Responda em até 20 segundos e confira a explicação. Na livraria, escolha café ou biblioteca; cada percurso tem três cenas.
5. Recarregue a página durante uma cena: o progresso retorna. Em uma narração retomada, pressione play para continuar o áudio a partir da posição correspondente ao relógio do servidor.
6. Pare e execute novamente a API, mantendo o navegador. A mesma aventura deve voltar. Uma pergunta vencida retorna como timeout, sem reiniciar os 20 segundos.

Pausar o áudio não pausa o cronômetro de Adventure. O player interrompe a reprodução ao sair da fase de narração. Falha no arquivo mantém o início da cena bloqueado e oferece **Tentar carregar áudio novamente**.

## Narrativas, arquivos e integridade

A história original está em `Api/Demo/story.json`, como recurso **somente do servidor**, fora de wwwroot. Esse arquivo inclui gabaritos e não deve ser copiado para o cliente. A edição narrada usa outro StoryVersionId; sessões persistidas congelam sua definição completa.

`python3 scripts/generate_demo_audio.py` prepara WAVs offline com eSpeak NG en-us. São vozes sintéticas de demonstração, sem geração durante o jogo, serviços de IA ou dependências JavaScript externas. Arquivos e `media/manifest.json` ficam fora do Git, em diretório dedicado. A geração verifica assets existentes e não sobrescreve uma narração publicada com conteúdo diferente.

A API valida hash SHA-256, tamanho e estrutura PCM WAV antes de disponibilizar os arquivos. A duração vem da quantidade de frames; não é um número estimado pelo frontend. Os URLs contêm o hash do arquivo e recebem cache imutável. A entrega suporta HTTP Range; hashes ausentes retornam 404, sem expor diretórios ou rascunhos.

O caminho padrão é `media/` na raiz do checkout. É possível configurar `Media__Root` para um volume externo contendo o mesmo manifesto e subdiretório `audio`. Na evolução editorial, narrações revisadas e metadados completos substituirão a preparação de desenvolvimento.

## Código

| Componente | Responsabilidade |
|---|---|
| Domain | Regras, captura/restauração do estado e definição congelada da história |
| Application | Porta de armazenamento e coordenação com retry por revisão |
| Infrastructure | EF Core/Npgsql, migration, JSONB e validação de WAV |
| Api/Demo | Conteúdo, token opaco, mídia e mapeamento para DTOs explícitos |
| Contracts | Dados públicos sem gabaritos ou checkpoints |
| Web/NarrationPlayer | Player HTML nativo, pré-carregamento e recuperação de falhas |
| Web/audio-player.js | Interop localizado para play, pause e posição do áudio |

O navegador recebe apenas Web e Contracts. Domain, Infrastructure, gabaritos e credenciais de banco permanecem no servidor. O relógio do cliente apenas desenha a contagem; ele não determina aceitação, estado ou pontos.

## HTTP

| Método | Rota | Comportamento |
|---|---|---|
| GET | `/health/live` | Host ativo |
| GET | `/health/ready` | PostgreSQL acessível e migrations aplicadas |
| GET | `/api/demo/session` | Snapshot da aventura associada ao cookie |
| POST | `/api/demo/session` | Nova aventura, revogando a credencial anterior |
| POST | `/api/demo/commands` | `start`, `answer`, `continue` ou `choose` |
| GET | `/media/audio/{hash}.wav` | Apenas áudio do manifesto publicado, com Range |

Os endpoints de demonstração e mídia ficam habilitados somente em Development. Comandos incluem RequestId e ExpectedStateVersion; respostas também citam a instância da pergunta. Regra violada retorna ProblemDetails/409; entrada inválida, 400; mídia indisponível, 503.

O cookie aleatório é HttpOnly, SameSite=Strict e restrito a `/api/demo`, com duração de 30 dias. Somente o hash da credencial fica no banco. O cookie é Secure sob HTTPS; a prévia local usa HTTP em loopback. POST exige o cabeçalho customizado `X-English-Roulette: demo`; não há CORS permissivo. Snapshots usam `Cache-Control: no-store`.

Preserve o cookie, o volume PostgreSQL e a pasta de mídia para retomar. Abas do mesmo navegador compartilham a aventura; outro navegador recebe uma identidade separada. Recomeçar substitui a aventura anterior desse navegador, como na prévia original.

## Verificações e limites

Veja os comandos no README e o desenho transacional em [PERSISTENCE.md](PERSISTENCE.md). Os testes PostgreSQL usam schemas isolados e validam concorrência, restauração e constraints reais. Os testes de WAV exercitam duração e rejeição de arquivos inválidos.

Para QA visual, percorra os dois finais, experimente uma resposta errada e um timeout, confira o cronômetro no celular e teste retomada após reiniciar a API. Bloquear a URL de áudio nas ferramentas do navegador deve exibir recuperação de mídia e impedir o início até o carregamento funcionar.

Ainda faltam catálogo/painel editorial, autenticação definitiva, retenção, worker de prazos, tabelas analíticas, pontuação aprovada e multiplayer. A solução atual persiste snapshots completos; não apresenta o modelo relacional planejado como se já estivesse implementado.

Referências: [arquivos estáticos do Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files), [API de síntese offline do eSpeak NG](https://github.com/espeak-ng/espeak-ng/blob/master/src/include/espeak-ng/speak_lib.h).
