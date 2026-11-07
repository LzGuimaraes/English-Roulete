# The Lighthouse beyond Earth — B1

**Gênero:** Ficção científica · **Estado:** publicado na VPS, com narração e animação 2D dark

Em uma estação lunar, Anik encontra um sinal que parece escutar e escolhe como responder antes de automatizar o posto.

**Foco:** Inferência apoiada em resposta observável, limites da evidência e escolha de comunicação. Tecnologia especulativa, sem exigir conhecimento científico externo.

**Percursos:**

- outpost-shift → an-answering-pause → choosing-a-language → spoken-reply
- outpost-shift → an-answering-pause → choosing-a-language → pulse-reply

Cada percurso visita quatro cenas e tem um final próprio. Os dois finais são alternativas, não acontecimentos consecutivos.

Os gabaritos abaixo são material editorial local. Este arquivo não deve ser copiado para `wwwroot` nem exposto como contrato público.

## A Signal before the Change (`outpost-shift`)

Anik was preparing a lunar communications outpost for automatic operation. Soon, recorded messages would replace his night shifts. While checking the signal desk, he noticed a yellow indicator pulsing three times, followed by a pause. The pattern did not match the station's usual messages. He listened through the headphones, with grey ground and black sky beyond the window. Before handing the room over to a repeating recording, he wanted to know whether something was already trying to be heard.

**Pergunta:** Why did Anik investigate before switching to automatic operation?

- **A.** The yellow indicator proved that a visitor was standing outside
- **B.** The station's usual recordings had already explained the pattern
- **C.** The unfamiliar pattern might be an incoming attempt to communicate

**Gabarito editorial:** C
**Explicação:** The unfamiliar pattern might be an incoming message. Anik wants to investigate before filling the channel with recordings.

**Pista e continuidade:** O sinal é diferente do habitual, mas ainda não prova quem ou o que o produziu.

**Próxima etapa:**

- Continue the story → `an-answering-pause`

## A Pause That Changed (`an-answering-pause`)

Anik sent the station's ordinary test tone. The three-beat pattern stopped. When he ended the tone, it returned, but the pause was now longer. He checked the recording twice. A loose connection might repeat a noise, yet this change seemed related to what he had just sent. He wrote down that observation without naming its source. If the response was deliberate, a channel filled with automatic messages might prevent them from hearing each other.

**Pergunta:** What made Anik consider the possibility of a deliberate response?

- **A.** The pattern changed after he sent the test tone
- **B.** The recording announced the name of its sender
- **C.** The window showed someone holding a transmitter

**Gabarito editorial:** A
**Explicação:** The pause changes after Anik sends a test tone. This suggests a response without proving its source.

**Pista e continuidade:** A mudança após o teste sustenta uma hipótese, não uma conclusão infalível sobre origem extraterrestre.

**Próxima etapa:**

- Continue the story → `choosing-a-language`

## A First Reply (`choosing-a-language`)

The desk recorder could send either a short spoken message or the same three beats. A voice would show that someone was listening, though the receiver might not understand the words. Repeating the pattern would use a form the unknown source already seemed to recognise. Anik could record either reply and preserve the result for the incoming crew. He kept the automatic transmitter silent and chose how to begin, knowing that a response would still not tell him who was there.

**Pergunta:** What was the advantage of repeating the three beats?

- **A.** It guaranteed that the sender would identify itself
- **B.** It used a form the unknown source seemed to recognise
- **C.** It allowed the automatic transmitter to fill the channel

**Gabarito editorial:** B
**Explicação:** Repeating the pattern uses a form the receiver seems to recognise. Spoken words might not be understood.

**Pista e continuidade:** As duas escolhas têm vantagens explícitas e não dependem de tradução ou fala livre do jogador.

**Próxima etapa:**

- Send a simple recorded greeting → `spoken-reply`
- Repeat the three-beat pattern → `pulse-reply`

## Someone Is Listening (`spoken-reply`)

Anik recorded a simple greeting: "This is Anik. Someone is listening." After he sent it, the speaker returned a broken version of the last two words. He saved both recordings and left clear notes for the incoming crew. There was no name, image or explanation of the distance between them. Still, a reply had changed with his message. He postponed the automatic broadcast. His last night shift had become the start of a conversation that another person could continue.

**Pergunta:** What did the returned words support?

- **A.** That Anik knew the sender's identity and location
- **B.** That the incoming crew no longer needed his recordings
- **C.** That the reply had changed in relation to Anik's message

**Gabarito editorial:** C
**Explicação:** The returned words change with his greeting. They support a connection, but give no identity or location.

**Pista e continuidade:** A resposta dá evidência de relação com a mensagem, sem alegar identidade que o texto não revelou.

**Final da rota.**


## A Pattern for the Next Shift (`pulse-reply`)

Anik sent three beats and waited. The speaker replied with three beats, then a fourth. He repeated the four-beat pattern; the next reply added one more. He saved the exchange and prepared a careful report for the incoming crew. It showed a developing response, not the identity of a sender. He left the channel free of automatic speech. Before leaving the room, he listened once more, comforted by the thought that curiosity could be shared without a common language.

**Pergunta:** Why did Anik describe the exchange cautiously?

- **A.** It showed a response but did not identify the sender
- **B.** It contained only the station's old automatic speech
- **C.** It proved that the incoming crew had sent every beat

**Gabarito editorial:** A
**Explicação:** The growing pattern shows a developing response. It does not say who is sending it.

**Pista e continuidade:** O padrão cresce de forma observável e a transferência do posto conclui o objetivo, mantendo a incerteza científica honesta.

**Final da rota.**


## Produção e revisão

**Direção visual:** animação 2D cinematográfica dark, personagens desenhados, cenários pintados e sombras profundas com pistas legíveis, sem aparência de pessoas reais.

Produção e envio à VPS concluídos: WAVs gerados a partir do texto exato e conferidos por transcrição automática; artes 2D dark inspecionadas e corrigidas por cena. Grafo e questões verificados pelos testes. Não declara revisão auditiva humana nem certificação CEFR.

`story.json` contém os roteiros de áudio, prompts de imagem/animação e referências de continuidade por cena. `media.example.json` continua sendo um modelo. O pacote real com cinco WAVs, cinco artes e cinco GIFs está em `media/anthology-01/the-lighthouse-beyond-earth-b1`, fora do Git, com manifesto e créditos preenchidos.

```sh
python3 scripts/import_story.py content/anthology-01/the-lighthouse-beyond-earth-b1/story.json --check
```

Para importar somente o texto como rascunho, use o mesmo JSON com `--url` e `--email`. Versões publicadas são imutáveis; duplique pelo painel antes de editar. Não use o manifesto de exemplo como pacote pronto.

**Envio à VPS:** este texto e suas mídias já foram enviados. Consulte `../vps-import.json` para o ID real da versão; use esse ID ao retomar a edição, para não criar uma cópia. Os comprovantes `vps-import.json` e `vps-media.json` registram estado, IDs e hashes verificados. Os prompts e os arquivos produzidos estão preservados localmente.
