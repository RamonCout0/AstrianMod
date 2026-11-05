# Ascensão Astral — base (esqueleto)

Raids de MMORPG no Terraria. Esta é a **base**: tudo funciona de forma simples, com arte provisória,
para você polir uma peça por vez.

## Instalar (em qualquer PC)
Clone direto na pasta de mods do tModLoader, com o nome **AscensaoAstral** (o nome da pasta precisa ser esse):

```
cd "Documents/My Games/Terraria/tModLoader/ModSources"
git clone https://github.com/RamonCout0/AstrianMod AscensaoAstral
```

Depois: Luminance e Subworld Library ativas → **Build + Reload**. Os shaders já vão compilados (`.xnb`).
Para atualizar em outro PC: `git pull` dentro da pasta e Build + Reload.

## Ferramentas (não vão para dentro do mod)
- `Ferramentas/BancadaDeShaders/bancada.html` — abre no navegador e mostra um `.fx` rodando ao vivo.
- `Ferramentas/LaboratorioDeAtaques/` — `node rodar.js` simula cada ataque e diz se dá para sobreviver;
  `node rodar2.js` diz se ficar parado é punido.
- `Ferramentas/compilar_shaders.py` — compila os `.fx` com o fxc da Luminance e gera os `.xnb`.

## Testar rápido
1. Luminance ativa. **Build + Reload** no Ascensão Astral.
2. Mundo de teste em Hardmode (Cheat Sheet / HERO's Mod ajudam):
   - quebre um altar → aparece "O mundo está tendo uma interferência exterior..." e a Astrânia é gerada
   - minere um bloco de Astrânia → clarão, tremor, explosão, cratera com o **Eco Estelar**
   - fale com ele: botão 1 troca o caminho (classe), botão 2 **funde**
3. Silvanna: mate a Queen Slime, vá para a neve, use o **Chamado da Bruxa**.
4. Astharoth: mate o Golem, use a **Lâmina Errante**. Lute na superfície (o Éter é construído no alto, perto da borda esquerda do mundo).

A linha verde no rodapé é o diagnóstico (`AscensaoAstral.DEBUG` em `AscensaoAstral.cs`).

## Mapa dos arquivos

| Arquivo | O que é |
|---|---|
| `Common/Raid/RaidBoss.cs` | **Base de todas as raids**: arena, estados, stagger, portões, checkpoints, wipe global, despawn |
| `Common/Raid/RaidJogador.cs` | Alma + reviver (aliado PARADO 3 s), espectador, renascer no checkpoint, câmera de cutscene |
| `Common/Raid/EterEstrutura.cs` | Constrói/desmonta a ponte + arena do Éter no céu |
| `Common/AstralMundo.cs` | História do mundo: Astrânia, impacto, cratera, chefes derrotados |
| `Common/AstralJogador.cs` | Classe escolhida + fusão (bônus provisórios) |
| `Common/Dialogo.cs` | Caixa de diálogo das cutscenes + falas do Eco |
| `Common/Hud.cs` | Banner, avisos, símbolo, barra de quebra, clarão, debug |
| `Content/Cenario/Ceus.cs` | Planeta Astra (cresce por chefe; à esquerda com Calamity/WotG), Éter (buraco negro), Nuvens |
| `Content/Projectiles/Perigos.cs` | `RetanguloPerigo` (aviso → ativo), `Rajada`, `AlmaDoJogador` |
| `Content/Bosses/Silvanna/` | Raid da Silvanna + estátuas, escudos, varredura do chão, lança |
| `Content/Bosses/Astharoth/` | Raid do Astharoth + orbes do Arcanjo |

## Criar um chefe novo
Herde de `RaidBoss`, defina `StaggerMax`, escreva `ExecutarEstado()` com um `switch` dos seus estados e `AoReiniciar()`.
Dentro dos estados use: `IrPara`, `MoverAte`, `SalvarCheckpoint`, `AbrirPortao` + `PortaoQuebrado`, `WipeGlobal`,
`RetanguloPerigo.Criar(...)`, `Hud.Banner/Aviso`, `Dialogo.Falar`, `RaidJogador.Foco`.

## O que é provisório
- Sprites: cavaleiro de gelo (forma verdadeira tingida), clones, cavaleiros do céu, Astharoth, dragão (feito de luz), estátuas, Eco Estelar
- Falas e nomes (Eco Estelar, frases da Silvanna e do Astharoth)
- Bônus das classes, vida/dano dos chefes, receitas
- A etapa final do Astharoth (ainda em aberto no documento): usa os ataques anteriores
- Em multiplayer o código já sincroniza estados, sorteio das almas e orbes, mas ainda não foi testado com 4 pessoas

## Regras de multiplayer (já seguidas no código)
Só o servidor cria perigos e sorteia; cada cliente cuida do próprio jogador (puxão, vento, olhar o dragão,
mirar no clone); efeitos visuais só no cliente.
