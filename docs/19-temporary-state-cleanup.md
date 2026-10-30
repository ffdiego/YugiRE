# Limpeza de estado temporário — comando 0x02

Análise estática, EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, image base 0x00400000. Sem execução do jogo ou gravação do banco.

## Flags do jogador sobrepostas à categoria 11

O layout já seguido tem registros de 0x90 bytes e associações em +0x0C/+0x4C.
A categoria 11 usa a mesma aritmética para a lista global do lado. Há também
uma coincidência exata para as flags:

```text
0x00A55D74 + 11*0x90 + 0x8C = 0x00A56430
```

Assim, um loop sobre 12 registros, no campo +0x8C, alcança tanto as flags das
11 posições normais quanto a palavra de flags do jogador. Não há uma 12ª
zona normal de monstro/magia. O mesmo arranjo explica contador 0x00A563AE,
índices 0x00A563B0 e metadados 0x00A563F0 para a categoria 11.

## Comando e máscaras

O dispatcher confirma **0x02 → índice 1 → stub 0x005B94A3 → 0x00574230**.
Esse consumidor primeiro chama 0x00403EC0 com o lado do comando. Depois:

- Para os dois lados, percorre **12** DWORDs de flags com passo 0x90 e faz
  `flags &= 0xFFFF`. Isso também limpa bit 18 de Soul Exchange em 0x00A56430.
- Faz `0x00A577EC &= 0xFFFF` nas flags globais.
- Percorre cinco listas com seus contadores e limpa bits 21–23 das palavras
  de estado usando `word &= 0xFF1FFFFF` (endereços base 0x00A56434,
  0x00A56574, 0x00A566B4, 0x00A566F0 e 0x00A5686C, mais stride por lado).
- Chama **0x0056AEA0** para limpar/decrementar associações.

O handler não apaga todos os campos das cartas nem todas as flags. Em
particular, não interpretamos cada bit baixo sobrevivente como permanente.
A máscara não é o mesmo tipo de dado do nibble das associações.

## Contador de quatro bits nas associações

0x0056AEA0 percorre 12 categorias dos dois lados, processando posições
ocupadas e sempre a categoria 11. Percorre as entradas de trás para frente:

| Tipo baixo | Operação nesta rotina |
|---|---|
| 0 | Remove por 0x005695E0 |
| 3 ou 12, bits 4–7 iguais a zero | Remove por 0x005695E0 |
| 3 ou 12, bits 4–7 maiores que zero | Decrementa esses quatro bits |
| Demais tipos | Mantém |

Ao decrementar, preserva byte alto e nibble do tipo: máscara **0xFF0F**.
Ao remover, o compactador preserva a correspondência de índice/metadado e
não zera a cauda física inativa, conforme o estudo de equipamentos.

Um valor de contador 1 torna-se zero nesta chamada e só é retirado na chamada
seguinte. A unidade é uma passagem pelo handler, ainda não chamada de “turno”.
Soul Exchange cria `3 | ator<<8`, contador zero, portanto sai na primeira
passagem de limpeza em que seu registro de alvo for processado. O bloqueio
de combate do jogador sai nessa mesma execução por outra operação, a máscara
de flags. Equipamentos tipo 1 e Parasite tipo 13 não são removidos por esta
regra específica; podem ser retirados por outros caminhos.

## Relação com inicialização e mudança de turno

A busca encontrou separadamente **0x005B9E50**, que zera amplas regiões de
estado por 0x005BDDF0 e termina gravando **8000 (0x1F40)** como uint16 em
0x00A55D64 e 0x00A56AA8, separados por 0xD44. Isso confirma o valor inicial
no código e os endereços de LP; não demonstra uma limpeza feita a cada turno.
Não inferimos quantidade de cartas compradas das outras chamadas desse helper.

O consumidor do comando **0x03**, 0x004048C0, incrementa o contador uint16 em
0x00A577F8, alterna bit 0 da metade alta, aplica OR 0x0E nessa metade e zera
0x00A577FE. É uma pista concreta de transição de turno/etapa. A ordem de
emissão entre 0x01/0x02/0x03 ainda não foi identificada. Portanto não fechamos
a duração de Soul Exchange como uma sequência temporal completa somente
pela presença desses handlers.

Para validação dinâmica futura, um breakpoint em 0x00574230 permite observar
quando ocorre a limpeza em um duelo comum, sem precisar obter Soul Exchange:
registrar contador/fase antes e depois da mudança de turno já testa a ligação
pendente. Não foi solicitado nem executado esse teste nesta sessão.

## Reprodução e evidências

```bash
python3 scripts/validate_temporary_cleanup.py
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_temporary_state_model.py'
```

O validador confere fingerprint, despacho, três transferências e quatro
sequências nativas. Cinco testes do modelo verificam expiração, contador,
compactação ativa, categoria global e máscaras. Não executam o original,
não modelam caudas inativas e não identificam quem agenda o comando.

[Handler](evidence/temporary-state-cleanup-handler.txt),
[associações](evidence/temporary-association-cleanup.txt),
[instruções e contador de turno](evidence/turn-transition-counter.txt),
[inicialização](evidence/player-state-initialization-candidates.txt),
[8000 em instruções](evidence/duel-initial-life-values.txt),
[relatório de validação](evidence/temporary-cleanup-validation.json).
As consultas com sufixos “candidate”, “reset” e “offsets” preservam caminhos
exploratórios; seus nomes não atribuem funções semânticas a cada trecho.
