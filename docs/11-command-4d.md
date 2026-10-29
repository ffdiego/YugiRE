# Comando 0x4D — limpeza de uma entrada associada

Oitava rodada, 2026-10-29. Análise estática; endereços VA, image base
`0x00400000`; alvo identificado em [validação](evidence/command-4d-validation.json).
Nenhum código do jogo foi executado nem alteração de análise persistida.

## Conclusão

O caminho de Parasite Paracide encontrado na rodada anterior emite um comando
que **zera dois campos de 16 bits na posição da associação encontrada**.
O processamento passa por uma fila; não acontece dentro do helper de busca.
A escrita corresponde exatamente ao índice e aos metadados consultados por
0x00570BE0. O handler examinado não compacta o array nem decrementa seu contador.
Confiança alta nesses acessos e na ligação entre produtor e consumidor.

## Registro e armazenamento

**0x005B9170** recebe quatro argumentos e monta quatro palavras de 16 bits,
totalizando oito bytes. Para 0x4D não há a conversão adicional de índices para
IDs que a função aplica a alguns outros códigos. O caminho já identificado
**0x00574960(s, 11, i)** fornece:

| Offset no registro | Palavra |
|---|---|
| +0 | `0x4D` ou `0x804D`, conforme o seletor usado pelo wrapper |
| +2 | `11 | (i << 8)`, truncado a 16 bits |
| +4 | zero |
| +6 | zero |

Há uma saída antecipada quando `(uint32[0x00A54E5C] & 0x180) == 0x80`.
Nos demais casos, se o contador uint16 de VA `0x00A558A8` for menor que 256,
o registro é anexado ao buffer em **0x00A550A8**, com passo de oito bytes.
Mesmo quando esse buffer está cheio, o fluxo alcança **0x00449D50**.
Essa função aloca/copia oito bytes e insere um wrapper num contêiner com
elementos de 16 bytes; o wrapper não é o payload de oito bytes em si.
0x0044B2A0 apresenta inserção/realocação genérica de contêiner. A assinatura
automática do decompilador não recupera todos os argumentos corretamente;
o listing da chamada em 0x00449D9C–0x00449DA3 confirma a inserção no fim.

0x0044A3D0 contém transferência em blocos de oito bytes, em caminhos de
leitura/escrita. Isso sugere um histórico serializável adicional, sem provar
qual recurso de interface o utiliza. Não classificar toda a estrutura como
replay apenas por essa observação.

Evidências: [produtor](evidence/command-dispatch-entry.txt),
[cópia e buffer](evidence/command-record-consumer.txt),
[contêiner e serialização](evidence/command-queue.txt).

## Consumo e despacho

**0x005B9CA0** copia o primeiro registro do buffer para o registro corrente
em **0x00A550A0**, decrementa o contador e desloca os registros restantes
em oito bytes. Em seguida, no estado apropriado, chama **0x005B9470**;
outros estados permitem que o processamento prossiga em chamadas posteriores.
Não confundir esse deslocamento da fila de comandos com o array de cartas.

Os acessores 0x00449DF0/0x00449E00/0x00449E20 expõem um cursor em passos
de 16 bytes sobre o outro contêiner. 0x005BC580 lê seus payloads e usa
0x005B9370 para alimentar o buffer de oito bytes, sem nova cópia para o
contêiner. Isso identifica um caminho de reapresentação dos registros;
semântica de modos e flags ainda pendente.

Evidências: [xrefs sem descompilar seus consumidores](evidence/command-buffer-xrefs.txt),
[acessores](evidence/command-accessors.txt),
[reapresentação](evidence/command-dispatch-consumer.txt),
[retirada da fila](evidence/command-batch-processing.txt),
[outro produtor](evidence/command-replay-source.txt).

### A tabela intermediária importa

O pseudocódigo de 0x005B9470 avisa que não conseguiu normalizar o switch.
Seus rótulos `case` representam índices intermediários, **não opcodes**.
As instruções e bytes do arquivo confirmam:

```text
opcode = uint16[0x00A550A0] & 0xFFF
indice = uint8[0x005B9AEC + opcode - 1]     # 0x4D -> 0x2C
stub   = uint32[0x005B9838 + indice * 4]   # 0x005B9586
stub: JMP 0x00571BF0
```

O dispatcher aceita esse caminho para opcodes entre 1 e 256. Seu trecho
selecionado está em [pseudocódigo](evidence/command-opcode-dispatch.txt);
a conclusão usa [instruções](evidence/command-4d-dispatch-instructions.txt)
e a leitura direta das duas tabelas, não os rótulos isolados.

## Escrita no estado

O trecho **0x00571BF0**, sem função independente reconhecida na análise salva,
desempacota o registro corrente e chama **0x0056B1C0** com:

```text
arg1 = (comando >> 15) | ((palavra_2 & 0xFF) << 8)
arg2 = palavra_2 >> 8
arg3 = palavra_3
arg4 = palavra_4
```

0x0056B1C0 escreve duas palavras, nos endereços:

```text
delta = (arg1 & 1) * 0xD44 + ((arg1 >> 8) & 0xFF) * 0x90 + arg2 * 2
uint16[0x00A55D80 + delta] = arg3
uint16[0x00A55DC0 + delta] = arg4
```

Com categoria 11, `11 * 0x90 = 0x630`: as bases tornam-se
**0x00A563B0** e **0x00A563F0**, as mesmas da consulta de Parasite Paracide.
Como os dois últimos argumentos são zero nesse produtor, zeram o índice
e os dois bytes de metadados da posição `i = resultado_da_busca - 1`.
Após chamar o helper, 0x00571BF0 limpa o bit 0x4000 em 0x00A558AC.
Não altera o contador em 0x00A563AE nem move outras entradas nesse trecho.

Evidências: [handler](evidence/command-4d-handler.txt),
[duas escritas](evidence/command-4d-state-update.txt).

## Reprodução e limites

```bash
python3 YugiRE/scripts/validate_command_4d.py
bash YugiRE/scripts/run-archive-query.sh command-4d-handler.txt ins:00571bf0:21 ins:005b9586:1
bash YugiRE/scripts/run-archive-query.sh command-4d-state-update.txt 0056b1c0 ins:0056b1c0:24
```

O validador exige o hash conhecido, traduz VA pelos cabeçalhos PE e verifica
o despacho, seis sequências de instruções, os cinco originais e o hash do
banco da rodada anterior. Saída esperada: `PASS`. O JSON contém bytes e offsets.
Relatórios de listing mais amplos podem incluir instruções posteriores ao
`RET`; essas instruções adjacentes não fundamentam as conclusões do handler.

Isso prova um dispatcher de comandos com escrita de estado, não um
interpretador geral de efeitos de cartas. Também não identifica ainda o
significado completo da categoria 11, do vínculo ou do código 0x365F.
Próximo passo pequeno: localizar onde uma entrada com nibble 13 é criada e
como seu vínculo é preenchido; isso pode explicar o ciclo de vida da associação.
