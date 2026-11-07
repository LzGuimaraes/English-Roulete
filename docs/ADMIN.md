# Login e catálogo editorial

O painel está em **http://localhost:5080/admin**. Esta etapa implementa login de administrador, troca de senha, histórias versionadas, cenas, uma pergunta de múltipla escolha por cena, caminhos, narração WAV, simulação, publicação e arquivamento. O jogo oferece as versões publicadas no catálogo PostgreSQL.

## Acesso local

```sh
python3 scripts/dev.py setup
python3 scripts/dev.py run
```

`setup` aplica as migrations, prepara o áudio de demonstração e provisiona a conta `ADMIN`. As credenciais ficam em **`infra/.env`**, nas chaves `ADMIN_EMAIL` e `ADMIN_PASSWORD`. Uma instalação nova gera a senha aleatoriamente; o arquivo é ignorado pelo Git. A senha nunca é enviada ao frontend como configuração.

Para atualizar um ambiente já preparado:

```sh
python3 scripts/dev.py migrate
python3 scripts/dev.py admin
python3 scripts/dev.py run
```

O provisionamento preserva a senha de uma conta existente. A troca normal é feita em **Minha conta**. Para recuperação local, configure uma senha forte em `ADMIN_PASSWORD` e execute `python3 scripts/dev.py admin-reset`. Esse comando altera a senha explicitamente; não existe endpoint público de cadastro ou recuperação.

O runner repassa as credenciais de bootstrap somente ao processo de provisionamento. A API normal usa o hash salvo pelo ASP.NET Core Identity no PostgreSQL. A história original é importada uma única vez, com o mesmo identificador de versão usado nas sessões anteriores.

## Fluxo de edição

1. Entre no painel e escolha **Nova história** ou abra uma versão existente.
2. Preencha título, sinopse, gênero, nível, autoria, categoria de direitos, evidência e créditos.
3. Adicione cenas com chaves únicas, texto em inglês, pergunta, alternativas, gabarito e explicação. Marque finais sem saídas; nas demais cenas, configure caminhos e seus destinos. Escolha a cena inicial.
4. Salve o rascunho. Para cada cena, informe créditos e evidência de direitos do áudio e envie sua narração.
5. Use **Simular percurso** para ouvir, testar alternativas e percorrer escolhas. A simulação usa o motor e o avaliador do servidor, sem cronômetro ou pontuação.
6. Escolha **Verificar publicação**, corrija os problemas e confirme revisão editorial e direitos. Depois publique.
7. Abra o jogo e selecione a história. Para alterar conteúdo publicado, use **Duplicar como rascunho**.

A publicação exige metadados completos, perguntas válidas, áudio íntegro correspondente ao texto salvo e um grafo em que todas as cenas sejam alcançáveis e levem a um final. A publicação V1 rejeita ciclos, conforme a restrição descrita em `DATABASE.md`; o motor de domínio continua independente dessa política editorial. O hash do texto identifica mudanças posteriores ao upload; a correspondência real entre voz e texto depende da revisão humana confirmada pelo administrador.

Uma versão aceita até **40 cenas**, **8 alternativas** e **8 caminhos por cena**. O upload aceita **WAV PCM de 16 bits**, até **10 MB** e **2 minutos**. O servidor verifica os bytes, calcula duração e SHA-256 e escolhe o nome do arquivo; não confia na extensão ou na duração informada pelo navegador. MP3, transcodificação e geração de voz no painel não fazem parte deste incremento.

## Versões e persistência

Conteúdo publicado é imutável. Uma nova publicação arquiva a edição anterior da mesma história dentro de uma transação; apenas uma versão por história fica disponível para novas aventuras. Arquivar retira a versão da seleção pública e mantém conteúdo, mídia já publicada e sessões existentes. Rascunhos arquivados continuam privados.

A revisão numérica detecta edições simultâneas. Uma aba desatualizada recebe HTTP 409 e precisa recarregar antes de salvar. O editor avisa antes de descartar alterações locais. A API registra criação, edição, duplicação, upload, publicação e arquivamento com administrador e horário UTC em `admin_audit`.

As tabelas `catalog_stories`, `catalog_versions`, `catalog_scenes`, `catalog_options`, `catalog_transitions` e `catalog_media` guardam o catálogo normalizado. A pergunta fica na própria cena neste primeiro modelo. Checkpoints de jogo continuam congelando a definição completa e os recibos de comandos em JSONB; o catálogo não substitui o armazenamento da sessão. Títulos e áudios da versão preservada alimentam sua apresentação.

Uploads ficam em `media/private/audio/`, fora de `wwwroot`. Antes de publicar, apenas administradores acessam `/api/admin/media/{id}`. A rota `/media/catalog/{id}` entrega somente assets já publicados e suporta HTTP Range. Depois de publicados, os arquivos permanecem disponíveis para sessões antigas. Arquivamento não revoga distribuição anterior nem remove bytes do disco. Retenção e limpeza de arquivos não referenciados são futuras tarefas.

## Autenticação e configuração

O servidor exige a role `ADMIN` em todas as rotas editoriais. O cookie de autenticação é HttpOnly, SameSite=Strict, limitado a `/api/admin` e expira em oito horas, sem renovação deslizante. Fora de Development, ele exige HTTPS. Mutação exige token antiforgery nativo em `X-CSRF-TOKEN`; o cliente busca um token atualizado antes de cada requisição. Não há senha ou token de autenticação em localStorage.

Cinco falhas bloqueiam a conta por 15 minutos; login tem limite de dez requisições por minuto por IP. Escritas editoriais têm limite por usuário. A senha exige pelo menos 12 caracteres, cinco caracteres distintos, maiúscula, minúscula, número e símbolo. Trocar ou recuperar a senha altera o security stamp; outras sessões são revalidadas em até cinco minutos.

| Configuração | Uso |
|---|---|
| `ConnectionStrings__Adventure` | Conexão PostgreSQL da API e migrations |
| `Media__Root` | Diretório persistente de mídia; padrão `media/` no checkout local |
| `DataProtection__KeysPath` | Chaves persistentes dos cookies; padrão `.local/data-protection/` |
| `Admin__BootstrapEmail` | E-mail, somente no comando de provisionamento |
| `Admin__BootstrapPassword` | Senha, somente no comando de provisionamento |

Preserve banco, mídia e chaves de proteção entre reinícios. O diretório das chaves recebe permissão 0700 no Linux e fica fora do Git. A implantação com proxy HTTPS, volumes e secrets será tratada na etapa de VPS/CI/CD; ela ainda não foi implementada.

## Organização do código e validação

| Componente | Papel |
|---|---|
| `AdminSecurity` | Identity, cookie, autorização, antiforgery e limites de requisições |
| `AdminBootstrap` | Provisionamento explícito e importação inicial da demonstração |
| `AdminEndpoints` / `AdminContracts` | HTTP protegido e mapeamento de DTOs editoriais explícitos |
| `CatalogRules` | Validação editorial e construção da história usada pelo motor |
| `IAdminCatalog` / `CatalogService` | Casos de uso e transações de persistência/versionamento |
| `CatalogMediaStorage` | Upload, vínculo com o texto, integridade e caminho de arquivos |
| `AdminClient` | Requisições do painel, antiforgery e mensagens de erro |
| `Admin`, `StoryEditor`, `SceneEditor` | Acesso, catálogo, edição e simulação em Blazor |

A nova dependência de runtime é o pacote oficial `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, usando as tabelas Identity no mesmo contexto PostgreSQL. `Microsoft.AspNetCore.Mvc.Testing` é usado apenas nos testes HTTP. Domain continua sem dependências de autenticação ou persistência. Contracts contém DTOs editoriais com gabaritos, retornados exclusivamente pelas rotas ADMIN; os DTOs de jogo continuam sem gabarito.

Execute as verificações do README e `python3 scripts/dev.py test`. Os testes HTTP usam schemas PostgreSQL e diretórios temporários isolados. Cobrem acesso anônimo e sem role, CSRF, bloqueio/rate limit, troca de senha/logout, edição concorrente, entrada malformada, revisão, áudio privado, Range, publicação, imutabilidade, substituição e retomada após arquivamento.

Para verificar a UI, siga o fluxo de edição em desktop e celular; confirme também que sair com alterações pendentes exige uma decisão e que logout impede chamadas editoriais. Pontuação, contas de jogadores, multiplayer, gerenciamento de administradores, MFA, recuperação por e-mail, imagens, tags, eventos e relatórios de uso permanecem fora desta etapa. Gênero é um campo livre e os níveis são uma lista fixa nesta versão.
