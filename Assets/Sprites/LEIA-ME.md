# Sprites próprios: solte o PNG aqui e pronto

Quase toda a arte nova do mod é **provisória**: um item do Terraria tingido, ou um desenho feito em código.
Para trocar qualquer uma, coloque nesta pasta um PNG com **exatamente o nome da tabela** e recompile o mod
(Workshop → Develop Mods → Build + Reload). O código acha o arquivo sozinho, passa a usar ele e desliga a tinta.
Sem o arquivo, continua no provisório. Não precisa mexer em nenhum `.cs`.

Tamanhos são sugestões (pixel art 1×; o Terraria desenha em 2×, então desenhe na metade do tamanho que
quer ver na tela e amplie 2× sem suavizar, como os sprites do jogo).

## Itens (ícone no inventário e na mão)

| Arquivo | Item | Hoje usa (Terraria) | Tamanho sugerido |
|---|---|---|---|
| `GeloEterno.png` | Gelo Eterno (material) | Frost Core | 20×20 |
| `LancaDeArmotheus.png` | Lança de Armotheus | North Pole | 40×40 a 60×60, ponta para cima-direita |
| `CajadoDoPingente.png` | Cajado do Pingente | Blizzard Staff | 40×40, ponta para cima-direita |
| `LancadorDeAvalanche.png` | Lançador de Avalanche | Snowball Cannon | 50×24, cano para a direita |
| `CajadoDosCavaleiros.png` | Cajado dos Cavaleiros do Céu | Frost Staff | 40×40 |
| `PingenteDeArremesso.png` | Pingente de Arremesso (Rogue, só com Calamity) | Frost Daggerfish | 30×30 |
| `CoracaoDeArmotheus.png` | Coração de Armotheus (Expert) | Life Crystal | 28×28 |
| `BolsaDaSilvanna.png` | Bolsa do Tesouro da Silvanna | bolsa da Queen Slime | 32×32 |
| `ChapeuDeArmotheus.png` | Chapéu de Armotheus (máscara) | Wizard Hat | 28×20 |
| `TrofeuDaSilvanna.png` | Troféu da Silvanna (ícone) | troféu da Queen Slime | 32×32 |
| `RelicariaDaSilvanna.png` | Relíquia da Silvanna (ícone) | relíquia da Queen Slime | 30×40 |
| `CartolaDeArmotheus.png` | Cartola de Armotheus (item do pet) | Magic Hat | 28×28 |
| `EssenciaDoInverno.png` | Essência do Inverno (material da revanche) | Stardust Fragment | 22×22 |
| `CoroaDoInvernoEterno.png` | Coroa do Inverno Eterno | Gold Crown | 28×24 |
| `FragmentoDoEter.png` | Fragmento do Éter (material) | Nebula Fragment | 20×20 |
| `LaminaDoErrante.png` | Lâmina do Errante | Night's Edge | 52×52, ponta para cima-direita |
| `ArcoDoArcanjo.png` | Arco do Arcanjo | Daedalus Stormbow | 24×54, em pé |
| `TomoDoJulgamento.png` | Tomo do Julgamento | Spell Tome | 28×30 |
| `CajadoDoDragao.png` | Cajado do Dragão do Éter | Stardust Dragon Staff | 40×40 |
| `AdagaDoErrante.png` | Adaga do Errante (Rogue, só com Calamity) | Magic Dagger | 24×24, ponta para cima-direita |
| `EcoDoEter.png` | Eco do Éter (Expert) | Celestial Stone | 28×28 |
| `BolsaDoAstharoth.png` | Bolsa do Tesouro do Astharoth | bolsa do Golem | 32×32 |
| `ElmoDoErrante.png` | Elmo do Errante (máscara) | Squire Great Helm | 24×24 |
| `TrofeuDoAstharoth.png` | Troféu do Astharoth (ícone) | troféu do Golem | 32×32 |
| `RelicariaDoAstharoth.png` | Relíquia do Astharoth (ícone) | relíquia do Golem | 30×40 |
| `OvoDoEter.png` | Ovo do Éter (item do pet) | Shadow Orb | 28×28 |

## Ícones de buff (32×32)

| Arquivo | Buff | Hoje usa |
|---|---|---|
| `EscudoDeGelo.png` | Escudo de Gelo (Coração de Armotheus) | Ice Barrier |
| `PassoNoEter.png` | Passo no Éter (Eco do Éter) | Shadow Dodge |
| `BuffCavaleiroDoCeu.png` | lacaio Cavaleiro do Céu | Flinx |
| `BuffDragaoLacaio.png` | lacaio Dragãozinho do Éter | Stardust Dragon |
| `BuffMiniArmotheus.png` | pet Mini Armotheus | Wisp |
| `BuffMiniDragao.png` | pet Mini Dragão do Éter | Shadow Orb |

## Coisas desenhadas no mundo

| Arquivo | O que é | Hoje usa | Tamanho |
|---|---|---|---|
| `Cavaleiro.png` | O cavaleiro de gelo (última forma do Armotheus) | a Forma Verdadeira tingida de azul | 100×120, um quadro, olhando para a esquerda |
| `CavaleiroDoCeu.png` | Os cavaleiros do fundo e o lacaio do cajado | a Forma Verdadeira encolhida | ~60×72, um quadro |
| `MiniArmotheus.png` | O pet | a animação do chapéu encolhida | ~42×42, um quadro |
| `ChapeuDeArmotheus_Head.png` | A máscara na cabeça do jogador | a aparência do Wizard Hat | 40×1120 (20 quadros de 40×56, como as máscaras do Terraria) |
| `ElmoDoErrante_Head.png` | Idem, do Astharoth | a aparência do Squire Great Helm | 40×1120 |
| `QuadroDaSilvanna.png` | O troféu na parede (inteiro, com moldura) | moldura desenhada em código + ícone do chefe | 48×48 |
| `QuadroDoAstharoth.png` | Idem, do Astharoth | idem | 48×48 |
| `ReliquiaDaSilvanna.png` | A peça dourada que flutua sobre o pedestal da relíquia | a Silvanna na vassoura, dourada | até 46×46 |
| `ReliquiaDoAstharoth.png` | Idem, do Astharoth | o sprite do Astharoth, dourado | até 46×46 |

## O que NÃO é trocado por aqui

- **Os sprites que já são seus** ficam onde estão: `Assets/Sheets/` (chapéu, Forma Verdadeira, Silvanna, Bruxa),
  `Content/Bosses/*/` (Astharoth, ícones de mapa, Estátua Viva), `Content/Items/` (Astrânia, os dois Chamados),
  `Content/NPCs/EcoEstelar.png`, `Content/Buffs/`. Para trocar, substitua o arquivo com o mesmo nome e tamanho.
- **O Dragão do Éter** (o do chefe, o lacaio e o pet) usa os pedaços do dragão do Lunatic Cultist, tingidos.
  Trocar exige uma folha por segmento: fica para quando você tiver a arte (está em `DragaoDoEter.cs` e `DragaoMini`).
- **Os ataques** (cortes, gelo, luz, orbes, pingentes, lanças, bola de neve) são desenhados por shaders e código,
  de propósito: não têm sprite.
- **O pedestal da relíquia** é desenhado em código (`RelicariaDosChefes.SpecialDraw`).
- **Músicas**: os chefes usam músicas do próprio Terraria (`Music = MusicID...` em cada chefe).
