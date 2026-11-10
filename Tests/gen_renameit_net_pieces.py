# Writes Tests/suites/DrakesRenameIt-Net/PN-20..22: one second client (Bjornita, a DrakesTestLab virtual player) against
# one piece of every kind. Run: python gen_renameit_net_pieces.py
#  PN-20  owner side: a Shared piece I own accepts Bjornita's rename RPC (SetName) and her Shared switch is refused.
#  PN-21  sender side: her client owns the piece, I rename it: the rename must go to her as an RPC (not claimed away).
#  PN-22  access side: as another player (playAs Alice) on a piece I built and shared: the rename is allowed and offered.
import json, os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesRenameIt-Net')
SERVER_OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'DrakesRenameIt-Server')

# one prefab per kind of thing a player can rename (see PieceReference.txt for the full list; PR-17 sweeps all of them locally)
PIECES = [
    'piece_chest_wood', 'piece_chest', 'piece_chest_private', 'piece_chest_blackmetal', 'piece_chest_barrel', 'Cart',
    'piece_workbench', 'forge', 'piece_cauldron', 'piece_stonecutter', 'piece_artisanstation', 'piece_magetable', 'blastfurnace',
    'smelter', 'charcoal_kiln', 'windmill', 'piece_spinningwheel', 'eitrrefinery', 'fermenter', 'piece_cookingstation',
    'piece_oven', 'piece_MeadCauldron', 'piece_cartographytable', 'piece_bed02', 'bed', 'portal_wood', 'wood_door', 'wood_gate',
    'piece_chair', 'piece_bench01', 'piece_throne01', 'Raft', 'Karve', 'piece_TrainingDummy', 'piece_barber', 'piece_beehive',
    'piece_sapcollector', 'piece_gift1',
]

BASE = {
    '12 Pieces/PieceRenameEnabled': 'true',
    '12 Pieces/PieceRenameAdminOnly': 'false',
    '12 Pieces/PieceExcludedCategory': '',
    '12 Pieces/PieceExcludedNames': '',
    '12 Pieces/PieceLockToBuilder': 'true',
    '12 Pieces/PieceSharedEnabled': 'true',
    '12 Pieces/PieceRequireWardAccess': 'false',
}


def hook(name, label=None, expect=None, contains=None, **args):
    s = {'do': 'hook', 'name': 'renameit.' + name}
    if args:
        s['args'] = args
    if expect is not None:
        s['expect'] = expect
    if contains is not None:
        s['contains'] = contains
    if label:
        s['label'] = label
    return s


def spawn(prefab, i):
    return {'do': 'spawn', 'prefab': prefab, 'dx': (i % 6) * 4 - 10, 'dz': 5 + (i // 6) * 4, 'owner': 'me', 'as': 'p%d' % i}


def test(name, desc, steps):
    return {
        'name': name, 'description': desc,
        'steps': [{'do': 'setConfig', 'values': BASE}, {'do': 'peerJoin', 'name': 'Bjornita'}, *steps],
        'asserts': [{'check': 'noErrors', 'source': 'DrakesRenameit'}],
        'timeout': 600,
    }


def build():
    a, b, c = [], [], []
    for i, p in enumerate(PIECES):
        a += [
            spawn(p, i),
            hook('pieceShare', '%s: I share it' % p, expect=True, target='p%d' % i, on=True),
            {'do': 'peerRpc', 'peer': 'Bjornita', 'target': 'p%d' % i, 'method': 'DrakePiece_RPC_SetName', 'args': ['Peer ' + p], 'types': ['string']},
            {'do': 'wait', 'seconds': 0.3},
            hook('pieceName', '%s: her rename was written' % p, contains='Peer ' + p, target='p%d' % i),
        ]
        b += [
            spawn(p, i),
            {'do': 'peerOwn', 'peer': 'Bjornita', 'target': 'p%d' % i},
            hook('pieceRename', '%s: I may rename it' % p, expect=True, target='p%d' % i, text='Mine ' + p),
            {'do': 'check', 'check': 'peerReceived', 'target': 'p%d' % i, 'method': 'DrakePiece_RPC_SetName', 'types': ['string'], 'args': ['Mine ' + p],
             'label': '%s: the name went to her client' % p},
            {'do': 'check', 'check': 'zdoOwner', 'target': 'p%d' % i, 'expect': 'Bjornita', 'label': '%s: and she still owns it' % p},
        ]
        c += [
            spawn(p, i),
            hook('pieceShare', '%s: I share it' % p, expect=True, target='p%d' % i, on=True),
            {'do': 'playAs', 'who': 'Alice'},
            hook('pieceCan', '%s: Alice may rename the shared piece' % p, expect=True, target='p%d' % i),
            hook('pieceHover', '%s: and is offered it' % p, contains='Rename', target='p%d' % i),
            hook('pieceRename', '%s: and it is written' % p, expect=True, target='p%d' % i, text='Alice ' + p),
            {'do': 'wait', 'seconds': 0.2},
            hook('pieceName', '%s: for everyone' % p, contains='Alice ' + p, target='p%d' % i),
            {'do': 'playAs', 'who': 'me'},
        ]
    return {
        'PN-20': test('PN-20 every kind of piece: her rename of my shared piece is written',
                      'The owner side for %d kinds of piece: a Shared piece accepts Bjornita\'s SetName RPC.' % len(PIECES), a),
        'PN-21': test('PN-21 every kind of piece: renaming one her client owns goes to her',
                      'The sender side for %d kinds of piece: I am the builder, she owns the piece, my rename must reach her client as an RPC and leave the owner alone.' % len(PIECES), b),
        'PN-22': test('PN-22 every kind of piece: another player may rename a shared piece',
                      'Access side for %d kinds of piece: as Alice, on a piece I built and shared, the rename is allowed, offered in the hover and written.' % len(PIECES), c),
    }


def p2(step):
    return {'on': 'player2', **step}


def build_server():
    # Real second client (Player 2) on the dedicated server; steps tagged "on": "player2" run on her game (see DrakesTestLab
    # docs/MULTIPLAYER.md). Needs Target = Server on Player 1 and Player 2 connected. Plan: docs/TWO-CLIENT-PIECE-SYNC.md.
    a, b, c = [], [], []
    for i, p in enumerate(PIECES):
        t = 'p%d' % i
        a += [
            spawn(p, i),
            hook('pieceShare', '%s: I share it' % p, expect=True, target=t, on=True),
            {'do': 'wait', 'seconds': 1.0},
            p2(hook('pieceCan', '%s: Player 2 may rename the shared piece' % p, expect=True, target=t)),
            p2(hook('pieceHover', '%s: and the Rename hint shows for her' % p, contains='Rename', target=t)),
            p2(hook('pieceRename', '%s: she renames it' % p, expect=True, target=t, text='P2 ' + p)),
            {'do': 'wait', 'seconds': 1.5},
            hook('pieceName', '%s: Player 1 sees her name' % p, contains='P2 ' + p, target=t),
            p2(hook('pieceName', '%s: and so does she' % p, contains='P2 ' + p, target=t)),
        ]
        b += [
            spawn(p, i),
            hook('pieceRename', '%s: I rename it' % p, expect=True, target=t, text='P1 ' + p),
            {'do': 'wait', 'seconds': 1.5},
            p2(hook('pieceName', '%s: Player 2 sees my name' % p, contains='P1 ' + p, target=t)),
            p2(hook('pieceHoverTitle', '%s: in her hover too' % p, contains='P1 ' + p, target=t)),
        ]
        c += [
            spawn(p, i),
            p2(hook('pieceCan', '%s: not shared, Player 2 may not' % p, expect=False, target=t)),
            hook('pieceShare', '%s: I share it' % p, expect=True, target=t, on=True),
            {'do': 'wait', 'seconds': 1.0},
            p2(hook('pieceCan', '%s: shared, she may' % p, expect=True, target=t)),
            hook('pieceShare', '%s: I turn Shared off' % p, expect=True, target=t, on=False),
            {'do': 'wait', 'seconds': 1.0},
            p2(hook('pieceCan', '%s: off again, she may not' % p, expect=False, target=t)),
        ]
    def t(name, desc, steps):
        return {'name': name, 'description': desc, 'steps': [{'do': 'setConfig', 'values': BASE}, *steps],
                'asserts': [{'check': 'noErrors', 'source': 'DrakesRenameit'}], 'timeout': 900}
    return {
        'VS-20': t('VS-20 every kind of piece: Player 2 renames my shared piece', 'Real second client against the server: she may, is offered it, renames it, and both clients read her name.', a),
        'VS-21': t('VS-21 every kind of piece: my rename reaches Player 2', 'I rename, she reads the name and hover title on her own client.', b),
        'VS-22': t('VS-22 every kind of piece: Shared on and off gates Player 2', 'Not shared: no. Shared: yes. Shared off again: no.', c),
    }


if __name__ == '__main__':
    for k, v in build().items():
        with open(os.path.join(OUT, k + '.json'), 'w', encoding='utf-8') as f:
            json.dump(v, f, indent=2)
    for k, v in build_server().items():
        with open(os.path.join(SERVER_OUT, k + '.json'), 'w', encoding='utf-8') as f:
            json.dump(v, f, indent=2)
    print('wrote', len(PIECES), 'pieces x 3 net tests + 3 server tests')
