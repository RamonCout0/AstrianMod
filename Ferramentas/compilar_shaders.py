"""Compila os .fx do mod com o fxc da Luminance (via wine) e empacota em .xnb do jeito que a Luminance gera."""
import os, sys, struct, subprocess, glob, shutil
S = '/tmp/claude-0/-home-claude/0c331114-2ed5-5bdf-bc69-2242849ed720/scratchpad'
COMP = os.path.join(S, 'fxcteste')
LEITOR = b'Microsoft.Xna.Framework.Content.EffectReader, Microsoft.Xna.Framework.Graphics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553'

def u7(n):
    out = b''
    while True:
        b = n & 0x7F; n >>= 7
        out += bytes([b | (0x80 if n else 0)])
        if not n: return out

def xnb(efeito):
    corpo = struct.pack('<I', 0xBCF00BCF) + struct.pack('<I', 16) + b'\0' * 8 + efeito
    conteudo = u7(1) + u7(len(LEITOR)) + LEITOR + struct.pack('<i', 0) + u7(0) + u7(1) + struct.pack('<I', len(corpo)) + corpo
    total = 10 + len(conteudo)
    return b'XNBw' + bytes([5, 1]) + struct.pack('<I', total) + conteudo

env = dict(os.environ, WINEDEBUG='-all', WINEPREFIX=os.path.join(S, 'wineprefix'), WINEDLLOVERRIDES='d3dcompiler_47=n')
ok = True
for fx in sorted(glob.glob(os.path.join(sys.argv[1], '**', '*.fx'), recursive=True)):
    nome = os.path.basename(fx)
    shutil.copy(fx, os.path.join(COMP, nome))
    r = subprocess.run(['wine', 'fxc.exe', '/nologo', '/T', 'fx_2_0', '/O3', '/Fo', 'saida.fxc', nome], cwd=COMP, env=env, capture_output=True, text=True, timeout=120)
    saida = os.path.join(COMP, 'saida.fxc')
    if r.returncode != 0 or not os.path.exists(saida):
        ok = False
        print('ERRO', nome, '\n', (r.stdout + r.stderr).strip()[-1500:])
        continue
    dados = open(saida, 'rb').read(); os.remove(saida)
    open(fx[:-3] + '.xnb', 'wb').write(xnb(dados))
    print('ok  ', nome, len(dados), 'bytes')
sys.exit(0 if ok else 1)
