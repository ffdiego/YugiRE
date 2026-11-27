# Handoff — runtime visual no Proton/GDB (27/11)

## Objetivo único
Fechar V17 por famílias de compositor, com evidência reutilizável no
LookAndFeel. Ler AGENTS.md, PLANS.md e docs/plans/VISUALS.md; seguir
docs/research/joey-proton-validation.md e scripts/regression/README.md.
Agente único, sem esperar confirmação a cada cenário; mudar para o
próximo cenário independente se bloqueado. Nenhum recurso proprietário
ou prefixo Wine no Git.

## Prioridade
1. **Composição e máscaras compartilhadas.** Capturar receptor/vtable,
   formatos (origem/destino), pitch, chave, retângulos, parâmetros,
   pixels pré/pós, clipping e ordem das operações, em pares reproduzíveis.
   Começar em um caminho já reproduzível: seleção cemitério, espada e
   ATK/DEF com roteiros existentes (não repetir evidência já fechada;
   coletar pixels finais pareados do cliente e original). Para máscaras,
   observar Affect +94 e Vija +98; 005DC6EC+94=0041AC90,
   +98=004CCD40, +A0=004EEC70, +50=0054D820. Confirmar receptor
   ativo antes de inferir semântica. Coin usa +94/+A0; Affect e Vija
   têm planos de scratch distintos. Evitar substituir operações por
   alpha Raylib sem confronto. Se Vija for difícil de provocar, coletar
   estático 59 e avançar a outro efeito de máscara acessível.
2. **Equipamento, break e exclusão.** Roteiros 56/57 e 34; verificar
   B2, gate +4, Draw terminal, momento de atributos, comparação visual,
   sound request; distinguir 79/7A/7C e banimento comum de V26.
   Para Affect, Tiger Axe equipado, cursor sobre cada lado do vínculo,
   Dragon Capture Jar com Dragon, Spellbinding Circle, campo, TER.
   Roteiro 58 resolve dados estáticos que faltam; evitar repetir análise
   de callback já indexado.
3. **Eventos com produtor incerto.** Capturar quatro palavras cruas
   de E0/E1/E2/E3/E4 e contexto de Time Wizard/Barrel Dragon/
   Graceful Dice/Skull Dice, resultado, âncora, gate e som. Coletar 0A/0B
   corrente e B1 Jinzo em um duelo com resposta, não introduzir
   esperas artificiais. Cenários negativos também contam como evidência
   limitada; ausência de objeto não prova ausência universal.
4. **Cinemáticas/áudio.** Selecionar reações, signature e Vija quando
   as famílias anteriores estiverem fechadas ou bloqueadas. Capturar
   áudio real e -nosound, IDs de Voice.dat, predicados de término
   e ordem. Cadência sem breakpoint para tempo real; contador de
   updates/draws e eventos para comparação sem depender de wall time.

## Critério de aceite
Uma fatia pequena: cenário reproduzível + contrato observado + teste
automatizado de geometria/pixels/gate + implementação compartilhada
quando possível, ou lacuna explícita com próximo comando. Registrar
fingerprint do EXE, versão do cliente, perfil de velocidade, perspective,
setup e limites da amostra. Capturas/JSONL e SQLite ficam locais;
versionar apenas resumo reproduzível, scripts e testes sintéticos.
Priorizar uso no duelo real, não elevar cobertura por review isolado;
não alterar regras para satisfazer pixel diff. Não tratar tempo de
breakpoint como duração física. Não promover Verified sem comparar
o escopo completo da rotina.

Enquanto esta frente roda, análise estática independente pelo chat
pode recuperar os ramos ainda faltantes no SQLite, mas evitar alterações
concorrentes nos mesmos arquivos.
