# Produção offline de mídia — Antologia 01

O lote contém 12 histórias, 60 cenas, 60 narrações WAV, 60 artes 2D dark e 60 loops de câmera discretos. A produção foi autorizada pelo responsável, inclusive envio e publicação direta na VPS. Os arquivos ficam em `media/anthology-01/`, ignorados pelo Git; o banco guarda as referências às cópias do volume da VPS. Os textos, prompts e comprovantes sem credenciais ficam em `content/anthology-01/`.

## Responsabilidades

- `produce_anthology_audio.py`: usa Piper offline, copia exatamente `audioText`/`narration`, aplica ritmo por nível editorial e grava WAV PCM mono de 16 bits. Registra hashes do texto, modelo e áudio, duração e medidas de silêncio/clipping. Retoma arquivos íntegros sem regenerar.
- A ferramenta embutida `image_gen` produziu uma arte por cena a partir de `productionNotes.imagePrompt`. As imagens selecionadas são `<chave>-dark.png`; variantes anteriores ficam separadas. Revisão visual corrigiu pessoas extras, pistas e paisagens incompatíveis com estações espaciais. Nenhum serviço de geração foi incorporado ao jogo.
- `encode_anthology_visuals.py`: usa FFmpeg para derivar um GIF de cada arte aprovada. Move apenas o enquadramento, em dois pixels, com retorno ao início. Mantém 640×360, 48 quadros e seis segundos. Limita entrada e tempo de processo, usa paleta estável e compressão de diferenças entre quadros. O comprovante vincula o GIF ao hash da arte.
- `verify_anthology_narration.py`: transcreve os WAVs localmente com Whisper, comparando o resultado ao roteiro. Decodifica com FFmpeg para evitar diferenças entre APIs de versões de PyAV. A comparação é uma ferramenta de revisão: não substitui automaticamente nomes ou texto para satisfazer o reconhecedor.
- `upload_anthology_media.py`: usa a API ADMIN existente com cookies e CSRF. Confere versão, metadados e cenas, envia somente arquivos diferentes e relê os bytes para comparar SHA-256. Atualiza os mesmos IDs, preserva a revisão otimista e não altera versões publicadas. Uma resposta perdida interrompe a execução; a retomada compara o arquivo remoto antes de repetir o upload.

## Arquivos e validação

Cada pasta de mídia contém `audio/`, `images/`, `animations/` e `media.json`, com créditos e evidências reais. `audio-production.json` identifica a voz `en_US-ljspeech-high`, o motor Piper 1.8.0 e os hashes. `licenses/` preserva o cartão da voz, metadados do repositório e a licença do motor. O [cartão do modelo](https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/ljspeech/high/MODEL_CARD) identifica o dataset [LJ Speech](https://keithito.com/LJ-Speech-Dataset/) como domínio público; os metadados do repositório declaram MIT. O motor Piper é uma ferramenta de produção GPL-3.0 e não é distribuído no container do jogo.

As artes seguem animação 2D cinematográfica dark, com adultos desenhados, sombras profundas, cenários pintados e pistas legíveis. Os horários, objetos e estados de cada final são preservados. Os loops não inventam fala, ações de personagens, monstros, efeitos de gameplay ou objetos novos.

Os 60 WAVs e 60 GIFs passaram pelos parsers `WavAudio` e `ImageInspection` da aplicação. Também foram conferidos 48 quadros, seis segundos e igualdade do primeiro/último enquadramento de cada GIF. As 60 transcrições automáticas foram comparadas aos roteiros; diferenças de nomes, variantes ortográficas e reconhecimento foram inspecionadas. Não há declaração de revisão auditiva humana ou certificação CEFR.

## Reproduzir e retomar

```sh
python3 -m venv .local/media-tools/venv
.local/media-tools/venv/bin/pip install -r scripts/media-requirements.txt
.local/media-tools/venv/bin/python -m piper.download_voices \
  --data-dir .local/media-tools/piper en_US-ljspeech-high
.local/media-tools/venv/bin/python scripts/produce_anthology_audio.py \
  --model .local/media-tools/piper/en_US-ljspeech-high.onnx
```

Gere as artes com a ferramenta `image_gen`, uma chamada por cena, usando os prompts editoriais e inspecionando cada resultado. Salve as selecionadas em `media/anthology-01/<história>/images/<chave>-dark.png`. A geração de imagens não é automatizada por uma API adicional nem exige incluir credenciais no projeto.

```sh
# Substitua FFMPEG pelo caminho retornado pelo comando anterior.
.local/media-tools/venv/bin/python -c 'import imageio_ffmpeg; print(imageio_ffmpeg.get_ffmpeg_exe())'
.local/media-tools/venv/bin/python scripts/encode_anthology_visuals.py --ffmpeg FFMPEG
.local/media-tools/venv/bin/python scripts/verify_anthology_narration.py
python3 scripts/upload_anthology_media.py --kind all --email SEU_EMAIL_ADMIN
```

A senha é solicitada de forma protegida ou lida de `ENGLISHROULETTE_ADMIN_PASSWORD`. `--env-file` pode ler `ADMIN_PASSWORD` de um arquivo local ignorado; nenhuma senha é gravada nos comprovantes. O endereço e os IDs vêm de `vps-import.json`. `--story` restringe a operação a uma história. Sem `--publish`, a ferramenta apenas envia e confere mídias.

`--publish` exige as duas mídias de todas as cenas, validação oficial sem pendências e um `media/anthology-01/review.json` que vincule a revisão ao hash exato do JSON e de cada mídia. Esse arquivo de produção local registra os métodos e a data da revisão; uma alteração invalida os hashes e exige nova conferência. O servidor exige ainda revisão e direitos confirmados. Não remova as validações para publicar um pacote incompleto.

Os comprovantes versionáveis são `vps-import.json` e `vps-media.json`: contêm estado, IDs, hashes e verificação UTC, sem binários, senhas ou cookies. Depois de publicada, uma história permanece imutável. Para mudar texto ou mídia, crie uma nova versão pelo painel e repita a revisão.
