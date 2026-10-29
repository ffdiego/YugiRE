# Ambiente Fedora — 2026-10-29

## Atualização: Ghidra 12.2 validado

Instalação oficial extraída em `../../tools/ghidra_12.2_PUBLIC/`.
O ZIP `ghidra_12.2_PUBLIC_20261016.zip` passou na verificação SHA256:
`2c2cd776db3c1f6a097ba418922378ac3649cadd5e844a6f5f088ae628a7be67`.
Após o usuário instalar o JDK Fedora, `java` e `javac` reportam 25.0.4.1.
A abertura de uma cópia do projeto com `-noanalysis -readOnly` funcionou:
o script verificou o SHA256 armazenado do executável e contou **6718 funções**.
Log: `scratch/ghidra-12.2-check.TBfX3S/headless.log`.

O Flatpak continua instalado: a tentativa de remoção autorizada pelo usuário
falhou com `Flatpak system operation Uninstall not allowed for user`.
Para concluir no terminal: `sudo flatpak uninstall org.ghidra_sre.Ghidra`.
Para iniciar a instalação nova, a partir da raiz do workspace:
`./tools/ghidra_12.2_PUBLIC/ghidraRun`.

As seções abaixo registram o diagnóstico anterior à instalação do JDK.

Verificação local, sem executar o jogo nem modificar os originais ou o banco
persistente do Ghidra.

## Ambiente encontrado

- Fedora Linux 44 Workstation, x86_64; OpenJDK do host 25.0.4.1.
- Ghidra Flatpak instalado no sistema: `org.ghidra_sre.Ghidra`, versão
  **12.1.3**, branch stable, origem flathub, runtime Freedesktop SDK 26.08.
- Permissões declaradas do Flatpak incluem acesso a `home` e socket X11.
- `pwsh` não encontrado no PATH. Os scripts PowerShell existentes ainda
  dependem dele; os wrappers de Ghidra também referenciam a distribuição
  Windows `ghidra_12.2` e `analyzeHeadless.bat`, ausente neste workspace.
- O repositório Git está em `YugiRE/`, com scripts, testes e documentação.
  O `AGENTS.md` da raiz descreve o ambiente antigo e precisa ser lido com
  essa diferença em mente.

## Projeto trazido do Windows

Local: `ghidra-projects/Joey.gpr` acompanhado de `Joey.rep/`, aproximadamente
39 MiB. O registro `idata/00/00000000.prp` identifica um programa
`/joey_pc.exe`; o banco está em `idata/00/~00000000.db/db.1.gbf`.
É necessário preservar tanto `.gpr` quanto `.rep/`.

Copiamos o projeto para `scratch/fedora-check/projects/` e solicitamos
abertura com Ghidra 12.1.3, `-process joey_pc.exe -noanalysis -readOnly`.
Resultado do headless:

```text
ERROR /joey_pc.exe: this file was created with a newer version of Ghidra, and can not be processed.
```

O processo retornou código zero apesar desse erro: o log deve ser conferido,
não apenas o status do shell. O teste confirma incompatibilidade de versão;
não permite concluir sobre a integridade lógica completa do programa salvo.
A documentação anterior registra criação com Ghidra **12.2**. Para retomar
essa análise salva, usar essa versão ou uma posterior compatível, começando
por uma cópia de segurança do projeto.

## Limites da execução nesta sessão

`flatpak run --command=sh org.ghidra_sre.Ghidra ...` falhou com
`Unable to allocate instance id` no ambiente restrito do agente. Isso não
comprova falha ao abrir o aplicativo numa sessão normal do desktop.

A chamada direta ao `support/analyzeHeadless` do pacote no host também
falhou: `support/launch.properties` é um link para
`/var/config/ghidra.properties`, resolvido dentro do sandbox Flatpak.
Para o teste de compatibilidade, iniciamos as classes Java instaladas pelo
pacote diretamente com o Java do host, usando `Utility.jar`,
`-Djava.system.class.loader=ghidra.GhidraClassLoader`,
`ghidra.Ghidra ghidra.app.util.headless.AnalyzeHeadless`.
Preferências, cache e logs foram redirecionados a `scratch/fedora-check/`.
O log está em `scratch/fedora-check/headless.log` (não versionado).
Não foi validada a interface gráfica nem a descompilação nativa neste teste.

## Preservação e continuidade

SHA256 e tamanho de `joey_pc.exe`, `data.dat`, `Voice.dat`, `Region.dat` e
`deck.ydc` conferem integralmente com `evidence/initial-inspection.json`.
SHA256 do executável:
`DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE`.

A sexta rodada documentada já confirmou nomes ingleses e IDs no deck.
A próxima investigação prevista é seguir os callers de VA `0x00570BE0`
(ID associado a Parasite Paracide), com poucas funções por rodada.
Antes disso, resolver a versão do Ghidra e adaptar os comandos de consulta
para Linux. Não é necessário reimportar o executável para diagnosticar a
incompatibilidade atual.
