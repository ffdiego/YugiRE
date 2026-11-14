"""Deterministic projections. Structural reach is never a coverage promotion."""
from collections import Counter, defaultdict

from scripts.coverage.validation import SCHEMA, check_shape, validate

AXES = ('identification', 'understanding', 'implementation', 'validation')
GAMES = ('Yugi', 'Kaiba', 'Joey')


def cell(value):
    text = str(value).replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')
    return text.replace('\\', '\\\\').replace('|', '&#124;').replace('\r', '').replace('\n', '<br>')


def table(headers, rows):
    return ['| ' + ' | '.join(map(cell, headers)) + ' |',
            '| ' + ' | '.join('---' for _ in headers) + ' |',
            *['| ' + ' | '.join(map(cell, row)) + ' |' for row in rows], '']


def checked(registry, attributes):
    validate(registry)
    required = {'kind', 'registryVersion', 'fingerprints', 'packSha256', 'evidenceRefs', 'cards', 'summary'}
    if set(attributes) != required or attributes['kind'] != 'ExtractedAttributes' or type(attributes['registryVersion']) is not int or attributes['registryVersion'] != 1:
        raise ValueError('Unsupported attributes format')
    if attributes['fingerprints'] != registry['fingerprints']:
        raise ValueError('Attributes/registry fingerprint mismatch')
    check_shape(attributes['packSha256'], SCHEMA['$defs']['sha256'])
    evidence = {e['id'] for e in registry['evidence']}
    if not set(attributes['evidenceRefs']) <= evidence:
        raise ValueError('Unknown attribute evidence')
    cards = sorted(registry['cards'], key=lambda c: c['identity']['internalIndex'])
    attrs = {}
    fields = {'cardRef', 'properties', 'classCode', 'attack', 'defense', 'level', 'pack', 'availability'}
    for a in attributes['cards']:
        if set(a) != fields or a['cardRef'] in attrs:
            raise ValueError('Invalid/duplicate attribute row')
        for key, maximum in [('properties', 2**32-1), ('pack', 65535)]:
            if type(a[key]) is not int or not 0 <= a[key] <= maximum:
                raise ValueError('Invalid attribute integer')
        p = a['properties']
        expected = dict(classCode=(p >> 20) & 31, attack=((p >> 9) & 511)*10, defense=(p & 511)*10, level=(p >> 25) & 15)
        if any(type(a[k]) is not int or a[k] != v for k, v in expected.items()):
            raise ValueError('Inconsistent attribute decoding')
        if set(a['availability']) != set(GAMES) or any(type(a['availability'][g]) is not bool or a['availability'][g] != bool(a['pack'] & b) for g, b in zip(GAMES, (1, 2, 4))):
            raise ValueError('Inconsistent pack availability')
        attrs[a['cardRef']] = a
    if set(attrs) != {c['id'] for c in cards}:
        raise ValueError('Attributes must match selected registry cards exactly')
    # Raw summary coverage is intentionally NOT used for annotated coverage.
    s = attributes['summary']
    counts = dict(catalogRows=len(cards), distinctResources=len({c['identity']['resourceId'] for c in cards}),
                  callbackRows=len(registry['callbacks']), packUnionRows=sum(bool(a['pack'] & 7) for a in attrs.values()),
                  availabilityRows={g: sum(a['availability'][g] for a in attrs.values()) for g in GAMES})
    if any(s.get(k) != v for k, v in counts.items()):
        raise ValueError('Inconsistent attribute denominators')
    profiles = {}
    for cb in registry['callbacks']:
        signature = tuple(cb['slots'][k] for k in sorted(cb['slots']))
        if cb['id'] != f"callback:{cb['row']}" or (cb['profileId'] in profiles and profiles[cb['profileId']] != signature):
            raise ValueError('Inconsistent callback profile/identity')
        profiles[cb['profileId']] = signature
    for c in cards:
        matching = {cb['id'] for cb in registry['callbacks'] if cb['resourceId'] == c['identity']['resourceId']}
        lookup = c['callbackLookup']
        if lookup['state'] == 'AbsentInTable' and matching or lookup['state'] == 'Present' and set(lookup['callbackRefs']) != matching:
            raise ValueError('Callback lookup contradicts registry table')
    return cards, attrs, counts


def refs(cov):
    return set(r for axis in cov.values() for r in axis['evidenceRefs'])


def states(cov):
    return ' / '.join(cov[a]['state'] for a in AXES)


def coverage_report(registry, attributes):
    cards, attrs, counts = checked(registry, attributes)
    lines = ['# Card Coverage Registry — cobertura', '', f"Baseline: `{registry['baselineCommit']}`.", '',
             f"Escopo: {cell(registry['catalogScope']['kind'])}; {cell(registry['catalogScope']['selection'])}.", '',
             'Estados vêm do registry selecionado. Atributos fornecem somente disponibilidade e denominadores.',
             'Não há percentual geral; domínios, eixos, cartas e vínculos são universos separados.', '', '## Universos', '']
    lines += table(['Universo', 'Registros'], [('Catálogo selecionado completo', counts['catalogRows']),
        ('Recursos distintos no catálogo', counts['distinctResources']), ('União dos três jogos (pack & 7)', counts['packUnionRows']),
        ('Fora da união dos jogos', len(cards)-counts['packUnionRows']),
        *[(g, counts['availabilityRows'][g]) for g in GAMES], ('Tabela de callbacks (inclui órfãos)', counts['callbackRows'])])
    lines += ['As versões se sobrepõem; não somar suas contagens. A união dos packs não substitui o catálogo completo.', '', '## Fingerprints', '']
    lines += table(['Fonte', 'SHA256'], sorted(registry['fingerprints'].items()) + [('card_pack', attributes['packSha256'])])
    lines += ['## Histogramas de cartas', '']
    lines += table(['Eixo', 'Estado', 'Cartas'], [(axis, state, sum(c['coverage'][axis]['state'] == state for c in cards))
        for axis in AXES for state in SCHEMA['$defs'][axis]['properties']['state']['enum']])
    lines += ['## Inventários e buscas de callbacks', '']
    lines += table(['Inventário', 'Cartas'], [(s, sum(c['mechanicInventory'] == s for c in cards)) for s in ('Open', 'Closed', 'Unknown')])
    lines += table(['Busca de callback', 'Cartas'], [(s, sum(c['callbackLookup']['state'] == s for c in cards)) for s in ('Present', 'AbsentInTable', 'Unknown')])
    lines += ['## Mecânicas por domínio e estado', '', 'Mecânicas registradas não são denominador de todas as regras do jogo.', '']
    mechanics = sorted(registry['mechanics'], key=lambda m: m['id'])
    lines += table(['Domínio', 'Mecânicas'], [(d, sum(m['domain'] == d for m in mechanics)) for d in ('Rules', 'AiStats', 'AiDecision')])
    lines += table(['Domínio', 'Eixo', 'Estado', 'Mecânicas'], [(d, axis, s, n) for d in ('Rules', 'AiStats', 'AiDecision') for axis in AXES
        for s, n in sorted(Counter(m['coverage'][axis]['state'] for m in mechanics if m['domain'] == d).items())])
    bindings = [b for c in cards for b in c['mechanics']]
    lines += ['## Vínculos (contados separadamente)', '']
    lines += table(['Eixo', 'Estado', 'Vínculos'], [(axis, s, n) for axis in AXES for s, n in sorted(Counter(b['coverage'][axis]['state'] for b in bindings).items())])
    lines += ['## Cartas, associações e lacunas', '', 'Ordem dos estados: identificação / compreensão / implementação / validação.', '']
    for c in cards:
        lines += [f"### {cell(c['id'])} — {cell(c['identity']['name'])}", '',
            f"Recurso: {c['identity']['resourceId']}; inventário: {c['mechanicInventory']}; packs: {', '.join(g for g in GAMES if attrs[c['id']]['availability'][g]) or 'nenhum'}.", '',
            f"Callbacks: {c['callbackLookup']['state']} ({', '.join(sorted(c['callbackLookup']['callbackRefs'])) or 'nenhum'}); evidências: {', '.join(sorted(c['callbackLookup']['evidenceRefs'])) or 'Unknown'}.", '']
        lines += table(['Eixo', 'Estado', 'Escopo', 'Evidências', 'Limitações'], [(axis, c['coverage'][axis]['state'], c['coverage'][axis]['scope'],
            ', '.join(sorted(c['coverage'][axis]['evidenceRefs'])) or 'Unknown', '; '.join(c['coverage'][axis]['limitations']) or 'nenhuma registrada') for axis in AXES])
        lines += table(['Mecânica', 'Relação', 'Quatro eixos', 'Evidências do vínculo e eixos', 'Escopos e limitações'], [(b['mechanicRef'], b['relation'], states(b['coverage']),
            ', '.join(sorted(set(b['evidenceRefs']) | refs(b['coverage']))) or 'Unknown',
            '; '.join(f"{axis}: {b['coverage'][axis]['scope']} — {', '.join(b['coverage'][axis]['limitations'])}; evidências: {', '.join(sorted(b['coverage'][axis]['evidenceRefs'])) or 'Unknown'}" for axis in AXES)) for b in sorted(c['mechanics'], key=lambda b: b['mechanicRef'])])
        lines += ['Perguntas abertas: ' + cell('; '.join(c['openQuestions']) or 'nenhuma registrada; não atesta completude'), '']
    lines += ['## Definições de mecânicas', '']
    for m in mechanics:
        lines += [f"### {cell(m['id'])}", '', cell(m['title'] + ' — ' + m['scope']), '',
            'Dependências: ' + cell(', '.join(sorted(m['dependencies'])) or 'nenhuma registrada'), '',
            'Perguntas abertas: ' + cell('; '.join(m['openQuestions']) or 'nenhuma registrada'), '']
        lines += table(['Eixo', 'Estado', 'Escopo', 'Evidências', 'Limitações'], [(axis, m['coverage'][axis]['state'], m['coverage'][axis]['scope'],
            ', '.join(sorted(m['coverage'][axis]['evidenceRefs'])) or 'Unknown', '; '.join(m['coverage'][axis]['limitations']) or 'nenhuma registrada') for axis in AXES])
    lines += ['## Ledger de evidências', '']
    for e in sorted(registry['evidence'], key=lambda e: e['id']):
        loc = e['location']
        url = f"https://github.com/{loc['repository']}/blob/{loc['commit']}/{loc['path']}"
        lines += [f"### {cell(e['id'])} ({e['kind']})", '', f"Fonte: [{cell(loc['path'])}]({url}); commit `{loc['commit']}`; SHA256 `{loc['sha256']}`.", '',
            'Localizador: ' + cell(loc['locator']), '', cell(e['claim']), '', 'Limitações: ' + cell('; '.join(e['limitations'])), '']
        if e['capture'] is not None:
            lines += ['Captura: ' + cell('; '.join(f'{k}={v if v is not None else "null"}' for k, v in sorted(e['capture'].items()))), '']
    return ('\n'.join(lines).rstrip() + '\n').encode('utf-8')


def candidates(registry, attributes):
    cards, attrs, _ = checked(registry, attributes)
    groups = defaultdict(list)
    for cb in registry['callbacks']:
        groups[cb['profileId']].append(cb)
    result = []
    mechanics = {m['id']: m for m in registry['mechanics']}
    def make(id, callbacks, members, structural):
        evidence = set()
        mids = set()
        for c in members:
            evidence |= refs(c['coverage'])
            for b in c['mechanics']:
                mids.add(b['mechanicRef'])
                evidence |= set(b['evidenceRefs']) | refs(b['coverage'])
                evidence |= refs(mechanics[b['mechanicRef']]['coverage'])
        availability = {g: sum(attrs[c['id']]['availability'][g] for c in members) for g in GAMES}
        row = dict(id=id, callbackRefs=sorted(cb['id'] for cb in callbacks), structuralEvidence=sorted({e for cb in callbacks for e in cb['evidenceRefs']}), callbackRows=len(callbacks), cards=[c['id'] for c in members],
            distinctResources=len({c['identity']['resourceId'] for c in members}), structural=structural,
            packUnion=sum(bool(attrs[c['id']]['pack'] & 7) for c in members), availability=availability,
            evidence=sorted(evidence), mechanics=sorted(mids), gain='Unknown',
            unresolved=sum(bool(c['openQuestions']) or any(c['coverage'][axis]['state'] in ('Unknown', 'Partial', 'Conflicting', 'NotImplemented') for axis in ('understanding', 'implementation')) for c in members),
            behavior=Counter(c['coverage']['understanding']['state'] for c in members),
            implementation=Counter(c['coverage']['implementation']['state'] for c in members),
            questions=sorted({q for c in members for q in c['openQuestions']} | {q for mid in mids for q in mechanics[mid]['openQuestions']}))
        result.append(row)
    for profile, callbacks in sorted(groups.items()):
        cbids = {cb['id'] for cb in callbacks}
        members = [c for c in cards if cbids.intersection(c['callbackLookup']['callbackRefs'])]
        # All-null profiles preserve records but provide no shared executable address.
        structural = any(p is not None for cb in callbacks for p in cb['slots'].values())
        make('profile:' + profile, callbacks, members, structural)
    for c in cards:
        if c['callbackLookup']['state'] != 'Present' and c['mechanics']:
            make(c['id'], [], [c], False)
    # Explicit lexicographic queue: reach, table size, pack reach, versions, existing evidence, mechanics, stable ID.
    result.sort(key=lambda r: (-int(r['structural']), -len(r['cards']) if r['structural'] else 0,
        -r['callbackRows'] if r['structural'] else 0, -r['packUnion'], -sum(n > 0 for n in r['availability'].values()),
        -len(r['evidence']), -len(r['mechanics']), -r['unresolved'], r['id']))
    return result


def priorities_report(registry, attributes):
    rows = candidates(registry, attributes)
    lines = ['# Card Coverage Registry — próximas investigações', '',
        f"Baseline: `{registry['baselineCommit']}`.", '',
        'Fila estrutural, não ranking de implementação. Cada profile agrupa exatamente os cinco ponteiros; não representa uma família semântica.',
        'Potencial de reutilização: alcance estático observado. Ganho futuro comprovável: Unknown para todos; nenhuma investigação foi executada.', '',
        'Ordenação lexicográfica: ponteiro não nulo conhecido primeiro; cartas distintas (linhas), registros de callback, união dos packs, número de versões, quantidade de evidências semânticas registradas, mecânicas registradas, cartas a esclarecer e ID ordinal.',
        'Para candidatos sem ponteiro executável compartilhado, alcance estrutural é Unknown e não participa dos dois critérios de tamanho.',
        'Evidências e mecânicas são apenas desempates de contexto; quantidade não mede qualidade. Adequação científica das evidências e esforço/custo: Unknown. Lacunas são exibidas por estado, sem pesos inventados.',
        'Estados Unknown não são ausência de efeito/implementação. Profiles órfãos e nulos ficam visíveis. Cartas sem callback e sem anotação continuam como pool de auditoria fora desta fila.', '',
        f"Pool fora da fila (sem callback presente e sem mecânicas): {sum(c['callbackLookup']['state'] != 'Present' and not c['mechanics'] for c in registry['cards'])} cartas.", '',
        'Cartas a esclarecer: perguntas abertas ou compreensão/implementação Unknown, Partial, Conflicting ou NotImplemented. É demanda de investigação, não prova de defeito; usada somente como último desempate numérico.', '',
        '## Candidatos', '']
    lines += table(['Posição', 'Candidato', 'Registros callback', 'Cartas distintas / recursos', 'Potencial estrutural', 'União packs', 'Yugi / Kaiba / Joey', 'Evidências semânticas', 'Mecânicas', 'Compreensão (estado:contagem)', 'Implementação (estado:contagem)', 'Cartas a esclarecer', 'Ganho comprovável'],
        [(i, r['id'], r['callbackRows'], f"{len(r['cards'])} / {r['distinctResources']}", len(r['cards']) if r['structural'] else 'Unknown',
          r['packUnion'], ' / '.join(str(r['availability'][g]) for g in GAMES), ', '.join(r['evidence']) or 'Unknown',
          ', '.join(r['mechanics']) or 'Unknown', ', '.join(f'{s}:{n}' for s, n in sorted(r['behavior'].items())) or 'Unknown',
          ', '.join(f'{s}:{n}' for s, n in sorted(r['implementation'].items())) or 'Unknown', r['unresolved'], r['gain']) for i, r in enumerate(rows, 1)])
    lines += ['## Escopo e perguntas por candidato', '']
    for r in rows:
        lines += [f"### {r['id']}", '', 'Cartas: ' + cell(', '.join(r['cards']) or 'nenhuma no catálogo selecionado'), '',
            'Callbacks: ' + cell(', '.join(r['callbackRefs']) or 'nenhum') + '; evidências estruturais: ' + cell(', '.join(r['structuralEvidence']) or 'Unknown'), '',
            'Perguntas: ' + cell('; '.join(r['questions']) or 'Unknown'), '']
    lines += ['## Fontes dos critérios', '']
    used = {e for r in rows for e in r['evidence'] + r['structuralEvidence']}
    for e in sorted(registry['evidence'], key=lambda e: e['id']):
        if e['id'] in used:
            loc = e['location']
            lines += [f"- {cell(e['id'])}: {e['kind']}; [{cell(loc['path'])}](https://github.com/{loc['repository']}/blob/{loc['commit']}/{loc['path']}); SHA256 `{loc['sha256']}`; {cell(loc['locator'])}. Limitações: {cell('; '.join(e['limitations']))}", '']
    lines += ['## Limites', '',
        'Tamanho de grupo não garante reaproveitamento de regras nem ganho de cobertura. A disponibilidade conta linhas do catálogo, sem fundir duplicatas ou somar versões sobrepostas.',
        'As referências semânticas resolvem no ledger do registry e do coverage-report.md. A proveniência estrutural vem dos evidenceRefs dos callbacks; nenhuma afirmação semântica é derivada desses refs.', '']
    return ('\n'.join(lines).rstrip() + '\n').encode('utf-8')
