# Joey.Lab: automação de experimentos

A branch `work/joey-lab-experiment-automation` parte de `origin/main` e mantém o backend Python/Frida. Os commits `a1700ad` e `fd24342` adicionam, respectivamente, o sinal de cancelamento externo ao coordenador paralelo e o comando .NET. Os demais ajustes e este handoff estão no commit seguinte.

O comando executa `python -u -m yugilab`, acompanha stdout/stderr e solicita cancelamento por arquivo; ele nunca encerra PIDs. Aguarda o backend terminar sua limpeza, mesmo se demorar. Em uma execução paralela, o coordenador repassa o sinal aos workers, sem matá-los. O `manifest.json` do laboratório só indica conclusão se o backend terminou com sucesso, as capturas passaram pela análise de integridade e a verificação de encerramento do processo foi confirmada pelo backend. A execução individual exige ainda PID, contagem e footer correspondentes. `report.json` contém os relatórios de captura e contextos observados; os JSONL originais e o manifesto paralelo, quando aplicável, permanecem em `backend/`. Campos desconhecidos continuam na captura original. Nenhuma análise transforma contagens de comandos em vencedor ou estado completo.

Saídas padrão ficam em `scratch/experiments/` (ignorado pelo Git). Um `--output-dir` dentro do repositório deve ficar sob `scratch/`; se for externo, o operador deve garantir que ele não seja versionado. O analisador atual carrega cada JSONL na memória. Uma falha de inicialização, captura parcial, cancelamento ou encerramento não verificado deixa `complete=false`; código de saída 130 indica cancelamento, 2 indica validação/integridade, e erros de processo são propagados. Se um worker ficar travado no Frida, a espera pela limpeza também pode ficar indefinida: verificar manualmente o log, sem matar PIDs de outras sessões.

## Piloto Windows: três duelos, um worker

Na raiz do repositório, com .NET 10, a venv Windows com Frida instalada conforme `yugilab/README.md`, e o executável original com o SHA256 exigido:

```powershell
dotnet build YugiRE.sln

dotnet run --project .\src\Joey.Lab -- experiment `
  --exe "C:\caminho\para\joey_pc.exe" `
  --duels 3 --workers 1 --seed 12345 --speed-value 600 `
  --deck-0 ".\native_hooks\decks\demo-A.json" `
  --deck-1 ".\native_hooks\decks\demo-B.json" `
  --trace-ai --python ".\native_hooks\.venv-frida\Scripts\python.exe"
```

Ajustar o caminho do EXE e dos decks ao ambiente local. O processo exibe a pasta de resultado e o progresso do backend. Verificar `manifest.json` (`complete`, `capturesComplete`, `backendVerified`), `report.json` (issues, contagens e conclusão live), o footer do JSONL e o log de encerramento confirmado por handle. Se qualquer verificação falhar, preservar a pasta e interromper a ampliação. Ctrl+C solicita limpeza e aguarda o backend; não fechar o terminal durante essa etapa. Os resultados podem ser passados diretamente aos comandos offline de Joey.Lab; duas execuções podem ser comparadas com `compare` usando os caminhos das capturas ou diretórios aceitos pelo comparador.

## Evidência offline e próximo passo

Fedora: build da solução sem restore, 675 checks .NET com DAT/deck locais, suítes Python `native_hooks` e `yugilab`, e três testes JS do backend. Os testes .NET usam processos shell simulados para sucesso, erro, cancelamento e descoberta de JSONL; o teste Python cobre cancelamento externo antes do spawn. Não foram executados Joey, Frida ou o piloto Windows. O próximo passo é executar o comando acima no Windows e inspecionar os artefatos e o encerramento do PID; até então a integração real permanece não validada.

## Falha observada no primeiro piloto e correção offline

O operador executou o piloto de três duelos no Windows. O backend criou o PID 16916 suspenso, mas não chegou ao `resume`: o RPC de trace de IA rejeitou o modo recebido. O próprio backend registrou o encerramento desse PID pelo handle Windows; não há duelos observados nesse piloto. Em seguida, o Joey.Lab falhou ao reabrir `backend.log` ainda aberto para escrita, impedindo a geração do manifesto de falha. A mensagem `Unable to start idund` também apareceu; seu impacto permanece desconhecido.

O host agora passa `False` explicitamente ao RPC para selecionar os quatro hooks padrão, inclusive no console legado. Isso evita depender da interpretação de um argumento omitido pelo transporte Frida; não modifica hooks. Os mocks exigem o argumento explícito. O Joey.Lab fecha o log antes de relê-lo e o teste de falha exige manifesto incompleto. Essas correções foram verificadas offline; não provam que o transporte real ou o restante do startup funcionem no Windows. Próximo passo: atualizar a branch no Windows e repetir exatamente o piloto acima, preservando o log e o manifesto se houver nova falha.
