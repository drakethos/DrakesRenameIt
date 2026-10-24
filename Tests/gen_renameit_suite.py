# Writes Tests/suites/DrakesRenameIt (automated) and DrakesRenameIt-Manual. Repeated step runs live as DrakesTestLab
# DrakesTestLab functions in DrakesRenameIt/_functions/ (called like steps: "writePage(\"X\")" or {"do": "pinPage", ...});
# the config baseline both suites share is
# DrakesRenameIt/_shared/baseline.json (pulled in with "include"). Run: python gen_renameit_suite.py
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites')
AUTO = os.path.join(OUT, 'DrakesRenameIt')
MANUAL = os.path.join(OUT, 'DrakesRenameIt-Manual')
GUID = 'com.drakemods.renameit'

# ---------------------------------------------------------------- helpers

def H(name, expect=None, label=None, contains=None, notContains=None, stop=False, **args):
    s = {'do': 'hook', 'name': 'renameit.' + name}
    if args:
        s['args'] = args
    if expect is not None:
        s['expect'] = expect
    if contains is not None:
        s['contains'] = contains
    if notContains is not None:
        s['notContains'] = notContains
    if label:
        s['label'] = label
    if stop:
        s['stop'] = True
    return s

def give(prefab, count=1, crafter=None):
    # owned items are CRAFTED through the game's crafting code (real crafter id/name), never faked
    if crafter:
        s = {'do': 'craft', 'prefab': prefab}
        if count > 1:
            s['count'] = count
        if crafter != 'me':
            s['by'] = crafter
        return s
    return {'do': 'give', 'prefab': prefab, 'count': count}

def cfg(**pairs):
    return {'do': 'setConfig', 'values': {k.replace('__', '/').replace('_', ' ', 1) if False else k: v for k, v in pairs.items()}}

def C(values):
    return {'do': 'setConfig', 'values': values}

def play(who):
    return {'do': 'playAs', 'who': who}

def wait(s):
    return {'do': 'wait', 'seconds': s}

def shot(name):
    return {'do': 'screenshot', 'name': name}

def log(msg):
    return {'do': 'log', 'message': msg}

def check(checkName, label=None, **f):
    s = {'do': 'check', 'check': checkName}
    s.update(f)
    if label:
        s['label'] = label
    return s

def U(fn, **args):
    # call a function from _functions/. Args that are the first params in order -> the short form 'fn("a", 2)';
    # otherwise the step form {"do": fn, ...args}
    order = [p if isinstance(p, str) else next(iter(p)) for p in BLOCKS[fn].get('params', [])]
    if list(args) == order[:len(args)]:
        return f"{fn}({', '.join(json.dumps(v, ensure_ascii=False) for v in args.values())})"
    return {'do': fn, **args}

def P(name):
    # a block parameter: "{{name}}" (alone in a string it passes the value as is: numbers, true, objects)
    return '{{' + name + '}}'

NOERR = {'check': 'noErrors', 'source': 'DrakesRenameit'}

VIP = {'01 Admin/AllowAdminOverride': 'true', '01 Admin/VipOnlyOverride': 'true', '01 Admin/VipList': 'Nacho-VIP'}

# ---------------------------------------------------------------- functions (written to DrakesRenameIt/_functions/<name>.json)
# params in call order: a plain name is required, {name: default} is optional; None = left out (a key that is only "{{param}}" is dropped)

def guarded(param, step):
    # the step only runs when the param has a value
    return {'if': P(param), **step}

BLOCKS = {
    'craftRenamed': {
        'description': 'Craft an item through the game (real owner) and rename it; optionally give it a description too.',
        'params': ['text', {'desc': None}, {'prefab': 'AxeStone'}, {'by': None}],
        'actions': [
            {'do': 'craft', 'prefab': P('prefab'), 'by': P('by')},
            H('edit', True, prefab=P('prefab'), op='name', text=P('text')),
            guarded('desc', H('edit', True, prefab=P('prefab'), op='desc', text=P('desc'))),
        ]},
    'editAll': {
        'description': 'Set name, description and crafted-by on an item (each edit must succeed).',
        'params': ['name', 'desc', 'craftedBy', {'prefab': 'AxeStone'}],
        'actions': [H('edit', True, prefab=P('prefab'), op=op, text=P(op)) for op in ('name', 'desc', 'craftedBy')]},
    'checkAll': {
        'description': 'Check the item shows the given name, description and crafted-by.',
        'params': ['name', 'desc', 'craftedBy', {'prefab': 'AxeStone'}],
        'actions': [H(op, contains=P(op), prefab=P('prefab')) for op in ('name', 'desc', 'craftedBy')]},
    'writePage': {
        'description': 'Turn a blank sheet into a written page named text (stops the test if that fails); desc = page text.',
        'params': ['text', {'desc': None}, {'blanks': 1}],
        'actions': [
            {'do': 'give', 'prefab': 'Drakes_PieceOfPaper', 'count': P('blanks')},
            H('edit', True, 'write the page', stop=True, prefab='Drakes_PieceOfPaper', op='name', text=P('text')),
            guarded('desc', H('edit', True, 'page text', prefab='Drakes_Paper_Written', match=P('text'), op='desc', text=P('desc'))),
        ]},
    'pinPage': {
        'description': 'Pin a page from the inventory 2 m ahead (place-mode code) and let it settle.',
        'params': [{'prefab': 'Drakes_Paper_Written'}, {'label': None}, {'dz': 2}],
        'actions': [H('clearPages'), H('pin', True, P('label'), stop=True, prefab=P('prefab'), dz=P('dz')), wait(1.0)]},
    'itemOnStand': {
        'description': 'Spawn a horizontal item stand (tag "stand"), face it and put the item on it.',
        'params': [{'prefab': 'AxeStone'}, {'label': None}],
        'actions': [
            {'do': 'place', 'prefab': 'itemstandh', 'dz': 2.5, 'as': 'stand'},
            {'do': 'face', 'target': 'stand'},
            H('standPut', True, P('label'), stop=True, prefab=P('prefab'), target='stand'),
            wait(0.5),
        ]},
}

tests = {}

def T(tid, title, steps, asserts=None, desc='', setup=None, sheet=None, timeout=None):
    t = {'name': f'{tid} {title}', 'description': (f'Sheet: {sheet}. ' if sheet else '') + desc, 'steps': steps,
         'asserts': (asserts or []) + [NOERR]}
    if timeout:
        t['timeout'] = timeout
    if setup:
        t['setup'] = setup
    tests[tid] = t

# ---------------------------------------------------------------- SMOKE

T('SM-001', 'open rename menu', [
    give('AxeStone', crafter='me'),
    H('owner', label='crafting made you the owner', notContains='Alice', stop=True, prefab='AxeStone'),
    {'do': 'craft', 'prefab': 'AxeFlint', 'by': 'Alice'},
    H('owner', 'Alice', 'crafted by Alice = owned by Alice', prefab='AxeFlint'),
    H('menuOpens', True, 'menu opens for your own crafted axe', prefab='AxeStone'),
    H('tooltip', label='inventory tooltip', prefab='AxeStone'),
    H('menuOpens', False, 'not for the axe Alice made', prefab='AxeFlint'),
], sheet='SM-001', desc='Items are crafted through the game (real owner). The action menu would open (same rule as Shift+RMB). The real Shift+RMB and window are in the manual suite.')

T('SM-002', 'modify name / desc / crafted-by', [
    give('AxeStone', crafter='me'),
    H('edit', True, 'name', prefab='AxeStone', op='name', text='Test Club'),
    H('edit', True, 'desc', prefab='AxeStone', op='desc', text='Test description'),
    H('edit', True, 'crafted-by', prefab='AxeStone', op='craftedBy', text='crafted in test'),
    H('name', label='shows the new name', contains='Test Club', prefab='AxeStone'),
    H('desc', label='shows the new description', contains='Test description', prefab='AxeStone'),
    H('craftedBy', label='shows the new crafted-by', contains='crafted in test', prefab='AxeStone'),
], sheet='SM-002, SM-003, SM-004')

T('SM-005', 'upgrade keeps the changes', [
    U('craftRenamed', text='Test Club'),
    H('upgrade', True, 'upgrade at the workbench (game crafting code)', stop=True, prefab='AxeStone'),
    H('quality', 2, 'now quality 2', prefab='AxeStone'),
    H('name', label='name survived the upgrade', contains='Test Club', prefab='AxeStone'),
], sheet='SM-005')

T('SM-006', 'item stand shows the new name', [
    U('craftRenamed', text='Test Club'),
    U('itemOnStand', label='put the axe on the stand'),
    H('standHover', label='stand hover shows the new name', contains='Test Club', target='stand'),
    shot('stand'),
], sheet='SM-006')

T('SM-007', 'split, rename half, drag back', [
    C({'06 Stacks/SeparateStacks': 'true', '06 Stacks/SeparateStacksHardLock': 'false'}),
    give('Mushroom', 10),
    H('split', True, prefab='Mushroom', amount=5),
    H('edit', True, 'rename one half', prefab='Mushroom', index=1, op='name', text='Test Shroom'),
    H('stackCount', 2, 'two stacks', prefab='Mushroom'),
    H('dragMerge', True, 'drag the plain stack onto the renamed one merges (hard lock off)', prefab='Mushroom', **{'from': 0, 'to': 1}),
    H('stackSizes', '10', 'one stack of 10', prefab='Mushroom'),
    H('name', label='the merged stack kept the custom name', contains='Test Shroom', prefab='Mushroom'),
], sheet='SM-007')

T('SM-009', 'VIP can edit anyone\'s item', [
    C(VIP),
    give('AxeStone', crafter='Alice'),
    play('Nacho-VIP'),
    H('elevated', True, 'Nacho-VIP is elevated'),
    H('edit', True, 'VIP renames Alice\'s axe', prefab='AxeStone', op='name', text='VIP Club'),
    H('name', contains='VIP Club', prefab='AxeStone'),
    play('Bob'),
    H('elevated', False, 'Bob is not'),
    H('can', False, 'Bob cannot rename Alice\'s axe', prefab='AxeStone', op='name'),
    play('me'),
], sheet='SM-009, TC-217')

T('SM-010', 'craft paper', [
    give('Wood', 1),
    give('Coal', 1),
    {'do': 'craft', 'prefab': 'Drakes_PieceOfPaper', 'free': False},
    check('hasItem', 'crafted one Piece of Paper', prefab='Drakes_PieceOfPaper', count=1),
    check('noItem', 'used the wood', prefab='Wood'),
    check('noItem', 'used the coal', prefab='Coal'),
    H('paperRecipe', label='recipe enabled', contains='enabled=True'),
    H('paperRecipe', label='default cost', contains='cost=Wood:1,Coal:1'),
    C({'10 Paper/PaperEnabled': 'false'}),
    wait(0.3),
    log('PaperEnabled=false: the recipe state is recorded below (the recipe is applied when items register; a live toggle may need a world reload).'),
    H('paperRecipe'),
], sheet='SM-010', desc='Actually crafting it at the inventory is in the manual suite.')

T('SM-011', 'blank paper becomes a written page', [
    give('Drakes_PieceOfPaper', 2),
    H('can', True, 'blank paper always allows a name', prefab='Drakes_PieceOfPaper', op='name'),
    H('edit', True, 'write a name', stop=True, prefab='Drakes_PieceOfPaper', op='name', text='Test Paper'),
    H('isWritten', True, 'a written page appeared', prefab='Drakes_Paper_Written'),
    H('name', contains='Test Paper', prefab='Drakes_Paper_Written'),
    H('stackSizes', '1', 'one blank sheet used', prefab='Drakes_PieceOfPaper'),
    H('owner', label='the writer owns the page', notContains='Alice', prefab='Drakes_Paper_Written'),
], sheet='SM-011')

PIN_FLAT = C({'10 Paper/BlankPaperPlaceOrientation': 'Horizontal Only'})

def written(text='Test Paper', desc=None):
    return [U('writePage', text=text, **({'desc': desc} if desc else {}))]

T('SM-012', 'pin a page, copy it, take it', [
    PIN_FLAT,
    *written('Test Paper', 'Hello from the wall'),
    U('pinPage', label='pin the page (place-mode code)'),
    H('pageName', label='pinned page keeps its name', contains='Test Paper'),
    H('pageDesc', label='and its text', contains='Hello from the wall'),
    check('noItem', 'the page left the inventory', prefab='Drakes_Paper_Written'),
    shot('pinned'),
    give('Drakes_PieceOfPaper', 1),
    H('pageCopy', True, 'copy it from the wall (uses one blank)', copies=1),
    H('name', label='the copy has the same name', contains='Test Paper', prefab='Drakes_Paper_Written'),
    H('isCopy', label='copy mark on a separate written copy (informational: only printed stacks are stamped)', prefab='Drakes_Paper_Written'),
    H('pageTake', True, 'hold E takes it back', hold=True),
    H('stackCount', 2, 'copy + original in the inventory', prefab='Drakes_Paper_Written'),
], sheet='SM-012, SM-013, SM-014')

T('SM-015', 'paper names follow the language file', [
    give('Drakes_PieceOfPaper', 1),
    H('name', 'Piece of Paper', 'blank paper name (English)', prefab='Drakes_PieceOfPaper'),
    C({'10 Paper/PaperName': 'Scrap'}),
    wait(0.2),
    H('name', 'Scrap', 'PaperName config renames blank paper live', prefab='Drakes_PieceOfPaper'),
], sheet='SM-015 (partly)', desc='Switching the game language is in the manual suite.')

# ---------------------------------------------------------------- CORE

T('TC-101', 'unlock cost then edit everything', [
    C({'07 UnlockCost/UnlockCostEnabled': 'true', '07 UnlockCost/UnlockCost': 'Coins:5'}),
    give('AxeStone', crafter='me'),
    give('Coins', 10),
    H('showsUnlock', True, 'Unlock button offered', prefab='AxeStone'),
    H('denial', 'UnlockCostRequired', 'locked until paid', prefab='AxeStone', op='name'),
    H('unlock', True, 'pay the unlock', stop=True, prefab='AxeStone'),
    H('stackSizes', '5', '5 coins spent', prefab='Coins'),
    H('isUnlocked', True, prefab='AxeStone'),
    U('editAll', name='Test Name', desc='Test Desc', craftedBy='Crafted in Test'),
    U('checkAll', name='Test Name', desc='Test Desc', craftedBy='Crafted in Test'),
    H('unlock', False, 'cannot pay twice', prefab='AxeStone'),
    {'do': 'clearInventory'},
    give('AxeFlint', crafter='me'),
    give('Coins', 3),
    H('showsUnlock', True, 'Unlock offered for the new axe', prefab='AxeFlint'),
    H('unlock', False, 'not enough coins', prefab='AxeFlint'),
    check('message', 'the player was told why', contains='unlock requirements'),
], sheet='TC-101')

T('TC-102', 'upgrade keeps name, desc and crafted-by', [
    give('AxeStone', crafter='me'),
    U('editAll', name='Test Name', desc='Test Desc', craftedBy='Crafted in Test'),
    H('upgrade', True, stop=True, prefab='AxeStone'),
    U('checkAll', name='Test Name', desc='Test Desc', craftedBy='Crafted in Test'),
], sheet='TC-102')

T('TC-103', 'stand shows the name, taking it back keeps it', [
    U('craftRenamed', text='Test Name', desc='Test Desc'),
    U('itemOnStand'),
    H('standHover', contains='Test Name', target='stand'),
    shot('on-stand'),
    H('standTake', True, 'take it back', stop=True, target='stand'),
    H('name', label='name back in the inventory', contains='Test Name', prefab='AxeStone'),
    H('desc', label='description too', contains='Test Desc', prefab='AxeStone'),
], sheet='TC-103')

T('TC-104', 'reset fields and reset all', [
    give('AxeStone', crafter='me'),
    U('editAll', name='Old Name', desc='Old Desc', craftedBy='Old Maker'),
    H('reset', True, 'reset name', prefab='AxeStone', op='name'),
    H('name', label='default name back', notContains='Old Name', prefab='AxeStone'),
    H('reset', True, 'reset description', prefab='AxeStone', op='desc'),
    H('desc', notContains='Old Desc', prefab='AxeStone'),
    H('reset', True, 'reset crafted-by', prefab='AxeStone', op='craftedBy'),
    H('craftedBy', label='original crafter shown again', notContains='Old Maker', prefab='AxeStone'),
    U('editAll', name='111', desc='222', craftedBy='333'),
    H('reset', True, 'Reset All', prefab='AxeStone', op='all'),
    H('name', notContains='111', prefab='AxeStone'),
    H('desc', notContains='222', prefab='AxeStone'),
    H('craftedBy', notContains='333', prefab='AxeStone'),
    H('reset', False, 'nothing left to reset', prefab='AxeStone', op='all'),
], sheet='TC-104')

T('TC-105', 'paper basics: write, pin, style, take', [
    PIN_FLAT,
    give('Drakes_PieceOfPaper', 1),
    H('edit', True, stop=True, prefab='Drakes_PieceOfPaper', op='name', text='Test Paper'),
    H('edit', True, prefab='Drakes_Paper_Written', op='desc', text='Line 1\nLine 2\nLine 3\nLine 4'),
    H('edit', True, 'crafted-by on a written page', prefab='Drakes_Paper_Written', op='craftedBy', text='Test Name'),
    U('pinPage'),
    H('pageMayEdit', True, 'owner may open the wall editor'),
    H('pageStyle', label='landscape + font 7', contains='landscape=True', font=7, landscape=True),
    wait(0.5),
    shot('landscape-font7'),
    H('pageTake', True, 'hold E takes it', hold=True),
    H('name', contains='Test Paper', prefab='Drakes_Paper_Written'),
    H('desc', contains='Line 4', prefab='Drakes_Paper_Written'),
], sheet='TC-105', desc='Screenshot shows the landscape text; whether only LINE 1/LINE 2 fit is a visual check (manual suite).')

T('TC-106', 'copies, printed stack, recycle', [
    *written('Copy Me', 'Copy body'),
    give('Drakes_PieceOfPaper', 5),
    H('copy', True, '5 separate copies', stop=True, prefab='Drakes_Paper_Written', match='Copy Me', copies=5),
    H('stackCount', 6, 'original + 5 copies, unstackable', prefab='Drakes_Paper_Written'),
    give('Drakes_PieceOfPaper', 5),
    H('copy', True, 'printed (immutable) stack of 5', stop=True, prefab='Drakes_Paper_Written', index=0, copies=5, immutable=True),
    H('stackSizes', '5', 'one stack of 5 prints', prefab='Drakes_Paper_Print'),
    H('canRecycle', True, 'a written page can be recycled', prefab='Drakes_Paper_Written', index=0),
    H('recycle', True, 'recycle it', prefab='Drakes_Paper_Written', index=0),
    H('stackCount', 5, 'one written page fewer', prefab='Drakes_Paper_Written'),
    check('hasItem', 'got a blank back', prefab='Drakes_PieceOfPaper'),
    H('canRecycle', False, 'prints cannot be recycled (default)', prefab='Drakes_Paper_Print'),
    C({'10 Paper/PaperRecycleImmutableEnabled': 'true'}),
    H('canRecycle', True, 'unless PaperRecycleImmutableEnabled', prefab='Drakes_Paper_Print'),
    C({'10 Paper/PaperRecycleEnabled': 'false'}),
    H('canRecycle', False, 'PaperRecycleEnabled off blocks all recycling', prefab='Drakes_Paper_Written', index=0),
], sheet='TC-106, ISSUE-017')

T('TC-106b', 'pinning one print keeps the stack count', [
    PIN_FLAT,
    *written('Print Me'),
    give('Drakes_PieceOfPaper', 5),
    H('copy', True, stop=True, prefab='Drakes_Paper_Written', copies=5, immutable=True),
    U('pinPage', prefab='Drakes_Paper_Print', label='pin one print'),
    H('stackSizes', '4', '4 prints left in one stack', prefab='Drakes_Paper_Print'),
    H('pageTake', True, hold=True),
    H('stackSizes', '5', 'taking it back stacks again', prefab='Drakes_Paper_Print'),
], sheet='TC-106 step 8, ISSUE-015, ISSUE-016')

T('TC-107', 'shared page: others may rewrite, not re-share', [
    PIN_FLAT,
    *written('Shared Page'),
    U('pinPage'),
    H('pageShare', True, 'owner turns Shared on', on=True),
    H('pageIsShared', True),
    play('Bob'),
    H('pageEdit', True, 'Bob may rename a Shared page', op='name', text='Bob was here'),
    H('pageName', contains='Bob was here'),
    H('pageShare', False, 'Bob cannot turn Shared off', on=False),
    H('pageIsShared', True, 'still shared'),
    play('me'),
], sheet='TC-107, TC-504 (simulated)')

T('TC-107b', 'private page: others cannot rewrite', [
    PIN_FLAT,
    *written('Private Page'),
    U('pinPage'),
    play('Bob'),
    H('pageEdit', False, 'Bob cannot rename a private page', op='name', text='Bob was here'),
    H('pageName', label='unchanged', contains='Private Page'),
    H('pageShare', False, 'Bob cannot share it', on=True),
    play('me'),
], sheet='TC-107, TC-503 (simulated)')

T('TC-108', 'shared item: another player renames it', [
    give('AxeStone', crafter='me'),
    H('share', True, 'owner turns Shared on', prefab='AxeStone', on=True),
    U('itemOnStand'),
    play('Bob'),
    H('standTake', True, 'Bob takes it off the stand', stop=True, target='stand'),
    H('isShared', True, 'still Shared after the stand', prefab='AxeStone'),
    H('edit', True, 'Bob renames the Shared axe', prefab='AxeStone', op='name', text='Shared Axe'),
    H('can', False, 'but not the crafted-by line', prefab='AxeStone', op='craftedBy'),
    H('share', False, 'and cannot turn Shared off', prefab='AxeStone', on=False),
    H('owner', label='owner unchanged', notContains='Bob', prefab='AxeStone'),
    play('me'),
    H('share', True, 'owner turns Shared off', prefab='AxeStone', on=False),
    play('Bob'),
    H('can', False, 'now Bob cannot rename it', prefab='AxeStone', op='name'),
    play('me'),
], sheet='TC-108, TC-504 (simulated)')

T('TC-109', 'upgrading an unowned item claims it', [
    give('AxeStone'),
    H('owner', '', 'unowned to start', prefab='AxeStone'),
    H('upgrade', True, stop=True, prefab='AxeStone'),
    H('owner', label='upgrader now owns it (ISSUE-004, by design)', notContains='Alice', prefab='AxeStone'),
    H('edit', True, prefab='AxeStone', op='name', text='My Stone Axe'),
    H('name', contains='My Stone Axe', prefab='AxeStone'),
    {'do': 'clearInventory'},
    give('AxeStone', crafter='Alice'),
    H('upgrade', True, 'upgrading Alice\'s axe', prefab='AxeStone'),
    H('owner', label='observed owner after upgrading someone else\'s axe (informational)', prefab='AxeStone'),
    H('can', label='observed: may rename it after upgrading (informational)', prefab='AxeStone', op='name'),
], sheet='TC-109, ISSUE-004')

T('TC-110', 'locked ward: stand name shown, public page takeable', [
    PIN_FLAT,
    play('Alice'),
    U('craftRenamed', by='Alice', text='Alice Axe'),
    {'do': 'spawn', 'prefab': 'itemstandh', 'dz': 2.5, 'dx': -1.5, 'owner': 'Alice', 'as': 'stand'},
    H('standPut', True, stop=True, prefab='AxeStone', target='stand'),
    *written('Public Page'),
    U('pinPage'),
    H('pageShare', True, 'Alice shares the page', on=True),
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -3, 'owner': 'Alice', 'enabled': True, 'radius': 15, 'as': 'ward'},
    H('pageTakePublic', True, 'Alice makes it public (anyone may take it)', on=True),
    wait(0.5),
    H('pageIsTakePublic', True, 'the page is now public'),
    play('Bob'),
    H('pageHover', label="Bob's hover on the public page (informational)"),
    check('wardAccess', 'Bob has no ward access', expect=False),
    H('standHover', label='Bob still sees the axe name on the stand', contains='Alice Axe', target='stand'),
    shot('bob-sees-stand'),
    H('pageTake', True, 'Bob may take the public page', hold=True),
    H('edit', True, 'Bob renames the shared page', prefab='Drakes_Paper_Written', op='name', text='My Paper Now'),
    H('name', contains='My Paper Now', prefab='Drakes_Paper_Written'),
    C({'05 General/ShowItemStandItemNameWhenNoAccess': 'false'}),
    H('standHover', label='with ShowItemStandItemNameWhenNoAccess off the name is hidden', notContains='Alice Axe', target='stand'),
    play('me'),
], sheet='TC-110, TC-216')

# ---------------------------------------------------------------- CONFIG

T('TC-201', 'excluded names / categories / allowlist', [
    *[give(p, crafter='me') for p in ['AtgeirBronze', 'Club', 'MaceIron', 'MaceNeedle', 'Torch']],
    H('denial', 'ExcludedByName', 'Bronze atgeir excluded by name', prefab='AtgeirBronze', op='name'),
    H('denial', 'ExcludedByCategory', 'Club excluded by category', prefab='Club', op='name'),
    H('denial', 'ExcludedByCategory', 'Iron mace excluded by category', prefab='MaceIron', op='name'),
    H('denial', 'ExcludedByCategory', 'Porcupine excluded by category', prefab='MaceNeedle', op='name'),
    H('can', True, 'Torch is on the allowlist', prefab='Torch', op='name'),
    H('blockedMessage', label='denial text names the reason', contains='Excluded by name', prefab='AtgeirBronze'),
    H('blockedMessage', contains='Excluded by category', prefab='Club'),
], sheet='TC-201')

T('TC-202', 'LockToOwner on, NameClaimsOwner on', [
    C({'05 General/LockToOwner': 'true', '05 General/NameClaimsOwner': 'true'}),
    give('AxeStone', crafter='Alice'),
    H('denial', 'NotOwner', 'cannot edit Alice\'s axe', prefab='AxeStone', op='name'),
    give('Wood', 5),
    H('edit', True, 'rename unowned wood', prefab='Wood', op='name', text='My Wood'),
    H('owner', label='renaming claimed it', notContains='Alice', contains='', prefab='Wood'),
    play('Bob'),
    H('can', False, 'Bob cannot edit the claimed wood', prefab='Wood', op='name'),
    play('me'),
], sheet='TC-202')

T('TC-203', 'LockToOwner off, NameClaimsOwner on', [
    C({'05 General/LockToOwner': 'false', '05 General/NameClaimsOwner': 'true'}),
    give('AxeStone', crafter='Alice'),
    H('can', True, 'may edit Alice\'s axe', prefab='AxeStone', op='name'),
    give('Wood', 5),
    H('edit', True, prefab='Wood', op='name', text='My Wood'),
    play('Bob'),
    H('can', True, 'Bob may edit it too (no owner lock)', prefab='Wood', op='name'),
    play('me'),
], sheet='TC-203')

T('TC-204', 'LockToOwner on, NameClaimsOwner off', [
    C({'05 General/LockToOwner': 'true', '05 General/NameClaimsOwner': 'false'}),
    give('Wood', 5),
    H('edit', True, prefab='Wood', op='name', text='Nobody Wood'),
    H('owner', '', 'renaming did not claim it', prefab='Wood'),
    play('Bob'),
    H('can', True, 'still unowned, so Bob may edit it', prefab='Wood', op='name'),
    play('me'),
], sheet='TC-204')

T('TC-205', 'AllowRenameUnownedItems off', [
    give('Wood', 5),
    H('edit', True, 'rename wood while allowed (claims it)', prefab='Wood', op='name', text='Claimed Wood'),
    C({'05 General/AllowRenameUnownedItems': 'false'}),
    give('Stone', 5),
    H('denial', 'UncraftedResourceBlocked', 'unowned stone blocked', prefab='Stone', op='name'),
    H('can', True, 'already-claimed wood can still be changed', prefab='Wood', op='name'),
    give('AxeStone', crafter='me'),
    H('can', True, 'crafted items unaffected', prefab='AxeStone', op='name'),
    C({'03 Exclusions/RenameAllowlist': 'Torch,Stone'}),
    H('can', True, 'allowlist bypasses the unowned rule', prefab='Stone', op='name'),
], sheet='TC-205, ISSUE-009')

for tid, key, op, reason, others in [
        ('TC-206', 'RenameEnabled', 'name', 'GlobalRenameDisabled', ['desc', 'craftedBy']),
        ('TC-207', 'RewriteDescriptionsEnabled', 'desc', 'GlobalDescDisabled', ['name', 'craftedBy']),
        ('TC-208', 'CraftedByLabelEnabled', 'craftedBy', 'GlobalCraftedByDisabled', ['name', 'desc'])]:
    T(tid, f'{key} off', [
        C({f'02 Features/{key}': 'false'}),
        give('AxeStone', crafter='me'),
        H('denial', reason, f'{op} blocked', prefab='AxeStone', op=op),
        *[H('can', True, f'{o} still allowed', prefab='AxeStone', op=o) for o in others],
        C(VIP),
        play('Nacho-VIP'),
        H('can', True, 'VIP override still may', prefab='AxeStone', op=op),
        play('me'),
    ], sheet=tid)

T('TC-208b', 'Reset All clears a VIP crafted-by even when crafted-by is off', [
    C(VIP),
    play('Bob'),
    give('AxeStone', crafter='me'),
    play('Nacho-VIP'),
    H('edit', True, 'VIP sets a crafted-by label', prefab='AxeStone', op='craftedBy', text='VIP Label'),
    play('Bob'),
    C({'02 Features/CraftedByLabelEnabled': 'false'}),
    H('reset', True, 'owner uses Reset All', prefab='AxeStone', op='all'),
    H('craftedBy', notContains='VIP Label', prefab='AxeStone'),
    play('me'),
], sheet='ISSUE-011')

T('TC-209', 'durability labels', [
    C({'09 Modifiers/DurabilityModifierEnabled': 'true'}),
    give('AxeStone', crafter='me'),
    give('Wood', 5),
    H('durability', True, prefab='AxeStone', fraction=1.0),
    H('name', label='full = Pristine', contains='Pristine', prefab='AxeStone'),
    H('durability', True, prefab='AxeStone', fraction=0.75),
    H('name', label='<=0.8 = Tarnished', contains='Tarnished', prefab='AxeStone'),
    H('durability', True, prefab='AxeStone', fraction=0.55),
    H('name', label='<=0.6 = Worn', contains='Worn', prefab='AxeStone'),
    H('durability', True, prefab='AxeStone', fraction=0.3),
    H('name', label='<=0.4 = Rusty', contains='Rusty', prefab='AxeStone'),
    H('durability', True, prefab='AxeStone', fraction=0.0),
    H('name', label='0 = Broken', contains='Broken', prefab='AxeStone'),
    H('name', label='no label on resources (ISSUE-008)', notContains='Pristine', prefab='Wood'),
    C({'09 Modifiers/DurabilityModifierEnabled': 'false'}),
    H('name', label='off = no label', notContains='Broken', prefab='AxeStone'),
], sheet='TC-209, ISSUE-008')

def stacks_setup(sep, hard):
    return [
        C({'06 Stacks/SeparateStacks': sep, '06 Stacks/SeparateStacksHardLock': hard}),
        give('Mushroom', 50),
        H('split', True, stop=True, prefab='Mushroom', amount=25),
        H('stackCount', 2, 'split into two stacks', stop=True, prefab='Mushroom'),
        H('edit', True, prefab='Mushroom', index=0, op='name', text='Test Shroom'),
        H('edit', True, prefab='Mushroom', index=0, op='desc', text='Shroom desc'),
        H('edit', True, prefab='Mushroom', index=1, op='name', text='Test Mush'),
    ]

BLOCKS['splitStacks'] = {
    'description': 'SeparateStacks / HardLock as given, 50 mushrooms split 25/25, both halves renamed differently.',
    'params': ['sep', 'hard'],
    'actions': stacks_setup(P('sep'), P('hard'))}

T('TC-210', 'SeparateStacks on, HardLock on', [
    U('splitStacks', sep='true', hard='true'),
    H('dropAndPickup', 2, 'dropped and picked up: still 2 stacks', prefab='Mushroom'),
    H('dragMerge', False, 'drag does not merge', prefab='Mushroom'),
    H('stackCount', 2, prefab='Mushroom'),
], sheet='TC-210')

T('TC-211', 'SeparateStacks on, HardLock off', [
    U('splitStacks', sep='true', hard='false'),
    H('dropAndPickup', 2, 'pickup keeps them apart', prefab='Mushroom'),
    H('dragMerge', True, 'drag merges', prefab='Mushroom'),
    H('stackCount', 1, prefab='Mushroom'),
], sheet='TC-211')

T('TC-212', 'SeparateStacks off', [
    U('splitStacks', sep='false', hard='false'),
    H('dropAndPickup', 1, 'pickup merges them', prefab='Mushroom'),
], sheet='TC-212')

T('TC-213', 'ExcludeStacks', [
    C({'06 Stacks/ExcludeStacks': 'true'}),
    give('Mushroom', 10),
    give('AxeStone', crafter='me'),
    give('Wood', 10),
    H('denial', 'ExcludedStacks', 'stackable mushrooms blocked', prefab='Mushroom', op='name'),
    H('can', True, 'the axe (stack 1) is fine', prefab='AxeStone', op='name'),
    C({'03 Exclusions/RenameAllowlist': 'Torch,Wood'}),
    H('can', True, 'allowlisted wood bypasses ExcludeStacks', prefab='Wood', op='name'),
], sheet='TC-213')

T('TC-214', 'menu keys config', [
    {'do': 'invoke', 'method': 'DrakeRenameit.RenameitConfig.MenuOpenModifier'},
    check('lastResult', 'server default is Shift', expect='Shift'),
    C({'11 UI-NotSynced/MenuOpenModifier': 'Ctrl'}),
    {'do': 'invoke', 'method': 'DrakeRenameit.RenameitConfig.MenuOpenModifier'},
    check('lastResult', 'a local key overrides it', expect='Ctrl'),
    C({'11 UI-NotSynced/MenuOpenModifier': '', '05 General/ServerDefaultMenuOpenModifier': 'None'}),
    {'do': 'invoke', 'method': 'DrakeRenameit.RenameitConfig.MenuOpenModifier'},
    check('lastResult', 'empty local key follows the server default', expect='None'),
], sheet='TC-214 (partly)', desc='Pressing the keys for real is in the manual suite.')

T('TC-215', 'denial UI and reasons', [
    give('AxeStone', crafter='Alice'),
    H('blockedMessage', label='ShowReason on: specific reason', contains='own this item', prefab='AxeStone'),
    H('edit', False, 'editing is refused', prefab='AxeStone', op='name', text='x'),
    check('message', 'an on-screen message told the player', contains='own'),
    C({'05 General/ShowReason': 'false'}),
    H('blockedMessage', label='ShowReason off: generic text', notContains='own this item', prefab='AxeStone'),
    C({'05 General/ShowDenialUi': 'false'}),
    H('blockedMessage', '', 'ShowDenialUi off: no denial text', prefab='AxeStone'),
], sheet='TC-215')

T('TC-216', 'stand name hidden setting', [
    play('Alice'),
    U('craftRenamed', by='Alice', text='Hidden Axe'),
    {'do': 'spawn', 'prefab': 'itemstandh', 'dz': 2.5, 'owner': 'Alice', 'as': 'stand'},
    H('standPut', True, stop=True, prefab='AxeStone', target='stand'),
    {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -3, 'owner': 'Alice', 'enabled': True, 'radius': 15},
    wait(0.5),
    play('Bob'),
    H('standHover', label='on: name shown to someone without access', contains='Hidden Axe', target='stand'),
    C({'05 General/ShowItemStandItemNameWhenNoAccess': 'false'}),
    H('standHover', label='off: name hidden', notContains='Hidden Axe', target='stand'),
    play('me'),
], sheet='TC-216 (assumed: ShowItemStandItemNameWhenNoAccess)')

T('TC-217', 'VIP and admin override settings', [
    give('AxeStone', crafter='Alice'),
    H('elevated', False, 'AllowAdminOverride off: nobody elevated'),
    C({'01 Admin/AllowAdminOverride': 'true', '01 Admin/VipOnlyOverride': 'false'}),
    H('elevated', label='observed: local host counts as admin (informational)'),
    C({'01 Admin/VipOnlyOverride': 'true'}),
    H('elevated', False, 'VipOnlyOverride: admin alone is not enough'),
    C({'01 Admin/VipList': 'Bob;Nacho-VIP'}),
    play('Bob'),
    H('elevated', True, 'Bob on the VIP list (semicolon separated)'),
    H('can', True, 'VIP may edit Alice\'s axe', prefab='AxeStone', op='name'),
    C({'01 Admin/AllowAdminOverride': 'false'}),
    H('elevated', False, 'AllowAdminOverride off turns VIPs off too'),
    play('me'),
], sheet='TC-217')

T('TC-218', 'WrittenPaperIgnoresRestrictions', [
    play('Alice'),
    *written('Alice Notes'),
    H('share', True, 'Alice shares the page', prefab='Drakes_Paper_Written', on=True),
    play('Bob'),
    C({'02 Features/RenameEnabled': 'false'}),
    H('can', False, 'rename off: Bob cannot rename the shared page', prefab='Drakes_Paper_Written', op='name'),
    C({'10 Paper/WrittenPaperIgnoresRestrictions': 'true'}),
    H('can', True, 'with the bypass he can', prefab='Drakes_Paper_Written', op='name'),
    play('Alice'),
    C({'10 Paper/WrittenPaperIgnoresRestrictions': 'false'}),
    H('can', True, 'the owner always may', prefab='Drakes_Paper_Written', op='name'),
    play('me'),
], sheet='TC-218')

# ---------------------------------------------------------------- EDGE

T('TC-302', 'rich text in names', [
    give('AxeStone', crafter='me'),
    H('edit', True, prefab='AxeStone', op='name', text='<color=red>Red</color> Axe'),
    H('name', label='colour tag kept', contains='<color=red>Red</color>', prefab='AxeStone'),
    H('edit', True, prefab='AxeStone', op='name', text='<color=#00ff00>Unclosed'),
    H('name', label='unclosed tag gets closed for display', contains='</color>', prefab='AxeStone'),
    H('edit', True, prefab='AxeStone', op='desc', text='<b>Bold</b> and <i>italic</i>'),
    H('desc', contains='<b>Bold</b>', prefab='AxeStone'),
], sheet='TC-302, ISSUE-002')

def fill_inventory():
    return [give('Stone', 1600)]

T('TC-304', 'take a page with a full inventory', [
    PIN_FLAT,
    *written('Full Inv Page'),
    U('pinPage'),
    *fill_inventory(),
    H('pageTake', False, 'cannot take it with no room', hold=True),
    H('pageName', label='page still on the wall', contains='Full Inv Page'),
    {'do': 'clearInventory'},
    H('pageTake', True, 'takes it once there is room', hold=True),
], sheet='TC-304')

T('TC-305', 'copy more pages than you can hold', [
    *written('Too Many'),
    give('Drakes_PieceOfPaper', 50),
    give('Stone', 1400),
    H('copy', False, 'refused when there is no room for the copies', prefab='Drakes_Paper_Written', copies=10),
    H('stackCount', 1, 'no partial copies left behind', prefab='Drakes_Paper_Written'),
    H('stackSizes', '50', 'blanks refunded / not used', prefab='Drakes_PieceOfPaper'),
], sheet='TC-305')

# ---------------------------------------------------------------- extra coverage (not in the sheet)

T('X-01', 'paper config: stack sizes, item type, hammer pieces', [
    H('maxStack', 50, 'BlankPaperStackSize 50', prefab='Drakes_PieceOfPaper'),
    H('maxStack', 1, 'Written Page always 1', prefab='Drakes_Paper_Written'),
    H('maxStack', 50, 'PrintedPaperStackSize 50', prefab='Drakes_Paper_Print'),
    H('itemType', 'Material', 'PaperItemType Material', prefab='Drakes_PieceOfPaper'),
    H('pieceEnabled', True, 'wall sheet placeable', prefab='Drakes_PaperBlank_Upright'),
    H('pieceEnabled', True, 'flat sheet placeable', prefab='Drakes_PaperBlank_Laying'),
    H('pieceEnabled', True, 'paper stack placeable', prefab='Drakes_PaperStack'),
    C({'10 Paper/VerticalPaperPlaceable': 'false', '10 Paper/StackPaperPlaceable': 'false'}),
    H('pieceEnabled', False, 'VerticalPaperPlaceable off hides the wall sheet', prefab='Drakes_PaperBlank_Upright'),
    H('pieceEnabled', True, 'flat sheet unaffected', prefab='Drakes_PaperBlank_Laying'),
    H('pieceEnabled', False, 'StackPaperPlaceable off hides the stack', prefab='Drakes_PaperStack'),
], sheet='CONFIGS 10 Paper rows 7-9, 23-25')

T('X-02', 'crafted-by labels', [
    give('AxeStone', crafter='me'),
    H('edit', True, 'pick the Belongs To label', prefab='AxeStone', op='craftedBy', text='Bjorn', lineLabel='Belongs To'),
    H('craftedBy', contains='Belongs To', prefab='AxeStone'),
    H('edit', False, 'a label not in AllowedLabels is refused', prefab='AxeStone', op='craftedBy', text='Bjorn', lineLabel='Stolen From'),
    C({'04 CraftedBy/LabelCustomizable': 'false'}),
    H('edit', True, 'label picker locked for normal players', prefab='AxeStone', op='craftedBy', text='Bjorn2', lineLabel='Return to'),
    H('craftedBy', label='label unchanged when not customizable', notContains='Return to', prefab='AxeStone'),
], sheet='CONFIGS 04 CraftedBy')

T('X-03', 'shared items need PublicRewriteEnabled', [
    give('AxeStone', crafter='me'),
    H('share', True, prefab='AxeStone', on=True),
    C({'02 Features/PublicRewriteEnabled': 'false'}),
    play('Bob'),
    H('can', False, 'PublicRewriteEnabled off: Shared does nothing', prefab='AxeStone', op='name'),
    play('me'),
], sheet='CONFIGS 02 Features row 4')

T('X-04', 'unlock cost with other prices and nocost', [
    C({'07 UnlockCost/UnlockCostEnabled': 'true', '07 UnlockCost/UnlockCost': 'Coal:2;Wood:3'}),
    give('AxeStone', crafter='me'),
    give('Coal', 2),
    give('Wood', 3),
    H('unlock', True, 'pays a two-item cost (semicolon list)', prefab='AxeStone'),
    check('noItem', 'coal spent', prefab='Coal'),
    check('noItem', 'wood spent', prefab='Wood'),
    give('Drakes_PieceOfPaper', 1),
    H('denial', 'None', 'paper is never unlock-gated', prefab='Drakes_PieceOfPaper', op='name'),
    C({'07 UnlockCost/UnlockCost': 'NotAnItem:5'}),
    give('AxeFlint', crafter='me'),
    H('showsUnlock', False, 'an invalid cost is ignored (no gate)', prefab='AxeFlint'),
], sheet='CONFIGS 07 UnlockCost')

T('X-05', 'renamed item survives drop and pickup', [
    U('craftRenamed', text='Dropped Axe', desc='Still here'),
    H('dropAndPickup', 1, prefab='AxeStone'),
    H('name', contains='Dropped Axe', prefab='AxeStone'),
    H('desc', contains='Still here', prefab='AxeStone'),
], sheet='SM-008 (partly: persistence through the world, not a relaunch)')

T('SM-008', 'changes survive logging out and back in', [
    PIN_FLAT,
    U('craftRenamed', text='Saved Axe', desc='Saved desc'),
    H('edit', True, prefab='AxeStone', op='craftedBy', text='Saved maker'),
    {'do': 'craft', 'prefab': 'AxeFlint'},
    H('edit', True, prefab='AxeFlint', op='name', text='Stand Axe'),
    U('itemOnStand', prefab='AxeFlint'),
    *written('Saved Page', 'Saved text'),
    U('pinPage', dz=1.2),
    {'do': 'relog', 'timeout': 240},
    U('checkAll', name='Saved Axe', desc='Saved desc', craftedBy='Saved maker'),
    check('hook', 'still has an owner', name='renameit.owner', args={'prefab': 'AxeStone'}),
    H('standHover', label='the stand still shows the renamed axe', contains='Stand Axe', target='stand'),
    H('pageName', label='the pinned page kept its name', contains='Saved Page'),
    H('pageDesc', label='and its text', contains='Saved text'),
    shot('after-relog'),
    H('pageTake', True, 'take the page back (keeps the world clean)', hold=True),
], sheet='SM-008', timeout=300,
   desc='Logs out to the main menu and loads the same world and character back in (the relog step; no game restart), like a player checking their changes were saved.')

# ---------------------------------------------------------------- real UI and world (formerly manual)
# Keys go through the same calls the game and mods read (Input.GetKey/GetKeyDown, ZInput, Libs' key check);
# the inventory right-click runs the grid's own right-click handler, the same code a mouse click runs.

def open_window(prefab='AxeStone', mods='Shift'):
    return [{'do': 'itemClick', 'prefab': prefab, 'mods': mods}, wait(0.4)]

CLOSE = [H('closeWindows'), {'do': 'closeUi'}]

T('SM-001-UI', 'Shift + right-click opens the Workshop window', [
    give('AxeStone', crafter='me'),
    {'do': 'craft', 'prefab': 'AxeFlint', 'by': 'Alice'},
    *open_window(),
    H('windowOpen', True, 'the window opened for your own axe', stop=True),
    check('uiVisible', 'its Save button is showing', name='Save'),
    shot('workshop-open'),
    *CLOSE,
    *open_window('AxeFlint'),
    H('windowOpen', False, 'the RenameIt window does not open for the axe Alice made'),
    H('blockedMessage', label='the reason a player is given (a tab from another mod, e.g. ReskinIt, may open instead)', contains='own', prefab='AxeFlint'),
    *CLOSE,
], sheet='SM-001, TC-101 step 2', desc='Real Shift + right-click on the inventory slot (formerly manual M-SM-001).')

T('TC-214-UI', 'menu keys pressed for real', [
    C({'11 UI-NotSynced/MenuOpenModifier': 'Ctrl'}),
    give('AxeStone', crafter='me'),
    *open_window(mods='Shift'),
    H('windowOpen', False, 'with Ctrl configured, Shift + right-click does nothing'),
    *CLOSE,
    *open_window(mods='Ctrl'),
    H('windowOpen', True, 'Ctrl + right-click opens it'),
    *CLOSE,
    C({'11 UI-NotSynced/MenuOpenModifier': 'Ctrl+Alt'}),
    *open_window(mods='Ctrl'),
    H('windowOpen', False, 'Ctrl alone is not enough for Ctrl+Alt'),
    *CLOSE,
    *open_window(mods='Ctrl+Alt'),
    H('windowOpen', True, 'Ctrl+Alt + right-click opens it'),
    *CLOSE,
], sheet='TC-214', desc='Formerly manual M-TC-214.')

T('TC-301-UI', 'character limit in the Name field', [
    C({'08 Limits/NameCharacterLimit': '10'}),
    give('AxeStone', crafter='me'),
    *open_window(),
    H('windowOpen', True, stop=True),
    {'do': 'type', 'field': 'Name', 'text': 'ABCDEFGHIJKLMNOP'},
    wait(0.2),
    shot('typed-16-chars'),
    {'do': 'click', 'button': 'Save'},
    wait(0.3),
    H('name', label='the saved name stops at 10 characters', contains='ABCDEFGHIJ', notContains='K', prefab='AxeStone'),
    *CLOSE,
], sheet='TC-301', desc='Types 16 characters into the real Name field with a limit of 10 (formerly manual M-TC-301).')

T('TC-303-UI', 'close and Esc do not save', [
    give('AxeStone', crafter='me'),
    *open_window(),
    H('windowOpen', True, stop=True),
    {'do': 'type', 'field': 'Name', 'text': 'Not Saved X'},
    {'do': 'click', 'button': '\u2715'},
    wait(0.3),
    H('windowOpen', False, 'the close button closes it'),
    H('name', label='and nothing was saved', notContains='Not Saved', prefab='AxeStone'),
    *open_window(),
    {'do': 'type', 'field': 'Name', 'text': 'Not Saved Y'},
    {'do': 'pressKey', 'key': 'Escape'},
    wait(0.4),
    H('windowOpen', False, 'Esc closes it (known issue in 1.2.1)'),
    H('name', label='and nothing was saved', notContains='Not Saved', prefab='AxeStone'),
    {'do': 'closeUi'},
    *open_window(),
    {'do': 'type', 'field': 'Name', 'text': 'Saved For Real'},
    {'do': 'click', 'button': 'Save'},
    wait(0.3),
    H('name', label='Save does save', contains='Saved For Real', prefab='AxeStone'),
    *CLOSE,
], sheet='TC-303', desc='Formerly manual M-TC-303.')

T('SM-010-UI', 'paper is in the crafting list', [
    give('Wood', 5),
    give('Coal', 5),
    {'do': 'pressKey', 'button': 'Inventory'},
    wait(0.6),
    check('uiVisible', 'Piece of Paper is listed (no station needed)', name='Piece of Paper'),
    shot('crafting-list'),
    {'do': 'closeUi'},
], sheet='SM-010', desc='Formerly manual M-SM-010; the crafting itself is SM-010.')

T('SM-012-UI', 'pin a page on a real wall', [
    C({'10 Paper/BlankPaperPlaceOrientation': 'Vertical Only'}),
    *written('Wall Page', 'On the wall'),
    {'do': 'place', 'prefab': 'woodwall', 'dz': 3, 'as': 'wall'},
    wait(0.3),
    {'do': 'lookAt', 'target': 'wall', 'distance': 2},
    H('clearPages'),
    H('pin', True, 'pinned on the wall where the crosshair is', stop=True, prefab='Drakes_Paper_Written', wall=True),
    wait(1.0),
    H('pageName', label='the wall page keeps its name', contains='Wall Page'),
    H('pageMayEdit', True, 'Shift+E editing is allowed for the owner'),
    shot('page-on-wall'),
    H('pageTake', True, 'hold E takes it', hold=True),
    check('hasItem', 'back in the inventory', prefab='Drakes_Paper_Written'),
], sheet='SM-012..014, TC-105 6-10', desc='Formerly manual M-SM-012.')

T('SM-015-UI', 'RenameIt in Spanish', [
    {'do': 'setLanguage', 'language': 'Spanish'},
    wait(0.3),
    {'do': 'craft', 'prefab': 'AxeStone', 'by': 'Alice'},
    H('blockedMessage', label='denial messages come from the Spanish file', contains='dueño', prefab='AxeStone'),
    give('Drakes_PieceOfPaper', 1),
    H('name', label='blank paper name is translated', notContains='Piece of Paper', prefab='Drakes_PieceOfPaper'),
    H('desc', label='blank paper description is translated', notContains='blank piece of paper', prefab='Drakes_PieceOfPaper'),
    shot('spanish'),
], sheet='SM-015', desc='Switches the game to Spanish for the test (put back after). Formerly manual M-SM-015.')

T('TC-105-UI', 'landscape + font 7 shows only the first lines', [
    PIN_FLAT,
    *written('Look At Me', 'LINE 1\nLINE 2\nLINE 3\nLINE 4'),
    U('pinPage'),
    H('pageStyle', contains='landscape=True', font=7, landscape=True),
    wait(0.6),
    H('pageVisibleText', label='the first line is visible', contains='LINE 1'),
    H('pageVisibleText', label='the last line does not fit', notContains='LINE 4'),
    shot('landscape-font7'),
], sheet='TC-105 step 9', desc='Reads what the page actually shows (cut where it overflows). Formerly manual M-TC-105.')

T('X-06-UI', 'world hover text styles', [
    U('craftRenamed', text='Hover Axe', desc='Hover desc'),
    {'do': 'drop', 'prefab': 'AxeStone', 'as': 'axe', 'dz': 1.5},
    wait(1.0),
    {'do': 'lookAt', 'target': 'axe', 'distance': 1.5},
    C({'11 UI-NotSynced/HoverStyle': 'Minimal'}),
    wait(0.3),
    check('uiVisible', 'Minimal shows the custom name', name='Hover Axe'),
    check('uiVisible', 'Minimal hides the description', name='Hover desc', expect=False),
    shot('hover-minimal'),
    C({'11 UI-NotSynced/HoverStyle': 'Detailed'}),
    wait(0.3),
    check('uiVisible', 'Detailed adds the description', name='Hover desc'),
    shot('hover-detailed'),
    C({'11 UI-NotSynced/HoverStyle': 'Vanilla'}),
    wait(0.3),
    check('crosshair', 'Vanilla: the game crosshair text shows the name', contains='Hover Axe'),
], sheet='CONFIGS 11 UI (hover)', desc='Formerly manual M-HOVER.')

# ---------------------------------------------------------------- PIECE RENAME (1.4)
# Hooks take target = a spawned piece's tag. Config lives in section "12 Pieces".

PC = '12 Pieces/'

def spawn(prefab, tag, owner='me', dz=2.5, dx=0):
    s = {'do': 'spawn', 'prefab': prefab, 'dz': dz, 'owner': owner, 'as': tag}
    if dx:
        s['dx'] = dx
    return s

def ward(owner, dz=-3, radius=15):
    return {'do': 'spawn', 'prefab': 'guard_stone', 'dz': dz, 'owner': owner, 'enabled': True, 'radius': radius}

def renames(tag, text, label=None):
    # rename and check the hover title is the new name (stop: later steps need it)
    return [H('pieceRename', True, label or f'{tag} can be renamed', stop=True, target=tag, text=text),
            H('pieceHoverTitle', label=f'{tag} hover shows the new name', contains=text, target=tag)]

T('PR-01', 'rename a placed piece', [
    play('Alice'),
    spawn('piece_chest_wood', 'chest', 'Alice'),
    H('pieceCan', True, 'builder may rename', stop=True, target='chest'),
    H('pieceRename', True, target='chest', text="Alice's Loot"),
    H('pieceName', "Alice's Loot", target='chest'),
    H('pieceHover', label='hover shows the custom name', contains="Alice's Loot", target='chest'),
    H('pieceHover', label='builder sees the Rename hint', contains='Rename', target='chest'),
    play('Bob'),
    H('pieceHover', label='others see the name', contains="Alice's Loot", target='chest'),
    H('pieceHover', label='others get no hint', notContains='Rename', target='chest'),
    H('pieceRename', False, 'others cannot rename', target='chest', text="Bob's now"),
    H('pieceName', "Alice's Loot", target='chest'),
    ward('Bob'),
    wait(0.5),
    play('Alice'),
    H('pieceCan', False, 'builder without ward access cannot rename', target='chest'),
    play('me'),
], desc='The builder renames a chest, everyone sees the name, others and players without ward access cannot rename it.')

T('PR-02', 'shared piece: others with ward access may rename', [
    play('Alice'),
    spawn('piece_workbench', 'bench', 'Alice'),
    H('pieceIsShared', False, 'not shared by default', stop=True, target='bench'),
    play('Bob'),
    H('pieceShare', False, 'others cannot turn Shared on', target='bench', on=True),
    H('pieceCan', False, 'not shared: others cannot rename', target='bench'),
    play('Alice'),
    H('pieceShare', True, 'builder turns Shared on', target='bench', on=True),
    H('pieceIsShared', True, target='bench'),
    play('Bob'),
    H('pieceCan', True, 'shared: others may rename', target='bench'),
    H('pieceRename', True, target='bench', text='Village Bench'),
    H('pieceHover', contains='Village Bench', target='bench'),
    H('pieceShare', False, 'others cannot turn Shared off', target='bench', on=False),
    play('Alice'),
    H('pieceShare', True, target='bench', on=False),
    play('Bob'),
    H('pieceCan', False, 'unshared again: others locked out', target='bench'),
    play('me'),
], desc='The builder turns Shared on, then another player may rename it but not change Shared; off locks them out again.')

T('PR-03', 'piece Workshop window: type, save, close', [
    C({'11 UI-NotSynced/UseToolkitUi': 'true'}),
    spawn('piece_chest_wood', 'chest'),
    H('pieceOpen', True, 'Shift+Use opens the piece Workshop', stop=True, target='chest'),
    wait(0.4),
    check('uiVisible', 'the Shared switch is there', name='Shared'),
    check('uiVisible', 'the Reset name button is there', name='Reset name'),
    shot('piece-workshop'),
    {'do': 'type', 'field': 'Name', 'text': 'Typed Chest'},
    wait(0.2),
    {'do': 'click', 'button': 'Save'},
    wait(0.3),
    H('pieceName', 'Typed Chest', 'Save writes the name', target='chest'),
    *CLOSE,
    H('pieceOpen', True, target='chest'),
    wait(0.3),
    {'do': 'type', 'field': 'Name', 'text': 'Not Saved'},
    {'do': 'pressKey', 'key': 'Escape'},
    wait(0.3),
    H('pieceName', 'Typed Chest', 'Esc closes without saving', target='chest'),
    *CLOSE,
], desc='The UI Toolkit piece Workshop opens, saves through its own Save button and Esc does not save.')

T('PR-04', 'hover shows only the new name', [
    spawn('iron_grate', 'gate', dz=3, dx=-3),
    spawn('piece_bathtub', 'tub', dz=4, dx=3),
    spawn('piece_chest_wood', 'chest', dz=3, dx=8),
    H('pieceRename', True, stop=True, target='gate', text='Special Gate'),
    H('pieceHoverTitle', 'Special Gate', 'gate title is the new name only', target='gate'),
    H('pieceHover', label='old gate name gone', notContains='Iron', target='gate'),
    H('pieceRename', True, stop=True, target='tub', text='Hot Tubyy'),
    H('pieceHoverTitle', 'Hot Tubyy', 'hot tub seat title is the new name only', target='tub'),
    H('pieceHover', label='seat name gone', notContains='Wood Bench', target='tub'),
    H('pieceRename', True, target='chest', text='Loot'),
    H('pieceHoverTitle', label='chest title has the new name', contains='Loot', target='chest'),
    H('pieceHoverTitle', label='old chest name gone', notContains='Chest', target='chest'),
    H('pieceHoverTitle', label='chest keeps ( Empty )', contains='Empty', target='chest'),
    H('pieceRename', True, target='gate', text=''),
    H('pieceHover', label='empty name restores the original', notContains='Special Gate', target='gate'),
], desc='Bug found by hand: pieces whose hover name differs from the piece name (iron gate, the hot tub seat "Wood Bench") showed the new name above the old one. The hot tub heater keeps vanilla Shift+Use.')

# one piece of every kind: (prefab, tag, category RenameIt matches)
KINDS = [
    ('piece_chest_wood', 'chest', 'Chest'),
    ('piece_chest_private', 'private', 'Chest'),
    ('piece_workbench', 'workbench', 'Station'),
    ('forge', 'forge', 'Station'),
    ('piece_stonecutter', 'stonecutter', 'Station'),
    ('piece_artisanstation', 'artisan', 'Station'),
    ('smelter', 'smelter', 'Smelter'),
    ('charcoal_kiln', 'kiln', 'Smelter'),
    ('windmill', 'windmill', 'Smelter'),
    ('piece_spinningwheel', 'wheel', 'Smelter'),
    ('piece_cookingstation', 'spit', 'Cooking'),
    ('fermenter', 'fermenter', 'Cooking'),
    ('wood_door', 'door', 'Door'),
    ('iron_grate', 'gate', 'Door'),
    ('bed', 'bed', 'Bed'),
    ('portal_wood', 'portal', 'Portal'),
    ('piece_chair', 'chair', 'Seat'),
    ('piece_beehive', 'beehive', None),
]

def kinds_row(kinds, dz=4, step=4.5):
    x0 = -step * (len(kinds) - 1) / 2
    return [spawn(p, t, dz=dz, dx=round(x0 + i * step, 1)) for i, (p, t, _) in enumerate(kinds)]

steps = kinds_row(KINDS[:9]) + kinds_row(KINDS[9:], dz=12) + [wait(0.5)]
for prefab, tag, cat in KINDS:
    steps += renames(tag, f'My {tag}', f'{prefab} can be renamed')
    if cat:
        steps.append(H('pieceCategories', label=f'{prefab} counts as {cat}', contains=cat, target=tag))
# ward last, away from the rest, so it does not cover the others
steps += [spawn('guard_stone', 'wardstone', dz=-14), *renames('wardstone', 'My ward')]
steps.append(H('pieceCategories', label='guard_stone counts as Ward', contains='Ward', target='wardstone'))
T('PR-05', 'one piece of every kind can be renamed', steps, timeout=180,
  desc='Chests, stations, smelters and other processors, cooking, doors and gates, bed, portal, seat, beehive and the ward: each renames and shows the new name, and each reports the category PieceExcludedCategory uses.')

T('PR-06', 'skipped pieces: fires, item stands, signs, plain walls', [
    spawn('fire_pit', 'fire', dz=3, dx=-4),
    spawn('itemstandh', 'stand', dz=3),
    spawn('sign', 'sign', dz=3, dx=4),
    wait(0.3),
    H('pieceNameable', False, 'fire pit: vanilla Shift+Use (fuel) stays', target='fire'),
    H('pieceNameable', False, 'item stand: vanilla Shift+Use stays', target='stand'),
    H('pieceNameable', False, 'sign: has its own text', target='sign'),
    spawn('piece_bathtub', 'tub', dz=-4),
    H('pieceNameable', True, 'hot tub seat is renamable (its heater is not)', target='tub'),
], desc='Pieces where vanilla already uses Shift+Use, or that have their own text, are left alone.')

T('PR-07', 'PieceRenameEnabled off: nobody renames, names hidden', [
    spawn('piece_chest_wood', 'chest'),
    *renames('chest', 'Hidden Loot'),
    C({PC + 'PieceRenameEnabled': 'false'}),
    H('pieceNameable', False, 'off: Shift+Use does not offer renaming', target='chest'),
    H('pieceCan', False, 'off: builder cannot rename', target='chest'),
    H('pieceHover', label='off: the custom name is hidden', notContains='Hidden Loot', target='chest'),
    C({PC + 'PieceRenameEnabled': 'true'}),
    H('pieceHover', label='back on: the name is still there', contains='Hidden Loot', target='chest'),
], desc='Turning the feature off hides names without deleting them.')

T('PR-08', 'PieceRenameAdminOnly: only admins / VIP', [
    C({PC + 'PieceRenameAdminOnly': 'true', **VIP}),
    spawn('piece_chest_wood', 'chest'),
    H('pieceCan', False, 'admin-only: the builder cannot rename', target='chest'),
    play('Nacho-VIP'),
    H('pieceCan', True, 'admin-only: a VIP can', target='chest'),
    H('pieceRename', True, target='chest', text='VIP Chest'),
    play('Bob'),
    H('pieceHover', label='everyone still sees the name', contains='VIP Chest', target='chest'),
    play('me'),
    C({'01 Admin/AllowAdminOverride': 'false'}),
    play('Nacho-VIP'),
    H('pieceCan', False, 'AllowAdminOverride off: the VIP is a normal player again', target='chest'),
    play('me'),
], desc='Admin-only mode, and admins/VIP need AllowAdminOverride.')

T('PR-09', 'PieceExcludedNames: prefab, token or shown name', [
    spawn('piece_chest_wood', 'chest', dx=-4),
    spawn('piece_workbench', 'workbench'),
    spawn('wood_door', 'door', dx=4),
    C({PC + 'PieceExcludedNames': 'piece_chest_wood'}),
    H('pieceCan', False, 'excluded by prefab name', target='chest'),
    H('pieceCan', True, 'other pieces unaffected', target='workbench'),
    C({PC + 'PieceExcludedNames': '$piece_workbench'}),
    H('pieceCan', False, 'excluded by token', target='workbench'),
    H('pieceCan', True, 'the chest is free again', target='chest'),
    C({PC + 'PieceExcludedNames': 'Wood door'}),
    H('pieceCan', False, 'excluded by the name players see (any case)', target='door'),
    C({PC + 'PieceRenameAllowlist': 'wood_door'}),
    H('pieceCan', True, 'PieceRenameAllowlist beats the exclusion', target='door'),
    C({PC + 'PieceRenameAllowlist': '', **VIP}),
    play('Nacho-VIP'),
    H('pieceCan', True, 'admins / VIP ignore exclusions', target='door'),
    play('me'),
], desc='Each name format, the allowlist, and the admin bypass.')

T('PR-10', 'PieceExcludedCategory: what a piece does, or its build category', [
    spawn('piece_chest_wood', 'chest', dx=-6),
    spawn('piece_chest_private', 'private', dx=-2),
    spawn('piece_workbench', 'workbench', dx=2),
    spawn('smelter', 'smelter', dx=6),
    spawn('wood_door', 'door', dz=8),
    C({PC + 'PieceExcludedCategory': 'Chest'}),
    H('pieceCan', False, 'Chest excludes the wood chest', target='chest'),
    H('pieceCan', False, 'Chest excludes the private chest', target='private'),
    H('pieceCan', True, 'the workbench is unaffected', target='workbench'),
    C({PC + 'PieceExcludedCategory': 'Smelter,Door'}),
    H('pieceCan', False, 'Smelter excludes the smelter', target='smelter'),
    H('pieceCan', False, 'Door excludes the door', target='door'),
    H('pieceCan', True, 'chests are free again', target='chest'),
    C({PC + 'PieceExcludedCategory': 'Crafting'}),
    H('pieceCategories', label='the workbench is in the Crafting build category', contains='Crafting', target='workbench'),
    H('pieceCan', False, 'a build-menu category works too', target='workbench'),
    C({PC + 'PieceExcludedCategory': 'Chest', PC + 'PieceRenameAllowlist': 'piece_chest_private'}),
    H('pieceCan', True, 'allowlist lets one chest through', target='private'),
    H('pieceCan', False, 'the other chests stay excluded', target='chest'),
], desc='Function categories (Chest, Smelter, Door, …), build-menu categories, and the allowlist.')

T('PR-11', 'PieceNameCharacterLimit in the window', [
    C({PC + 'PieceNameCharacterLimit': '5', '11 UI-NotSynced/UseToolkitUi': 'true'}),
    spawn('piece_chest_wood', 'chest'),
    H('pieceOpen', True, stop=True, target='chest'),
    wait(0.3),
    {'do': 'type', 'field': 'Name', 'text': 'ABCDEFGHIJ'},
    wait(0.2),
    {'do': 'click', 'button': 'Save'},
    wait(0.3),
    H('pieceName', 'ABCDE', 'the name stops at 5 characters', target='chest'),
    *CLOSE,
], desc='The limit is per piece setting, separate from the item NameCharacterLimit.')

T('PR-12', 'PieceLockToBuilder off: anyone the ward allows', [
    C({PC + 'PieceLockToBuilder': 'false'}),
    play('Alice'),
    spawn('piece_chest_wood', 'chest', 'Alice'),
    play('Bob'),
    H('pieceCan', True, 'no ward: Bob may rename Alice\'s chest', target='chest'),
    H('pieceRename', True, target='chest', text='Bob was here'),
    play('Alice'),
    ward('Alice'),
    wait(0.5),
    play('Bob'),
    H('pieceCan', False, 'inside Alice\'s ward Bob cannot', target='chest'),
    play('me'),
], desc='With the builder lock off, ward access decides.')

T('PR-13', 'PieceRequireWardAccess off: wards are ignored', [
    play('Alice'),
    spawn('piece_chest_wood', 'chest', 'Alice'),
    ward('Bob'),
    wait(0.5),
    H('pieceCan', False, 'default: Bob\'s ward stops the builder', target='chest'),
    C({PC + 'PieceRequireWardAccess': 'false'}),
    H('pieceCan', True, 'ward access off: the builder may rename', target='chest'),
    play('Bob'),
    H('pieceCan', False, 'still only the builder (PieceLockToBuilder)', target='chest'),
    play('me'),
], desc='The ward rule can be turned off; the builder rule still applies.')

T('PR-14', 'PieceSharedEnabled off: no Shared', [
    play('Alice'),
    spawn('piece_workbench', 'bench', 'Alice'),
    H('pieceShare', True, target='bench', on=True),
    play('Bob'),
    H('pieceCan', True, 'shared: Bob may rename', target='bench'),
    C({PC + 'PieceSharedEnabled': 'false'}),
    H('pieceCan', False, 'Shared off: the shared piece acts as not shared', target='bench'),
    play('Alice'),
    H('pieceShare', False, 'Shared off: the builder cannot switch it', target='bench', on=False),
    play('me'),
], desc='Turning Shared off hides the switch and ignores Shared marks.')

T('PR-15', 'world-generated pieces: admins only', [
    C({PC + 'PieceLockToBuilder': 'false', **VIP}),
    spawn('TreasureChest_meadows', 'loot', dx=-3),
    spawn('wood_door', 'cryptdoor', dx=3),
    H('pieceWorldBuilt', True, 'the chest has no builder, like in a dungeon', stop=True, target='loot'),
    H('pieceWorldBuilt', True, 'the door has no builder, like a crypt door', stop=True, target='cryptdoor'),
    H('pieceCan', False, 'a player cannot rename a dungeon chest (even with PieceLockToBuilder off)', target='loot'),
    H('pieceCan', False, 'a player cannot rename a dungeon door', target='cryptdoor'),
    H('pieceHover', label='and gets no Rename hint', notContains='Rename', target='loot'),
    play('Nacho-VIP'),
    H('pieceCan', True, 'an admin / VIP can', target='loot'),
    H('pieceRename', True, target='loot', text='Crypt Hoard'),
    play('me'),
    H('pieceHover', label='everyone sees the name the admin gave', contains='Crypt Hoard', target='loot'),
], desc='Pieces the world generated (dungeon chests, crypt doors) have no builder: only admins/VIP may rename them, whatever the builder and Shared settings say. A walk-into-a-real-dungeon check needs TestLab dungeon support (follow-up).')

UIC = '11 UI-NotSynced/'

T('PC-01', 'builder line on renamed pieces: Off / On / Forced and wording', [
    spawn('piece_chest_wood', 'chest', 'me'),
    H('pieceRename', True, stop=True, target='chest', text='Fjord Chest'),
    H('pieceHover', label='Off (the suite default): no builder line', notContains='Crafted by', target='chest'),
    C({PC + 'PieceCrafterLine': 'On', UIC + 'ShowPieceCrafter': 'true'}),
    H('pieceHover', label='On and the player allows it: the builder line shows', contains='Crafted by: ', target='chest'),
    C({UIC + 'ShowPieceCrafter': 'false'}),
    H('pieceHover', label='On but the player turned it off: no line', notContains='Crafted by', target='chest'),
    C({PC + 'PieceCrafterLine': 'Forced'}),
    H('pieceHover', label='Forced ignores the player choice', contains='Crafted by: ', target='chest'),
    C({PC + 'PieceCrafterLineStyle': 'Built by'}),
    H('pieceHover', label='wording follows PieceCrafterLineStyle', contains='Built by: ', target='chest'),
    C({PC + 'PieceCrafterLine': 'Off'}),
    H('pieceHover', label='Off again: gone', notContains='Built by', target='chest'),
    C({PC + 'PieceCrafterLineStyle': 'Crafted by', UIC + 'ShowPieceCrafter': 'true'}),
], desc='A renamed piece can show who built it as its own hover line. Server: Off / On / Forced and Crafted by / Built by; player: ShowPieceCrafter.')

T('PC-02', 'quick names: suggestions as you type', [
    H('quickNamesSet', True, text=''),
    H('quickNamesAdd', label='saving remembers the name', contains='pieces:Fjord Runner', kind='pieces', text='Fjord Runner'),
    H('quickNamesAdd', kind='animals', text='Fenrir'),
    H('quickNamesAdd', kind='pieces', text='<color=#5dd96a>Fen</color>Home'),
    H('quickNamesSuggest', label='typing "fj" offers Fjord Runner', contains='Fjord Runner', kind='pieces', typed='fj'),
    H('quickNamesSuggest', label='matching ignores the colour tags', contains='Home', kind='pieces', typed='fenh'),
    H('quickNamesSuggest', label='other kinds are not offered (no animal names for a piece)', notContains='Fenrir', kind='pieces', typed='fen'),
    H('quickNamesSuggest', label='the same kind is', contains='Fenrir', kind='animals', typed='fen'),
    H('quickNamesSuggest', label='an old plain pieces entry still suits any piece', contains='Fjord Runner', kind='pieces/Boat', typed='fj'),
    H('quickNamesSuggest', label='but a boat name is not offered for an item', notContains='Fjord Runner', kind='items', typed='fj'),
    H('quickNamesSuggest', label='an exact match is not suggested again', notContains='Fjord Runner', kind='pieces', typed='Fjord Runner'),
    H('quickNamesSuggest', label='nothing matches', expect='', kind='pieces', typed='zzz'),
    H('quickNamesAdd', label='a repeat moves to the top, not a duplicate', notContains='Fjord Runner||Fjord Runner', kind='pieces', text='Fjord Runner'),
    C({UIC + 'QuickNamesEnabled': 'false'}),
    H('quickNamesSuggest', label='off: no suggestions', expect='', kind='pieces', typed='fj'),
    C({UIC + 'QuickNamesEnabled': 'true'}),
    H('quickNamesSet', True, text=''),
], desc='The Name fields suggest names you saved before, only for the same kind of thing (items, a piece type like Boat or Chest, animals...); the drop-down itself is checked by hand.')

AC = '13 Animals/'

def pet(tag='pet', dx=0):
    s = {'do': 'spawn', 'prefab': 'Boar', 'dz': 4, 'as': tag}
    if dx:
        s['dx'] = dx
    return s

def tame(tag='pet'):
    return [H('animalTame', True, 'the animal is tamed', stop=True, target=tag), wait(0.5)]

T('AN-01', 'animal names: colours kept, limit, colour switch', [
    pet(),
    *tame(),
    H('animalCan', True, 'a tamed animal can be renamed', stop=True, target='pet'),
    H('animalSet', True, target='pet', text='<color=#5dd96a>Fen</color>rir'),
    wait(0.6),
    H('animalName', label='stored with its colour', contains='<color=#5dd96a>', target='pet'),
    H('animalHoverName', label='the label keeps the colour (vanilla strips it)', contains='<color=#5dd96a>', target='pet'),
    H('animalSanitize', label='the limit counts visible characters only', expect='<color=#5dd96a>Fen</color>rir', text='<color=#5dd96a>Fen</color>rir'),
    H('animalSanitize', label='a name over the limit (30) is cut', expect='abcdefghijklmnopqrstuvwxyz1234', text='abcdefghijklmnopqrstuvwxyz1234567890'),
    C({AC + 'AnimalNameCharacterLimit': '10'}),
    H('animalSanitize', label='the server limit applies', expect='abcdefghij', text='abcdefghijklmnopqrstuvwxyz'),
    C({AC + 'AnimalAllowColour': 'false'}),
    H('animalSanitize', label='colours off: tags stripped', expect='Fenrir', text='<color=#5dd96a>Fen</color>rir'),
], desc='Tamed animal names keep their colours in the label, honour the character limit (colour codes not counted) and the colour switch.')

T('AN-02', 'animal names: who may rename (Anyone / Namer / Admin) and the lock', [
    pet(),
    *tame(),
    H('animalSet', True, 'I name it first', target='pet', text='Biscuit'),
    wait(0.6),
    play('Alice'),
    H('animalCan', True, 'Anyone (the default): others may rename', target='pet'),
    C({AC + 'AnimalRenameAccess': 'Namer'}),
    H('animalCan', False, 'Namer: only whoever named it first', target='pet'),
    play('me'),
    H('animalCan', True, 'the namer still can', target='pet'),
    C({AC + 'AnimalRenameAccess': 'Anyone'}),
    H('animalLock', True, 'the namer locks the name', target='pet', on=True),
    wait(0.6),
    H('animalLocked', True, 'it is locked', target='pet'),
    play('Alice'),
    H('animalCan', False, 'locked: others may not rename', target='pet'),
    H('animalLock', False, 'and may not unlock it', target='pet', on=False),
    C({AC + 'AnimalRenameAccess': 'Admin'}),
    play('me'),
    H('animalCan', False, 'Admin only: not even the namer (no override in the suite baseline)', target='pet'),
    C({AC + 'AnimalRenameAccess': 'Anyone'}),
], desc='Anyone (vanilla) / Namer / Admin access, and the per-animal lock set by the player who named it.')

T('AN-03', 'animal window opens and closes', [
    pet(),
    *tame(),
    H('animalOpen', True, 'the window opens', stop=True, target='pet'),
    check('uiVisible', 'the animal window is showing', name='Lock name'),
    *CLOSE,
], desc='The Toolkit window for a tamed animal.')

T('MP-01', 'map pins: edit after placing (name colours, tint, icon) and remove', [
    H('pinAdd', True, 'a pin on the map', stop=True, text='Troll cave'),
    H('pinInfo', label='starts plain', contains='Troll cave|Troll cave||Icon3|False'),
    H('pinEdit', True, text='<color=#d8584a>Troll</color> cave', colour='#5cc6b8', type='Icon0', checked=True),
    H('pinInfo', label='the game keeps the plain name', contains='Troll cave|'),
    H('pinInfo', label='our styled name has the colour', contains='<color=#d8584a>Troll</color> cave|#5cc6b8|Icon0|True'),
    H('pinStored', label='saved on the character per world', contains='#5cc6b8'),
    H('pinEdit', True, text='', colour='', type='Icon1', checked=False),
    H('pinInfo', label='the name is cleared, tint gone, icon changed', contains='||Icon1|False'),
    H('pinStored', label='nothing left to store for a plain pin', notContains='#5cc6b8'),
    H('pinRemove', True),
], desc='Pins can be renamed with colours, tinted, given another icon and removed after they are placed. Vanilla keeps only the plain name; the styling lives on the character.')

T('MP-02', 'map pins: the editor window opens', [
    H('pinAdd', True, stop=True, text='Camp'),
    H('pinOpen', True, 'the pin editor opens', stop=True),
    check('uiVisible', 'the editor is showing', name='Pin colour'),
    *CLOSE,
    H('pinRemove', True),
], desc='The Toolkit pin editor (Shift + click a pin on the map).')

# ---------------------------------------------------------------- write

DESCRIPTION = ('Automated regression for Drake\'s RenameIt, following "Drake\'s RenameIt Test Suite 2.0" (SM-/TC- ids) plus extra config coverage (X-). '
               'Other players are simulated with play-as (Alice, Bob, Nacho-VIP). Visual / UI / multiplayer checks are in DrakesRenameIt-Manual.')

# the shared baseline (both suites include it): what the suite needs, the sheet's INITIAL TEST config, the start state
baseline = {
    'requireMods': [{'guid': GUID, 'minVersion': '1.2.0'}],
    'configOverrides': [{'guid': GUID, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for k, v in {
        # the sheet's INITIAL TEST column, except: unlock cost off by default (its own tests turn it on),
        # admin override off so the local host is a normal player, flat paper placement for pinning tests
        '01 Admin/AllowAdminOverride': 'false', '01 Admin/VipOnlyOverride': 'false', '01 Admin/VipList': '',
        '02 Features/RenameEnabled': 'true', '02 Features/RewriteDescriptionsEnabled': 'true',
        '02 Features/CraftedByLabelEnabled': 'true', '02 Features/PublicRewriteEnabled': 'true',
        '03 Exclusions/ExcludedNames': 'AtgeirBronze', '03 Exclusions/ExcludedCategory': 'Clubs', '03 Exclusions/RenameAllowlist': 'Torch',
        '04 CraftedBy/LabelCustomizable': 'true', '04 CraftedBy/AllowedLabels': 'Crafted By, Belongs To, Return to',
        '05 General/LockToOwner': 'true', '05 General/NameClaimsOwner': 'true', '05 General/AllowRenameUnownedItems': 'true',
        '05 General/ShowDenialUi': 'true', '05 General/ShowReason': 'true', '05 General/ShowItemStandItemNameWhenNoAccess': 'true',
        '05 General/ServerDefaultMenuOpenModifier': 'Shift',
        '06 Stacks/SeparateStacks': 'true', '06 Stacks/SeparateStacksHardLock': 'true', '06 Stacks/ExcludeStacks': 'false',
        '07 UnlockCost/UnlockCostEnabled': 'false', '07 UnlockCost/UnlockCost': 'Coins:5',
        '08 Limits/NameCharacterLimit': '50', '08 Limits/CraftedByCharLimit': '50', '08 Limits/DescriptionCharacterLimit': '1000',
        '09 Modifiers/DurabilityModifierEnabled': 'false',
        '10 Paper/PaperEnabled': 'true', '10 Paper/PaperName': 'Piece of Paper', '10 Paper/PaperPlaceEnabled': 'true',
        '10 Paper/PaperTakePublicEnabled': 'true', '10 Paper/PaperHoldToTake': 'true', '10 Paper/PaperWallRenameEnabled': 'true',
        '10 Paper/PaperRecycleEnabled': 'true', '10 Paper/PaperRecycleImmutableEnabled': 'false',
        '10 Paper/WrittenPaperIgnoresRestrictions': 'false', '10 Paper/BlankPaperPlaceOrientation': 'Both',
        '10 Paper/VerticalPaperPlaceable': 'true', '10 Paper/HorizontalPaperPlaceable': 'true', '10 Paper/StackPaperPlaceable': 'true',
        '11 UI-NotSynced/MenuOpenModifier': '', '11 UI-NotSynced/LogSpam': 'false',
        '12 Pieces/PieceRenameEnabled': 'true', '12 Pieces/PieceRenameAdminOnly': 'false', '12 Pieces/PieceExcludedNames': '',
        '12 Pieces/PieceExcludedCategory': '', '12 Pieces/PieceRenameAllowlist': '', '12 Pieces/PieceNameCharacterLimit': '50',
        '12 Pieces/PieceLockToBuilder': 'true', '12 Pieces/PieceRequireWardAccess': 'true', '12 Pieces/PieceSharedEnabled': 'true',
        '12 Pieces/PieceCrafterLine': 'Off', '12 Pieces/PieceCrafterLineStyle': 'Crafted by', '11 UI-NotSynced/ShowPieceCrafter': 'true',
        '13 Animals/AnimalRenameEnabled': 'true', '13 Animals/AnimalNameCharacterLimit': '30', '13 Animals/AnimalAllowColour': 'true',
        '13 Animals/AnimalRenameAccess': 'Anyone', '13 Animals/AnimalAllowLock': 'true',
        '13 Animals/AnimalRenameEnabled': 'true', '13 Animals/AnimalNameCharacterLimit': '30', '13 Animals/AnimalAllowColour': 'true',
        '13 Animals/AnimalRenameAccess': 'Anyone', '13 Animals/AnimalAllowLock': 'true',
    }.items()],
    'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1, 'noBuild': 'off', 'hideUnlockPopups': 1},
}

suite = {
    'name': 'DrakesRenameIt',
    'description': DESCRIPTION,
    'include': '_shared/baseline.json',  # functions come from _functions/ next to this file
    'resetBetweenTests': True,
}

def dump(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)

for d in (AUTO, MANUAL):
    if os.path.isdir(d):
        shutil.rmtree(d)
    os.makedirs(d)

dump(os.path.join(AUTO, 'suite.json'), suite)
dump(os.path.join(AUTO, '_shared', 'baseline.json'), baseline)
for name, block in BLOCKS.items():
    dump(os.path.join(AUTO, '_functions', f'{name}.json'), block)
for tid, t in tests.items():
    with open(os.path.join(AUTO, f'{tid}.json'), 'w', encoding='utf-8') as f:
        json.dump(t, f, indent=2, ensure_ascii=False)

# ---------------------------------------------------------------- manual suite (checkpoints)

manual = {
    'name': 'DrakesRenameIt-Manual',
    'description': 'What a person has to look at or press: real keys and windows, language, relaunch, other mods, real multiplayer. Run it after the automated suite. Each test sets things up and asks you Pass / Fail.',
    # the same baseline and blocks as the automated suite
    'include': ['../DrakesRenameIt/_shared/baseline.json', '../DrakesRenameIt/_functions'],
    'resetBetweenTests': True,
}
mtests = {
    'M-TC-502': ('item info for another real player', [
        U('craftRenamed', text='Shared Look', desc='Seen by both'),
        H('edit', True, prefab='AxeStone', op='craftedBy', text='Made by tester'),
        U('itemOnStand'),
        {'do': 'checkpoint', 'message': 'A renamed axe (Shared Look / Seen by both / Made by tester) is on the stand in front of you. With a second REAL player joined: they see the same name, description and crafted-by on the stand and after taking it, and they cannot rename it (it is yours). Mark Fail if no second player is available.'}], 'TC-502..504'),
    'M-TC-501': ('server lock and VIP list (dedicated server)', [
        {'do': 'checkpoint', 'message': 'Needs a dedicated server and a non-admin client: the client cannot change synced RenameIt settings (LockSyncedConfig) and cannot add itself to VipList (ISSUE-019, open). Mark Fail if not tested.'}], 'TC-501, ISSUE-019'),
    'M-TC-4xx': ('other mods', [
        U('craftRenamed', text='Mod Check', desc='With other mods'),
        {'do': 'checkpoint', 'message': 'With the modpack loaded (VRP, Shrooms, RPChatter, PlanBuild, InfinityHammer, Ward is Love, Protective Wards, Arcane Wards): the renamed axe in your inventory keeps its name and description, no errors, and wards from those mods are respected. PlanBuild losing data on stands/walls is known (ISSUE-003). Mark Fail if not tested.'}], 'TC-401..408'),
}
for tid, (title, steps, sheet) in mtests.items():
    t = {'name': f'{tid} {title}', 'description': f'Sheet: {sheet}.', 'manualOnly': True, 'steps': steps + [{'do': 'closeUi'}], 'asserts': [NOERR]}
    with open(os.path.join(MANUAL, f'{tid}.json'), 'w', encoding='utf-8') as f:
        json.dump(t, f, indent=2, ensure_ascii=False)
with open(os.path.join(MANUAL, 'suite.json'), 'w', encoding='utf-8') as f:
    json.dump(manual, f, indent=2, ensure_ascii=False)

print(len(tests), 'automated,', len(mtests), 'manual')
