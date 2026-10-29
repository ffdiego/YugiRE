# Executável

Alvo e convenções: [visão geral](00-overview.md). Evidência primária: [cabeçalhos/imports](evidence/initial-inspection.json), [strings ASCII com offsets](evidence/exe-ascii-strings.json), [recursos](evidence/pe-resources.json).

## Cabeçalhos — confiança alta

`MZ`; PE em offset de arquivo `0x900`, assinatura `50 45 00 00`; Machine `0x014C` (x86), OptionalHeader `0x010B` (PE32). Image base `0x00400000`; entrada RVA `0x001C5E58`, VA `0x005C5E58`. Subsystem 2 (Windows GUI). Timestamp declarado: `2004-05-21 04:10:44 UTC`, não prova de data real de compilação. Linker 6.0. DLL characteristics zero.

| Seção | RVA | Offset de arquivo | Tamanho no arquivo | Tamanho virtual | Entropia bits/byte |
|---|---:|---:|---:|---:|---:|
| .text | 0x1000 | 0x1000 | 0x1D9000 | 0x1D9000 | 6,567 |
| .rdata | 0x1DA000 | 0x1DA000 | 0x11000 | 0x102DC | 4,355 |
| .data | 0x1EB000 | 0x1EB000 | 0x8000 | 0x46E950 | 4,324 |
| .rsrc | 0x65A000 | 0x1F3000 | 0x4000 | 0x3660 | 4,384 |

`.text` é leitura/execução; `.data` leitura/escrita; demais leitura. A grande diferença entre tamanho virtual e físico de `.data` sugere muita memória global inicializada com zero (média), ainda sem associá-la ao duelo. A última seção termina exatamente no fim do arquivo: sem overlay após as seções (alta).

Diretórios de exportação, relocação, debug, TLS, security e CLR zerados. Nenhuma exportação PE declarada. Não confundir a entrada do programa com uma exportação DLL. Imports normais de oito DLLs; lista completa na evidência.

## Tecnologias e pistas

| Pista observada | Interpretação | Confiança |
|---|---|---|
| Linker 6.0; string `Microsoft Visual C++ Runtime Library` em offset 1960008 | Microsoft Visual C++ provável; possível CRT estaticamente ligado, sem import MSVCRT | Média |
| USER32: CreateWindowExA, PeekMessageA, DispatchMessageA | Janela e eventos Win32 | Alta para imports; média para arquitetura |
| GDI32: CreateDIBSection, TextOutW, CreateFontA | Operações gráficas/texto GDI; não comprova renderizador principal | Média |
| `dinput.dll`, string contendo `DirectInputCreateA` | Provável resolução dinâmica de DirectInput | Média |
| `InitDirectShow Failed`, `CSound::Load`, `kmMmioFile::open` | Camada de mídia/áudio candidata; strings não são símbolos de função | Média |
| MSACM32: acmStreamOpen/Convert/Size | Conversão de áudio via ACM | Alta para imports; codec desconhecido |
| WINMM: joyGetPosEx, timeGetTime | Joystick e temporização | Média |
| KERNEL32: CreateFileA, ReadFile, SetFilePointer | Pontos de partida para leitores de arquivos | Alta |
| ADVAPI32 e caminho SOFTWARE\\KONAMI\\Yu-Gi-Oh! Power Of Chaos | Uso de registro/configuração provável | Média |

Sem evidência suficiente nesta rodada para confirmar DirectDraw, Direct3D ou o mecanismo principal de renderização. COM e strings de mídia não comprovam tecnologia de todo o motor.

## Recursos e proteção

Árvore de recursos: sete RT_ICON (tipo 3), um bloco RT_STRING (tipo 6), quatro RT_GROUP_ICON (tipo 14: MAIN, Z_001, Z_002, Z_003), todos com identificador de idioma 1041. Não há RT_VERSION enumerado. Conteúdo visual não extraído.

Seções convencionais, imports amplos e código analisável favorecem ausência de empacotamento integral óbvio (média). Entropia de `.text` não parece a de um fluxo uniformemente comprimido; isso **não exclui** proteção localizada, modificações anteriores, compressão dos recursos ou verificações anticópia. A procedência exata deste executável não foi autenticada.

Avisos do loader sobre TLS/exports de DLLs do sistema não devem ser atribuídos ao alvo: seu diretório TLS é zero. Resultados do decompilador e identificações automáticas precisam de verificação local antes de conclusões sobre lógica própria.
