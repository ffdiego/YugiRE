# C04 — Equipamentos P025 de bônus fixo

Fatia de 18 novos recursos dos 771 packs, além dos três anteriores. SQLite
privado consultado somente leitura; PRAGMA quick_check = ok. Fingerprint
do executável: db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.

O filtro 00579CE0 contém os predicados específicos por tipo/atributo
dinâmico; 0056FD40 contém os deltas já implementados em NativeAiAttributes.
Reutilizar o avaliador, não duplicar os deltas no motor. A whitelist
não deriva apenas do perfil P025. Ver [inventário completo](p025-preliminary.md)
e [ciclo original do sandbox](p025-implementation.md).

| ID | Carta | Tipo | Atributo | ΔATK | ΔDEF |
| --- | --- | ---: | ---: | ---: | ---: |
| 302 | Dark Energy | 3 | — | 300 | 300 |
| 304 | Laser Cannon Armor | 10 | — | 300 | 300 |
| 306 | Elf's Light | — | 1 | 400 | -200 |
| 308 | Steel Shell | — | 3 | 400 | -200 |
| 309 | Vile Germs | 13 | — | 300 | 300 |
| 311 | Silver Bow and Arrow | 17 | — | 300 | 300 |
| 314 | Dragon Treasure | 1 | — | 300 | 300 |
| 315 | Electro-Whip | 19 | — | 300 | 300 |
| 318 | Mystical Moon | 12 | — | 300 | 300 |
| 321 | Violet Crystal | 2 | — | 300 | 300 |
| 322 | Book of Secret Arts | 18 | — | 300 | 300 |
| 324 | Machine Conversion Factory | 7 | — | 300 | 300 |
| 325 | Raise Body Heat | 9 | — | 300 | 300 |
| 326 | Follow Wind | 16 | — | 300 | 300 |
| 327 | Power of Kaishin | 14 | — | 300 | 300 |
| 653 | Salamandra | — | 4 | 700 | 0 |
| 1012 | Burning Spear | — | 4 | 400 | -200 |
| 1013 | Gust Fan | — | 6 | 400 | -200 |

Todos são magias (class 22) no catálogo de callbacks. IDs conferidos
em pack-index.json: 18 presentes na união, 5 no pack Joey. 305/323 também
possuem bônus conhecidos, mas não constam dos packs jogáveis e não
entram nesta fatia. Não generalizar para 316/667 (escopo a revisar),
Metalmorph, Megamorph, Sword of Dragon's Soul, Lightning Blade ou efeitos
com upkeep, restrição de ataque ou contagens contextuais.

A mudança em EquipmentRules permite que DemoDuel.EquipmentEligibility e
Equip, a seleção existente no DuelScreen e a CPU reconheçam esses IDs.
O fluxo de mão existente valida alvo face-up e guarda genérica declarada,
mantém vínculo e calcula ATK/DEF a cada leitura. A retirada comum reutiliza
o mecanismo anterior. Não modifica Demo.cs, não cria uma fila paralela.
A02 ainda está em PR #67; a integração legada deve migrar em C12.
Não afirmar equivalência nativa de supressão, remapeamento dinâmico,
mudança de controle, seleção por identidade ou ordem de retirada.
Set de equipamento continua não implementado.

Testes C# adicionados à suíte EquipmentChecks para 18 IDs, predicado
positivo/negativo, guarda desconhecida, alvo oculto, ativação em ambos
os lados, bônus independentes e retirada. SDK .NET indisponível nesta
sessão; execução pendente. O manifesto marca os novos IDs Partial,
sem mudança de Done/Verified ou badge. C04 continua Parcial.
