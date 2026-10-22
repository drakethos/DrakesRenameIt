# Writes Tests/suites/DrakesRenameIt (automated) and DrakesRenameIt-Manual. Repeated step runs live as DrakesTestLab
# blocks in DrakesRenameIt/_blocks/ (called with {"do": "use", ...}); the config baseline both suites share is
# DrakesRenameIt/_shared/baseline.json (pulled in with "include"). Run: python gen_renameit_suite.py
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites')
AUTO = os.path.join(OUT, 'DrakesRenameIt')
MANUAL = os.path.join(OUT, 'DrakesRenameIt-Manual')
GUID = 'com.drakesworkshop.renameit'

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

def U(block, **args):
    # call a block from _blocks/; args fill its {{params}}
    s = {'do': 'use', 'block': block}
    if args:
        s['args'] = args
    return s

def P(name):
    # a block parameter: "{{name}}" (alone in a string it passes the value as is: numbers, true, objects)
    return '{{' + name + '}}'

NOERR = {'check': 'noErrors', 'source': 'DrakesRenameit'}

VIP = {'01 Admin/AllowAdminOverride': 'true', '01 Admin/VipOnlyOverride': 'true', '01 Admin/VipList': 'Nacho-VIP'}

# ---------------------------------------------------------------- blocks (written to DrakesRenameIt/_blocks/<name>.json)
# params without a default are required; None = optional (a key that is only "{{param}}" is then left out)

def guarded(param, step):
    # the step only runs when the param has a value
    return {'if': P(param), **step}

BLOCKS = {
    'craftRenamed': {
        'description': 'Craft an item through the game (real owner) and rename it; optionally give it a description too.',
        'params': {'prefab': 'AxeStone', 'by': None, 'desc': None},
        'steps': [
            {'do': 'craft', 'prefab': P('prefab'), 'by': P('by')},
            H('edit', True, prefab=P('prefab'), op='name', text=P('text')),
            guarded('desc', H('edit', True, prefab=P('prefab'), op='desc', text=P('desc'))),
        ]},
    'editAll': {
        'description': 'Set name, description and crafted-by on an item (each edit must succeed).',
        'params': {'prefab': 'AxeStone'},
        'steps': [H('edit', True, prefab=P('prefab'), op=op, text=P(op)) for op in ('name', 'desc', 'craftedBy')]},
    'checkAll': {
        'description': 'Check the item shows the given name, description and crafted-by.',
        'params': {'prefab': 'AxeStone'},
        'steps': [H(op, contains=P(op), prefab=P('prefab')) for op in ('name', 'desc', 'craftedBy')]},
    'writePage': {
        'description': 'Turn a blank sheet into a written page named text (stops the test if that fails); desc = page text.',
        'params': {'desc': None, 'blanks': 1},
        'steps': [
            {'do': 'give', 'prefab': 'Drakes_PieceOfPaper', 'count': P('blanks')},
            H('edit', True, 'write the page', stop=True, prefab='Drakes_PieceOfPaper', op='name', text=P('text')),
            guarded('desc', H('edit', True, 'page text', prefab='Drakes_Paper_Written', match=P('text'), op='desc', text=P('desc'))),
        ]},
    'pinPage': {
        'description': 'Pin a page from the inventory 2 m ahead (place-mode code) and let it settle.',
        'params': {'prefab': 'Drakes_Paper_Written', 'label': None, 'dz': 2},
        'steps': [H('pin', True, P('label'), stop=True, prefab=P('prefab'), dz=P('dz')), wait(1.0)]},
    'itemOnStand': {
        'description': 'Spawn a horizontal item stand (tag "stand"), face it and put the item on it.',
        'params': {'prefab': 'AxeStone', 'label': None},
        'steps': [
            {'do': 'spawn', 'prefab': 'itemstandh', 'dz': 2.5, 'owner': 'me', 'as': 'stand'},
            {'do': 'face', 'target': 'stand'},
            H('standPut', True, P('label'), stop=True, prefab=P('prefab'), target='stand'),
            wait(0.5),
        ]},
}

tests = {}

def T(tid, title, steps, asserts=None, desc='', setup=None, sheet=None):
    t = {'name': f'{tid} {title}', 'description': (f'Sheet: {sheet}. ' if sheet else '') + desc, 'steps': steps,
         'asserts': (asserts or []) + [NOERR]}
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
    'params': {},
    'steps': stacks_setup(P('sep'), P('hard'))}

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
    }.items()],
    'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1, 'noBuild': 'off', 'hideUnlockPopups': 1},
}

suite = {
    'name': 'DrakesRenameIt',
    'description': DESCRIPTION,
    'include': '_shared/baseline.json',  # blocks come from _blocks/ next to this file
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
    dump(os.path.join(AUTO, '_blocks', f'{name}.json'), block)
for tid, t in tests.items():
    with open(os.path.join(AUTO, f'{tid}.json'), 'w', encoding='utf-8') as f:
        json.dump(t, f, indent=2, ensure_ascii=False)

# ---------------------------------------------------------------- manual suite (checkpoints)

manual = {
    'name': 'DrakesRenameIt-Manual',
    'description': 'What a person has to look at or press: real keys and windows, language, relaunch, other mods, real multiplayer. Run it after the automated suite. Each test sets things up and asks you Pass / Fail.',
    # the same baseline and blocks as the automated suite
    'include': ['../DrakesRenameIt/_shared/baseline.json', '../DrakesRenameIt/_blocks'],
    'resetBetweenTests': True,
}
mtests = {
    'M-SM-001': ('Shift+RMB opens the Workshop window', [give('AxeStone', crafter='me'), {'do': 'pressKey', 'button': 'Inventory'},
        {'do': 'checkpoint', 'message': 'Hover the stone axe and press Shift + Right click. The RenameIt window opens, the cursor works and you cannot move. Close it with Cancel. Pass?'}], 'SM-001, TC-101 step 2'),
    'M-TC-301': ('character limits in the window', [give('AxeStone', crafter='me'), C({'08 Limits/NameCharacterLimit': '10'}), {'do': 'pressKey', 'button': 'Inventory'},
        {'do': 'checkpoint', 'message': 'Open the window for the axe (Shift+RMB) and type more than 10 characters in Name. It stops at 10 (NameCharacterLimit = 10). Pass?'}], 'TC-301'),
    'M-TC-303': ('Cancel and Esc close the window without saving', [give('AxeStone', crafter='me'), {'do': 'pressKey', 'button': 'Inventory'},
        {'do': 'checkpoint', 'message': 'Open the window, change the name, press Cancel: the name is unchanged. Open it again, change it, press Esc: closes and unchanged. (Esc was a known issue in 1.2.1.) Pass?'}], 'TC-303'),
    'M-TC-214': ('menu keys for real', [give('AxeStone', crafter='me'), C({'11 UI-NotSynced/MenuOpenModifier': 'Ctrl'}), {'do': 'pressKey', 'button': 'Inventory'},
        {'do': 'checkpoint', 'message': 'MenuOpenModifier is now Ctrl: Ctrl + Right click opens the window and Shift + Right click does not. Pass?'}], 'TC-214'),
    'M-TC-105': ('landscape text looks right', [PIN_FLAT, *written('Look At Me', 'LINE 1\nLINE 2\nLINE 3\nLINE 4'),
        U('pinPage'), H('pageStyle', font=7, landscape=True),
        {'do': 'checkpoint', 'message': 'The page on the ground shows its text rotated 90 degrees with font 7 (only about LINE 1 and LINE 2 fit). Pass?'}], 'TC-105 step 9'),
    'M-SM-012': ('pin a page on a wall by hand', [*written('Wall Page'),
        {'do': 'checkpoint', 'message': 'Put the Written Page on the hotbar, select it and pin it on a house wall with the mouse (both orientations if Both). Then Shift+E opens Rename|Paper, and hold E takes it. Pass?'}], 'SM-012..014, TC-105 6-10'),
    'M-SM-015': ('menus in another language', [{'do': 'checkpoint', 'message': 'Settings > Language: switch to another language, open the RenameIt window and the Paper tab. Texts are translated or fall back to English, nothing shows raw $tokens. Switch back. Pass?'}], 'SM-015'),
    'M-SM-008': ('changes survive a relaunch', [{'do': 'checkpoint', 'message': 'Rename an item, save, quit to desktop and start again (DrakesTestLab boots the test world fresh, so load your own world/character for this). The custom name, description and crafted-by are still there. Pass?'}], 'SM-008'),
    'M-SM-010': ('craft paper at the inventory', [give('Wood', 5), give('Coal', 5), {'do': 'pressKey', 'button': 'Inventory'},
        {'do': 'checkpoint', 'message': 'In the inventory crafting list, Piece of Paper is there (no station needed) and crafting it uses 1 Wood + 1 Coal. Pass?'}], 'SM-010'),
    'M-HOVER': ('world hover text styles', [give('AxeStone', crafter='me'), H('edit', prefab='AxeStone', op='name', text='Hover Axe'),
        {'do': 'checkpoint', 'message': 'Drop the axe and look at it. Shift+F1 opens Quick Settings: try Vanilla / Minimal / Detailed, sizes S/M/L, Beside/Under, backdrop. The hover text follows each choice. Pass?'}], 'CONFIGS 11 UI (hover settings, not in the sheet)'),
    'M-TC-501': ('server lock and VIP list (dedicated server)', [{'do': 'checkpoint', 'message': 'Needs a dedicated server and two clients: a non-admin client cannot change synced settings (LockSyncedConfig) and cannot add itself to VipList (ISSUE-019, open). Mark Fail if not tested.'}], 'TC-501, ISSUE-019'),
    'M-TC-502': ('item info for another real player', [{'do': 'checkpoint', 'message': 'Two real players: a renamed item handed over or put on a stand shows the same name / description / crafted-by for the other player, and their edit rights match the automated play-as results. Mark Fail if not tested.'}], 'TC-502..504'),
    'M-TC-4xx': ('other mods', [{'do': 'checkpoint', 'message': 'With the modpack: VRP, Shrooms, RPChatter, PlanBuild (stands/walls lose data: known, ISSUE-003), InfinityHammer, Ward is Love, Protective Wards, Arcane Wards. No errors, wards respected. Mark Fail if not tested.'}], 'TC-401..408'),
}
for tid, (title, steps, sheet) in mtests.items():
    t = {'name': f'{tid} {title}', 'description': f'Sheet: {sheet}.', 'manualOnly': True, 'steps': steps + [{'do': 'closeUi'}], 'asserts': [NOERR]}
    with open(os.path.join(MANUAL, f'{tid}.json'), 'w', encoding='utf-8') as f:
        json.dump(t, f, indent=2, ensure_ascii=False)
with open(os.path.join(MANUAL, 'suite.json'), 'w', encoding='utf-8') as f:
    json.dump(manual, f, indent=2, ensure_ascii=False)

print(len(tests), 'automated,', len(mtests), 'manual')
