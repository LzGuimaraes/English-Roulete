# Histórias em português — incremento autorizado

O responsável solicitou um modo para ler e ouvir as histórias em português, com um botão para separar os idiomas e versões de todas as histórias já publicadas.

Critérios de aceite:

- Seletor Inglês / Português antes do início da aventura e da criação da sala.
- Catálogo, prévia e sorteio filtrados no servidor pelo idioma escolhido. Sem idioma explícito, preservar inglês para clientes e conteúdo antigos.
- O idioma é uma propriedade editorial explícita da versão, validada como `en` ou `pt-BR`; os registros antigos recebem `en` por migração aditiva.
- Cada versão portuguesa tem texto, sinopse, perguntas, explicações, escolhas e WAVs próprios. A história inglesa publicada permanece imutável. As ilustrações podem ser compartilhadas.
- Sala mantém um idioma único na criação, reconexão e partidas seguintes. Entrar por convite adota o idioma da sala. Pontuação, pódio, hints, prazos e autenticação seguem os fluxos atuais.
- Em português, a ajuda de vocabulário explica palavras em português e mostra o mesmo custo antes do clique. Não anunciar esses textos como tradução de inglês nem apresentar os níveis CEFR como certificação de português; a classificação original continua apenas como metadado editorial.
- Aventura solo continua sem retomada ao sair ou recarregar. Nenhuma tradução ou geração de voz acontece durante o jogo.
- Estados de carregamento, erro e catálogo vazio respeitam o idioma escolhido e permitem nova tentativa.
- Testes cobrem isolamento de idiomas, recusa de versão no idioma errado, compatibilidade do catálogo antigo, sala, reconexão, hints e pontuação. Conferência visual em desktop e celular.

DECISION NEEDED para evolução: oferecer tradução durante uma rodada inglesa e decidir seu eventual custo. Este incremento oferece catálogos separados antes da partida, conforme solicitado, sem introduzir essa mecânica.

## Implementação e validação

`StoryLanguage` valida os códigos de conteúdo no Domain. `CatalogMetadata` e `CatalogVersionRow` armazenam o idioma; a migração `StoryContentLanguage` acrescenta a coluna com padrão inglês e uma restrição para os dois idiomas suportados. O banco não precisa reescrever textos, versões ou mídias antigas. O `Down` remove a coluna e não deve ser usado para voltar o deploy após publicar traduções.

`StoryRoulette` filtra o idioma antes de sortear gênero e história. `DemoSessions` rejeita um ID de versão de outro idioma. `LobbyState` fixa o idioma da sala; `RoomEndpoints` usa esse estado autenticado ao sortear e `PrepareNext` o preserva. Checkpoints anteriores sem o campo continuam em inglês.

`LanguageSelector` reutiliza a paleta atual, com botões de estado explícito e navegação por teclado. `StoryLanguagePreference` guarda somente a escolha visual na memória da aplicação. Não guarda progresso nem credenciais e não muda a política de abandono do solo. Catálogo, prévia e componentes recebem o idioma declarado pelo servidor.

O painel administrativo informa o idioma do texto e do áudio e distingue tradução de significado nas dicas. O importador mantém a propriedade `language`. `produce_story_pack_audio.py` produz lotes offline com intervalos medidos, retoma arquivos por hashes e não participa do runtime.

Os testes `Language_*` em `AdminCatalogTests.Languages.cs` cobrem os dois catálogos, prévias, versão no idioma errado, definição paga, pontuação, idioma adotado por convidados e mantido após resultado e troca de anfitrião. `LobbyRoomTests` cobre checkpoints antigos. `tests/browser/languages.cjs` percorre solo e sala em português em 1366 e 390 px, além de troca de idioma, falha e catálogo vazio.

Validação manual: na página inicial, selecione Português, confira a prévia, inicie e toque em “Ouvir e iniciar cena”. Abra uma palavra sublinhada e responda após o áudio. Volte ao menu e selecione Inglês para conferir o outro catálogo. No multiplayer, crie uma sala em Português e entre com outro navegador: ambos devem receber a mesma história em português. As narrações precisam estar publicadas antes de uma história aparecer na roleta.

As descrições de imagem são específicas da versão (`SceneSupport.VisualDescription`), usando o JSON de apoio existente, com limite de 300 caracteres. `DemoSessions`, `RoomGames` e a prévia administrativa usam essa descrição e mantêm o texto do asset como fallback para conteúdo antigo. Assim, as traduções reutilizam a mesma imagem sem reescrever um asset inglês publicado.
