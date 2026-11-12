# Joey.Lab: piloto Windows de três duelos

## Proveniência

Experimento fornecido pelo operador, iniciado em 2026-11-12 03:24 UTC, após as correções `a32b3d7` e `9c50ee6`. ZIP local `experiment-20261112-032435-c164293c1652457b8a91b2c791319c7e.zip`, SHA256 `d1902895350e3faf97ac5c7dc5f24577ec13eb10d2c072527d3166c56a60e568`. Captura JSONL SHA256 `e3395667946a46e48edb893a797743b727531fe7a8f274bdf38a760eb6765af1`. Nenhum JSONL ou deck foi incluído neste repositório. A análise abaixo foi repetida offline sobre o ZIP fornecido, com `summarize` e `combat-contexts` da branch e conferência do log, manifesto e footer.

## Observações

- O manifesto registra EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`, três duelos, um worker, seed base 12345 e velocidade 600. O cabeçalho da captura não contém hash do EXE: o aviso do analisador permanece correto; o manifesto é a atestação separada registrada pelo host, não uma atestação embutida no JSONL.
- Log: PID 18652 criado suspenso, quatro hooks de IA armados, Joey retomado e progresso até 3/3. A mensagem de saída indica que o Frida pediu o encerramento e o handle Windows confirmou o fim do PID criado. O manifesto registra `exitCode=0`, `backendVerified=true` e `complete=true`.
- O footer informa 3/3 terminais, 3 aplicações da seed, 3.885 eventos, `overwritten=0` e `complete=true`. O analisador confirma sequências, contagem de eventos e três comandos terminais, sem erros de integridade ou correlação de seletores. Foram registrados seis eventos de aplicação e seis de verificação de deck e um evento de aplicação de velocidade. Essas observações não demonstram independência estatística nem equivalência integral das partidas.
- Contagens: 1.203 comandos, 2.489 eventos `ai_probe`, 70 seletores correlacionados e 193 buffers de avaliação de combate, dos quais 170 têm dois buffers de stats associados pelo analisador. Não são 70 jogadas necessariamente executadas nem vencedores. `combat-contexts` encontrou zero frames do esquema de contexto estendido e zero eventos desse esquema sem correlação; o piloto usou o trace padrão, não o modo `combat-context`.
- O único aviso do `summarize` é a ausência do hash do EXE na captura. A inspeção de `readError` não encontrou valores não nulos. O log também registra `Unable to start idund: idund did not start listening`: a execução prosseguiu, mas não há evidência suficiente para explicar a causa ou descartar outros impactos.

## Limites

Estes arquivos comprovam este piloto observacional e o encerramento verificado do seu processo; não comprovam múltiplos workers, cancelamento real, captura lossless em todos os cenários, legalidade, vencedor, igualdade de estado, nem comparação dinâmica com Joey.Core. A análise de combate não tem posição/flags/efeitos e outros campos necessários comprovados no instante de consumo. Comparações `Match`/`Mismatch` exigem um adaptador de contexto completo validado; não foram executadas neste piloto. Próximo passo: executar no Windows o novo relatório automático neste mesmo perfil de três duelos e confrontar seu resumo com o JSONL, preservando eventual falha.
