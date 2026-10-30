# Equipamentos — retirada e limpeza dos vínculos

Continuação estática de [Legendary Sword](legendary-sword.md), 2026-10-29.
Mesmo alvo fingerprintado, endereços VA, base 0x00400000. Nenhuma execução
do jogo, alteração dos originais ou gravação do banco. Timing fica para o Windows.

## Resultado e alcance

O caminho de retirada de campo limpa referências à posição que saiu e zera
seu registro. A remoção das referências compacta posições e metadados juntos.
No caminho de destruição do monstro, o emissor de dependências usa a associação
tipo 1 para encaminhar também a retirada dos equipamentos referidos.

Isso fecha a limpeza comum do vínculo que Legendary Sword cria. Não cobre
todos os produtores de movimento, todos os equipamentos ou a ordem visual
de operações simultâneas.

## Equipamento retirado

O consumidor de destruição **0x00404B40** chama **0x00570790** para retirar
a origem antes de inserir o destino. Para uma posição de campo, esse helper:

1. Chama **0x0056A490(lado, posição)** para limpar referências à origem.
2. Zera os **0x90 bytes** do registro por **0x005BDDF0**.
3. Limpa a marca da posição na máscara global de operações.

0x0056A490 percorre **dois blocos e doze conjuntos de associações por bloco**,
com passo 0x90. O laço inclui índice 11: não equivale a doze posições comuns
de campo. Nesse índice, os endereços coincidem com a categoria global já
estudada em Parasite; o tipo de metadado determina como interpretar entradas.

Para tipos **1, 2, 4, 5, 6, 7 e 8** no nibble baixo, compara a palavra de
posição com **`lado_retirado | posição_retirada<<8`**. Se coincidir, remove
a entrada por **0x005695E0**. Os demais tipos não entram nessa comparação,
mesmo que sua palavra tenha o mesmo valor numérico. Em particular, não
tratamos uma entrada de tipo 13 de Parasite como uma coordenada de equipamento.

Como o vínculo tipo 1 de Legendary Sword está no monstro e aponta para o
equipamento, a retirada do equipamento apaga essa referência do monstro.
O próximo cálculo de atributos deixa de percorrê-la. Além disso, o cálculo
já exige ocupação na posição referida; a limpeza evita conservar o vínculo
para uma carta que venha a ocupar esse espaço depois.

Essa conclusão vale para a configuração simples reconstruída: um vínculo
tipo 1 no monstro. Não pressupõe que equipamentos com outras associações
tenham sempre as mesmas consequências ao sair.

## Compactação dos arrays

O registro usa contador uint16 em **+0x0A**, posições em **+0x0C** e
metadados em **+0x4C**. **0x005695E0(lado, posição, índice)**:

- Se o índice está dentro do contador, decrementa o contador.
- Se há entradas posteriores, copia posição seguinte e metadado seguinte
  sobre a entrada removida e repete até o novo fim ativo.
- Não zera a antiga última entrada física. Ela fica fora do contador ativo.

O laço chamador recua índice e ponteiros após apagar uma entrada e depois
continua. Assim, reexamina a entrada que acabou de ser deslocada para o mesmo
índice, permitindo remover correspondências consecutivas.

Isso importa para dumps futuros: uma coordenada nos bytes além do contador
não demonstra um vínculo ainda ativo. A leitura deve respeitar o contador.
Os helpers confiam em dados internos válidos; não demonstramos tratamento
defensivo de índices negativos ou contadores corrompidos.

## Monstro retirado pelo caminho de destruição

Antes de consumir o comando de retirada do monstro, **0x00576B30** chama
**0x00577280** para emitir operações das dependências. Esse helper percorre
as associações do registro que está saindo. Para tipos **1, 4 e 5**, exige
ocupação na posição referida e chega a:

```text
0x00576B30(lado_referido, posição_referida, região=14, flags+2, origem=0)
```

Para o tipo 1 criado por Legendary Sword, essa posição é a do equipamento.
A máscara global de marcação e as verificações de 0x00576B30 continuam
participando; não concluímos que a chamada emita um segundo comando em todos
os estados ou que cada efeito simultâneo seja processado duas vezes.

O produtor compartilha o [caminho de destinos 14/15](monster-destruction.md),
incluindo suas condições e o desvio de Banisher quando aplicável. Quando o
consumidor retira de fato o monstro, o registro inteiro é zerado, eliminando
também sua lista de equipamentos. A retirada posterior do equipamento volta
a executar a limpeza genérica das referências restantes.

O produtor **0x00578030** também chama 0x00577280 a partir de uma cópia usada
no estado de batalha. A presença dessa chamada foi confirmada; o ciclo de
batalha completo e a ordem em relação ao dano não foram reconstruídos aqui.

## Helpers relacionados e limites

**0x0056ABE0** faz retirada localizada de uma ou de todas as referências a
uma coordenada numa lista, conforme seu terceiro argumento. Usa os mesmos
tipos e o mesmo compactador. **0x0056B250** faz a consulta inversa, procurando
num monstro ocupado/virado para cima uma lista que contenha a coordenada.
Esses helpers reforçam a interpretação do vínculo, sem provar uma associação
recíproca persistida no equipamento.

Pendências estáticas: outras rotas de movimento e troca de controle;
invalidação por tipo/orientação; composição de modificadores e equipamentos
com efeitos próprios ao sair. O estudo não afirma equivalência com todas
as regras oficiais nem depende de montar esses cenários agora.

## Reprodução

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_equipment_cleanup.py
```

Esperado: treze linhas `PASS`. Confere preservação e etapas anteriores,
instruções de retirada/compactação e destinos da tabela de tipos 1–8.
O modelo Python testa nove cenários contra um filtro independente: lista vazia,
correspondências adjacentes, remoção no meio/fim, tipos não coordenados com
mesmo valor e metadados com bits altos. Confere ordem/alinhamento dos pares
ativos e manutenção dos bytes inativos. **Não executa o código nativo.**

Evidências: [limpeza e retirada localizada](../evidence/equipment-cleanup-entrypoints.txt),
[compactador, cópia de batalha e consulta inversa](../evidence/equipment-cleanup-compaction.txt),
[instruções selecionadas](../evidence/equipment-cleanup-selected-instructions.txt),
[emissão de dependências já seguida](../evidence/monster-destruction-links.txt),
[retirada de estado](../evidence/monster-destruction-commands.txt),
[validação](../evidence/equipment-cleanup-validation.json).
