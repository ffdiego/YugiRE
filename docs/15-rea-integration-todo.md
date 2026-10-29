# TODO — integração do REA

Plano registrado em 2026-10-29; **integração ainda não instalada nem testada**.
Objetivo: reduzir tokens consumidos por consultas e resultados repetitivos,
preservando o projeto Ghidra, as evidências e os validadores existentes.

## Ambiente observado

- Ghidra 12.2 em `/home/diego/prj/re/joey/tools/ghidra_12.2_PUBLIC`.
- JDK 25 completo e `ps` disponíveis.
- Node.js 24.18.0 e npm 11.16.0; comando `rea` ausente do PATH.
- A documentação atual de `main` pede Node 24 >=24.22.1 nessa linha.
  Confirmar os requisitos da versão efetivamente publicada antes de instalar.
- Projeto: `YugiRE/ghidra-projects/Joey.gpr`, programa `joey_pc.exe`.

## Etapa 1 — preparar e fixar versões

- [ ] Consultar a versão publicada de `rea-agents` e seus requisitos de Node.
- [ ] Atualizar o Node para uma versão suportada, se necessário; registrar
  método, versão e caminho, sem trocar a instalação de Java/Ghidra.
- [ ] Instalar uma versão fixa do REA em local estável, fora do repositório.
- [ ] Registrar versão e origem do pacote; não depender de `latest` no uso diário.
- [ ] Executar diagnóstico limitado ao provider Ghidra e guardar o resultado.

## Etapa 2 — piloto por CLI

- [ ] Configurar os caminhos abaixo para a versão escolhida:

```bash
export GHIDRA_INSTALL_DIR=/home/diego/prj/re/joey/tools/ghidra_12.2_PUBLIC
export REA_ANALYSIS_PROVIDER=ghidra
export REA_GHIDRA_PROJECT_PATH=/home/diego/prj/re/joey/YugiRE/ghidra-projects/Joey.gpr
export REA_GHIDRA_PROGRAM_NAME=joey_pc.exe
```

- [ ] Confirmar que o modo de projeto existente usa uma cópia, `-noanalysis`
  e `-readOnly`; conferir hashes do alvo e banco antes/depois do piloto.
- [ ] Comparar com evidências já conhecidas: busca de ID em 0x0059DBE0,
  chamada indireta em 0x0059DEDF e dispatcher de 0x4D em 0x005B9470.
- [ ] Consultar um trecho sem listing: verificar como o REA informa essa
  limitação; conservar nosso caminho de desassemblagem transitória quando preciso.
- [ ] Salvar resultados completos em arquivos locais e enviar ao modelo somente
  os campos necessários à pergunta, sem duplicar texto/JSON do mesmo resultado.
- [ ] Registrar limites: no CLI, chamadas independentes podem encerrar a sessão;
  não pressupor reaproveitamento do decompilador entre comandos.

## Etapa 3 — medir economia de contexto

- [ ] Definir as mesmas três perguntas para o wrapper atual e para o REA.
- [ ] Medir chamadas, tamanho dos resultados efetivamente apresentados ao modelo
  e tokens quando houver medição do cliente; bytes são apenas aproximação.
- [ ] Contabilizar schemas/instruções adicionais e raciocínio/retrabalho,
  além do tamanho de cada resposta de ferramenta.
- [ ] Usar referências a evidências retidas e páginas/resumos em vez de repetir
  dossiers completos, quando disponíveis na interface escolhida.
- [ ] Separar ganho de tempo de inicialização de ganho de tokens: são métricas distintas.
- [ ] Decidir pelo piloto; não prometer percentual de economia antes de medir.

## Etapa 4 — MCP restrito, se compensar

- [ ] Consultar o catálogo da versão instalada e escolher somente famílias
  necessárias para sessão, análise nativa e evidências; não adivinhar seus IDs.
- [ ] Configurar `REA_MCP_TOOL_FAMILIES` com essa seleção.
- [ ] Manter `REA_MCP_INPUT_SCHEMA_PROFILE=compact`; avaliar perfil compacto
  de saída conforme o cliente, sem confundi-lo com redução do resultado real.
- [ ] Revisar o plano de registro somente para Codex e os caminhos alterados;
  manter backup da configuração anterior e preservar outros servidores.
- [ ] Decidir se a skill incluída acrescenta orientação útil ou repete as regras
  já existentes; incluir seu custo de contexto na comparação.
- [ ] Reconectar/reiniciar o cliente e conferir ferramentas efetivamente disponíveis.
- [ ] Validar consultas e reaproveitamento da sessão; desabilitar operações de
  execução do alvo no escopo deste piloto de análise estática.

## Critérios para concluir

- [ ] Consultas de referência reproduzem fatos já validados, com limitações explícitas.
- [ ] Banco original e arquivos do jogo preservados; nenhum repositório de
  dependências, cache ou pacote instalado incluído no Git.
- [ ] Resultado da comparação registrado, inclusive se não houver economia.
- [ ] Documentação com versões, comandos, configuração e recuperação atualizada.
- [ ] Commits por etapa concluída com `Co-authored-by: Codex <codex@openai.com>`;
  push permanece sob responsabilidade do usuário.

## Referências

- [Instalação e requisitos](https://github.com/morluto/rea/blob/main/docs/installation.md)
- [Reuso de projeto Ghidra](https://github.com/morluto/rea/blob/main/docs/installation.md#reuse-an-existing-ghidra-project)
- [Contratos MCP, schemas compactos e famílias](https://github.com/morluto/rea/blob/main/docs/mcp-contracts.md)
- [CLI, evidências e snapshots](https://github.com/morluto/rea/blob/main/docs/cli.md)

Links para `main` são móveis. Ao executar o plano, registrar a versão/commit
consultado e confirmar quais recursos estão no pacote instalado.
