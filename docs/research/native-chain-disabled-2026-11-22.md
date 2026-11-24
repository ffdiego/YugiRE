# Indicador de corrente desativada — contrato parcial (22/11/2026)

**Alvo:** Joey the Passion Win32, `joey_pc.exe` SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Fontes: inventário de vtables e decompilações transitórias documentadas em [visual-virtual-methods](visual-virtual-methods-2026-11-22.md). **Sem nova captura do original nesta etapa.**

## Caminho nativo conhecido

| Elemento | Evidência de análise estática |
| --- | --- |
| Disparo candidato | `00408730`: verifica condições de apresentação e cria o objeto |
| Construtor | `00452F20`, vtable `005DB374`, camada `0x111` no inventário |
| Update | `004531A0`: incrementa contador +1, desativa ao alcançar `0x18` |
| Draw | `004531C0`: índice `(contador/2)%6`, ramificação de composição dependente do bit `0x4000` do descritor completo |
| Recurso | Manifest `j\duel\chain\disable.txt`; ordem e dimensões ainda não verificadas aqui |
| Geometria | Consumidor `0045C140` ainda precisa de assembly e argumentos |

`24 Updates` não significa 24 frames de tela nem uma duração em segundos. O Draw pode ser chamado em cadência distinta. Com contador inicial 0, os índices para `0..23` são `0,0,1,1,...,5,5` duas vezes; depois o objeto se torna inativo. Isso é **um contrato de índice**, não uma animação completa.

## Implementação nesta etapa

- `src/Joey.LookAndFeel/NativeDisabledChainFrame.cs`: contador, amostragem de índice e flag `0x4000`, sem acesso a raylib, DAT, Core ou FPS. Valida intervalos e interrompe amostragem depois do update 24.
- `NativePresentationChecks.cs`: testes sintéticos de dois ciclos completos, flag separada do índice, limites, desativação e reset.
- `scripts/summarize_native_chain_dat.py`: extrai do relatório de `--inspect-visual-dat` somente manifests e metadados de imagens sob `j\duel\chain\`, apontando manifests esperados ausentes. Não recupera imagens.
- O inventário **continua com `004531C0` como `Pending`**: nenhum Draw novo está integrado ao `Joey.App`; o total permanece 19/34 (0 Verified).

## Coleta independente pendente

Na máquina com o DAT original:

```bash
dotnet run --project src/Joey.App -- /caminho/para/data.dat --inspect-visual-dat
python3 scripts/summarize_native_chain_dat.py
```

Saída: `scratch/visual-review/chain-manifests.json` (inspecionar e compartilhar privadamente, sem commitar).

Na máquina com Ghidra:

```bash
bash scripts/pending/02-chain-disabled-ghidra.sh
```

Saída: `scratch/ghidra-chain-disabled-contract.zip` (não versionar). Relacionar a ordem dos recursos do manifesto com o Draw real; rastrear o fluxo `00408730 -> 00452F20` e os argumentos de `0045C140`, o pass condicionado pelo bit `0x4000`, pivô, escala, blend e orientação. Só então integrar os YGAs/BMPs ao cliente e comparar com o executável em 800×600.

**Limite:** o modelo não dispara o indicador em um duelo nem bloqueia entrada. Conectar agora sem evidência de gatilho/composição seria simulação, não fidelidade.


## 23/11 — bundle de assembly recebido (5/5 funções)

`004531C0` consulta a textura de índice `(contador/2)%6` diretamente: seis imagens inteiras, não seis recortes de um único atlas. `004531E7` resolve o centro com os três campos do descritor via `0045C140`. `0045324E` testa 0x40 no byte `obj+0x15`, que é a parte alta da palavra de descritor iniciada em `obj+0x14`; a máscara correta é **0x4000**. No ramo não rotacionado (`0045328B..004532B0`), o destino é centro menos metade das dimensões da imagem. No outro (`00453254..00453279`), passa ângulo 90, escala 0x10000 para `00422540` e troca os meios-eixos. `NativeDisabledChainFrame` e testes atualizados. Ainda faltam o manifesto real `disable.txt`, a resolução absoluta do centro e a ligação do gatilho de jogo; Draw continua Pending.


## Fechamento cruzado dos bundles de 23/11

O dispatcher `005B9470`, presente no bundle card-status,
tem o caso **0x72 → 00408730**. Não é mais apenas
um chamador candidato. A rotina `00408730`
(bundle chain-disabled) mostra:

1. `00408761..0040878E`: se houver objeto de apresentação
   bloqueando a fila, retorna sem avançar o comando.
2. `0040878E..004087CB`: exige fase baixa
   `DAT_00A558AC&0x7f==0`, zona <=10 e ocupante
   com ID não zero (`fieldRecord&0xfff`). Caso
   inválido limpa o bit 0x4000 da variável de comando.
3. `004087D1..004087FB`: chama
   `00570710(side,zone,5,lowWordA550A4)`. A criação
   visual é condicionada a lowWord diferente de zero e
   highWord igual a zero. O papel semântico dos dois
   parâmetros ainda não é provado; não equiparar a qualquer
   cancelamento de corrente no Core.
4. `00408801..004088C6`: constrói descritor de lado,
   região e slot. Para zona <10, região =
   `(zone/5)*5` e slot = `zone%5`; zona 10 usa
   região 10, slot 0. O descritor copia a condição
   de posição do campo para 0x4000, como confirmou o Draw.
5. Constrói/encaminha objeto `00452F20` e incrementa
   o estado baixo do comando conforme o ramo normal.

`004531C0` chama `0045C140(desc&1,(desc>>1)&31,
(desc>>6)&255)`. O bundle mostra que as regiões usuais
de campo delegam a `0045C280`; regiões 0xB..0xE
possuem casos especiais (mão e posições com deslocamentos).
O retorno é par EAX/EDX. Para o efeito 0x72, as
regiões construídas são 0,5,10. Portanto pode-se
reusar geometria de zonas confirmada, sem inventar
um centro global para o indicador. Não alegar que os
valores absolutos de `0045C280` vieram nesse bundle.

O contrato do objeto (24 Updates, seis imagens inteiras,
rotacionar 90 graus para 0x4000) está consolidado.
Ainda falta o manifesto original `disable.txt`, conferir
a semântica de `00570710`/opcode 0x72 na engine e validar
a composição de `00422540`. A coleta Ghidra anterior
está concluída; não repeti-la. Draw continua Pending.


## V12 — implementação isolada para comparação

Consulta ao SQLite privado joey-knowledge-visual-decoded.sqlite
(fingerprint igual ao anterior): a decompilação de 00570710
mostra um setter genérico de bit em 00A55E00 (stride de 0x90
por zona, 0xD44 por lado). Parâmetro 4 zero limpa; não zero
seta. 00408730 fornece índice de bit 5. A escrita não depende
do argumento alto, que condiciona a criação visual.
00578390/005783C0 são produtores de 0x72, e 005BAFD0 chama
o segundo no case 1, antes de avaliar os callbacks do elo
nos cases 2/3. Não é evidência de uma negação genérica.
As assinaturas decompiladas de 004022E0 e os argumentos
perdidos em 00578390/005783C0 exigem assembly ou trace
antes de ligar a semântica ao Core. Nenhuma extração dos
quatro bundles foi repetida.

O cliente passa a oferecer uma revisão isolada com os
recursos lidos de disable.txt no DAT local, ordem original
das seis imagens, centros por região e perspectiva,
rotação de 90 graus e limites inteiros. Não há jogo, input
de duelo, placeholder ou duração inventada nesse modo.

Exemplo: lado 0, região 0, slot 2, bit 0x4000:
```powershell
dotnet run --project src/Joey.App -- ..\passion\data.dat --review-disabled-chain=4080 --review-perspective=0
```
F7 avança exatamente um Update (até 24); F8 reinicia; F12
grava PNG e JSON em scratch/visual-review; Escape encerra.
Use 0080 para a variante sem rotação, ou perspectiva 1
para a outra orientação do campo. O parser aceita a
convenção filename + inteiros opcionais já usada em outros
manifests; se a sintaxe real divergir, falha explicitamente.
Não temos o arquivo disable.txt real para executar esse modo
aqui. Blend/colorkey e equivalência de pixels permanecem
não verificados, usando a política já existente do cliente
somente na revisão isolada. O gatilho de duelo e integração
com ACTIONS seguem pendentes.

Testes sintéticos de ambos os lados, 11 zonas, bit 0x4000,
dimensões ímpares, ordem de manifesto, gates e set/clear.
A Draw é exercitável pelo modo de revisão, mas ainda não
integrada ao fluxo normal: **Pending, cobertura 19/34**.
