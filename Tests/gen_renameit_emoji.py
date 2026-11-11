# Writes Tests/suites/DrakesRenameIt-Emoji: do emoji survive on Piece of Paper the way they do on a Sign?
#  EM-01  control: a sign's widget draws every glyph of the sample text (reports which font supplied each).
#  EM-02  a written page: the text round-trips through the item, and the pinned page draws every glyph.
#  EM-03  the Libs rich-text length/truncate: an emoji is 2 UTF-16 chars; a cut must never split the pair.
# Run: python gen_renameit_emoji.py
import json, os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesRenameIt-Emoji')
os.makedirs(OUT, exist_ok=True)

# astral emoji (surrogate pairs), a text-presentation symbol with variation selector, a plain BMP symbol, and some words
EMO = 'Hi \U0001F600 \U0001F525 ❤️ ⚔️ ok'

def hook(name, label=None, **kw):
    s = {'do': 'hook', 'name': 'renameit.' + name}
    args = {k: v for k, v in kw.items() if k not in ('expect', 'contains', 'notContains')}
    if args:
        s['args'] = args
    for k in ('expect', 'contains', 'notContains'):
        if k in kw:
            s[k] = kw[k]
    if label:
        s['label'] = label
    return s

NOERR = {'check': 'noErrors', 'source': 'DrakesRenameit'}
PIN_FLAT = {'do': 'setConfig', 'values': {'10 Paper/BlankPaperPlaceOrientation': 'Horizontal Only'}}

tests = {
    'EM-01': {
        'name': 'EM-01 a sign draws the sample emoji (control)',
        'description': 'Puts the sample text on a sign\'s widget and lists, per character, which font supplied the glyph (NONE = missing-glyph box).',
        'steps': [
            {'do': 'spawn', 'prefab': 'sign', 'dz': 3, 'owner': 'me', 'as': 'sign'},
            {'do': 'wait', 'seconds': 0.5},
            hook('glyphProbe', 'sign glyphs (report)', source='sign', text=EMO, target='sign', contains='glyphs='),
            hook('glyphProbe', 'the sign draws every glyph', source='sign', text=EMO, target='sign', notContains='NONE'),
        ],
        'asserts': [NOERR],
    },
    'EM-02': {
        'name': 'EM-02 a pinned written page draws the sample emoji',
        'description': 'Writes a page whose text has emoji, pins it, checks the stored text and which glyphs the page widget draws.',
        'steps': [
            PIN_FLAT,
            'writePage("Emoji Page", %s)' % json.dumps(EMO, ensure_ascii=False),
            {'do': 'pinPage'},
            hook('pageDesc', 'the text kept its emoji', contains=EMO),
            {'do': 'wait', 'seconds': 0.6},
            hook('glyphProbe', 'page glyphs (report)', source='page', contains='glyphs='),
            hook('glyphProbe', 'the page draws every glyph', source='page', notContains='NONE'),
        ],
        'asserts': [NOERR],
    },
    'EM-04': {
        'name': 'EM-04 the editors (UI Toolkit font) can draw the sample emoji',
        'description': 'The paper editor, rename windows and tooltips fields use the Averia UI Toolkit font; checks it (with its fallbacks) has every sample glyph, and lists the sign font fallbacks a UI fallback could be built from.',
        'steps': [
            {'do': 'spawn', 'prefab': 'sign', 'dz': 3, 'owner': 'me', 'as': 'sign'},
            hook('uiGlyphs', 'UI font report', text=EMO, target='sign', contains='uiFont='),
            hook('uiGlyphs', 'the editor font draws every glyph', text=EMO, target='sign', notContains=':NO'),
        ],
        'asserts': [NOERR],
    },
    'EM-03': {
        'name': 'EM-03 length limit and cut never split an emoji',
        'description': 'RichText.VisibleLength / Truncate work in UTF-16 chars: an emoji counts twice, and a cut at the limit must not leave half a pair.',
        'steps': [
            hook('richLength', 'report: cut at 3', text=EMO, max=3, contains='visible='),
            hook('richLength', 'cut at 4 keeps whole pairs', text=EMO, max=4, contains='brokenPair=False'),
            hook('richLength', 'cut at 5 keeps whole pairs', text=EMO, max=5, contains='brokenPair=False'),
            hook('richLength', 'cut at 6 keeps whole pairs', text=EMO, max=6, contains='brokenPair=False'),
        ],
        'asserts': [NOERR],
    },
}

with open(os.path.join(OUT, 'suite.json'), 'w', encoding='utf-8') as f:
    json.dump({
        'name': 'DrakesRenameIt-Emoji',
        'description': 'Emoji on Piece of Paper compared with a Sign: glyph probe, stored text, length limits.',
        'include': ['../DrakesRenameIt/_shared/baseline.json', '../DrakesRenameIt/_functions'],
        'resetBetweenTests': True,
    }, f, indent=2)
for k, v in tests.items():
    with open(os.path.join(OUT, k + '.json'), 'w', encoding='utf-8') as f:
        json.dump(v, f, indent=2, ensure_ascii=False)
print('wrote', len(tests), 'tests')
