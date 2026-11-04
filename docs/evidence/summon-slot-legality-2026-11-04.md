# Elegibilidade das zonas: ocupação e predicado de efeito

2026-11-04. Fonte: `summon-slot-legality.txt`, pseudocódigo e
assembly extraídos pelo operador do Ghidra, build com EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Resultado principal: FUN_00569F70

O Ghidra fornece:

```c
bool FUN_00569F70(uint side, int slot) {
    if ((monster_slots[side][slot] & 0xfff) == 0) {
        return FUN_00569E10(side, slot) != 0;
    }
    return false;
}
```

O acesso original utiliza base `DAT_00A55D74` e índices
`slot * 0x24 + (side & 1) * 0x351`. O predicado tem duas
condições necessárias:

1. O low12 do slot precisa ser zero (sem carta presente).
2. `FUN_00569E10(side, slot)` precisa retornar != 0.

**Não sabemos ainda o conteúdo de `FUN_00569E10`**.
Essa é agora a função prioritária para rastrear o bloqueio
de dois slots relatado pelo usuário sob efeito de Ground
Collapse. A evidência de summon no slot 2 e set no slot 3
é consistente com a rejeição de slots 0 e 1, mas não
demonstra isoladamente qual condição foi responsável.

## Contagem das zonas elegíveis: FUN_0056A000

```c
int FUN_0056A000(uint side) {
    int eligible = 0;
    for (int slot = 0; slot < 5; ++slot) {
        if (FUN_00569F70(side, slot) != 0) ++eligible;
    }
    return eligible;
}
```

Portanto, essa rotina conta **slots aptos a uso segundo o
mesmo predicado**, e não simplesmente zonas vazias.
Ela é chamada inclusive por `FUN_005AAB90` (estudo
de elegibilidade de summon) e por outros controladores
e operações de campo.

Já havia referências a `FUN_00569E10` em
[`normal-summon-tribute-context.txt`](normal-summon-tribute-context.txt),
dentro de contagens relacionadas a monstros envolvidos em
possíveis tributos. Isso sugere que a restrição subjacente
é reutilizada pelo jogo em mais de um contexto, mas não
estabelece qual efeito é inspecionado pela função.

## Alteração da flag de turno: FUN_005706B0

Contrato estático confirmado:

```c
void FUN_005706B0(uint side, uint8_t bit, int value) {
    uint32_t* flags = &player_flags[side & 1];
    if (value == 0) *flags &= ~(1U << (bit & 31));
    else *flags |= 1U << (bit & 31);
}
```

O campo é `DAT_00A56430 + (side & 1) * 0xD44`.
`FUN_005706B0` **somente modifica o bit em memória**;
`FUN_00579930`, seu chamador, verifica se o valor já
está correto e produz `0x0D` quando necessário.
`FUN_00579930(side,0x11,1)` marca o bit 17
(`0x20000`), associado à normal summon consumida.

Não concluir que apenas esse bit constitui a autorização
de turno. A validação de classe da carta, efeitos contínuos
e estado do duelo exige gates superiores.

## Cuidado com a implementação externa

O encadeamento confirmado é:

```text
FUN_0056A000(side)   -> quantidade de slots aceitos
FUN_0056A030(side)   -> seleciona slot aceito, ou 0xFFFFFFFF
                          |
                          v
                  FUN_00569F70(side, slot)
                          |
                          +-- low12 do registro do slot é zero
                          +-- FUN_00569E10(side, slot) != 0
```

`FUN_005AD740` recebe o slot selecionado pelo jogo
e inicia a operação. Não substituir o predicado nativo
por teste de slot vazio: Ground Collapse é justamente o
contraexemplo. No futuro executor, sempre revalidar
seleção/legitimidade no contexto da thread de duelo e
impedir índice inválido `0xFFFFFFFF`.

## Próxima consulta: segundo nível do predicado

Executar a partir da raiz do repositório Windows:

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report summon-zone-effect-predicate.txt `
  -Addresses @(
    '00569e10',
    'xref:00569e10',
    'ins:00569e10:90'
  )
```

A decompilação deve revelar se `FUN_00569E10`
consulta as zonas impedidas por Ground Collapse ou se
essa restrição vem de uma função ainda mais profunda.
`xref` complementa a lista de callers, mas não garante
referências indiretas que o Ghidra não reconheceu.

Nenhuma chamada ativa de summon/set foi acrescentada nesta etapa.
