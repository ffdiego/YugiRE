# AI — equivalência das decisões do Joey

Objetivo: reproduzir as decisões do joey_pc.exe, não criar uma CPU mais forte. Reutilizar captura IA vs IA, SQLite e laboratório ACTIONS.

## I01

Capturar snapshots antes de decisões reais: fingerprint, duelo/turno/fase, jogador, zonas/ocorrências, opções legais, flags/histórico, informações ocultas acessíveis à CPU e RNG quando observáveis. Capturar escolha e vínculo com trace de ações. Guardar JSONL/SQLite privados; publicar schema, resumos e fixtures sanitizadas. Contexto insuficiente é inconclusivo.

## I02

Extrair Joey.AI da política CPU existente. Entrada: estado/visão imutável e opções legais. Saída: decisão pela mesma API do jogador. Core valida e executa na fila única; IA não altera zonas, LP ou apresentação e não tem fila própria.

## I03

Comparar decisões independentes do mesmo snapshot, incluindo alvo, ordem e RNG/empates conhecidos. Relatar acertos, divergências, inconclusivos, cobertura por tipo e primeira divergência. Separar corpus de ajuste e validação. Métrica AI = decisões equivalentes / comparáveis, reportar denominador e gaps.

## I04

Executar IA vs IA headless com decks/ordem/RNG controlados e comparar decisões e ações. Separar divergência de regra de escolha. Reusar corpus em CARDS/ACTIONS. Não declarar equivalência integral com registros desconhecidos.
