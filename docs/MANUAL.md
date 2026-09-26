# Manual do DetalhaBIM (Revit 2027)

Este manual explica cada ferramenta: para que serve, como usar, o que cada opção faz e dicas de uso.

## Sumário

- [Conceitos gerais](#conceitos-gerais)
- [Cotas](#cotas)
  - [Cotas por Ambiente](#cotas-por-ambiente) · [Cotas por Parede](#cotas-por-parede) · [Cotas Externas](#cotas-externas) · [Cota Alinhada](#cota-alinhada) · [Cotar Componentes](#cotar-componentes) · [Por Seleção](#por-seleção) · [Por Pontos](#por-pontos) · [Níveis de Piso](#níveis-de-piso)
- [Vistas](#vistas)
  - [Vistas por Ambiente](#vistas-por-ambiente) · [Isométrico e Elevações](#isométrico-e-elevações-atalhos) · [Plantas Técnicas](#plantas-técnicas) · [Vistas de Esquadrias](#vistas-de-esquadrias)
- [Interiores](#interiores)
  - [Rodapés](#rodapés) · [Paginação de Piso](#paginação-de-piso) · [Acabamentos](#acabamentos) · [Detalhar Marcenaria](#detalhar-marcenaria) · [Locação](#locação) · [Levantamento](#levantamento)
- [Documentação](#documentação)
  - [Etiquetar Vistas](#etiquetar-vistas) · [Quadros e Tabelas](#quadros-e-tabelas) · [Criar Pranchas](#criar-pranchas) · [Renumerar Elementos](#renumerar-elementos)
- [Organização](#organização)
  - [Editor de Níveis](#editor-de-níveis) · [Eixos Automáticos](#eixos-automáticos) · [Limpar Ambientes](#limpar-ambientes)
- [Modelagem](#modelagem)
  - [Forros por Ambiente](#forros-por-ambiente) · [Pisos por Ambiente](#pisos-por-ambiente) · [Distribuir Luminárias](#distribuir-luminárias)
- [Configurações](#configurações)
- [Solução de problemas](#solução-de-problemas)

---

## Conceitos gerais

**Diálogos que lembram as escolhas.** Cada ferramenta abre um diálogo de opções e guarda os valores da
última execução. Para voltar ao padrão, use **Restaurar padrões**, no canto inferior esquerdo do diálogo.

**Distâncias em "mm na folha".** Os afastamentos das cotas são medidos na folha impressa e se ajustam
à escala da vista. Por exemplo, 8 mm equivalem a 40 cm no modelo em 1:50 e a 20 cm em 1:25. Assim a
mesma configuração funciona em qualquer escala.

**Escopo dos ambientes.** As ferramentas que trabalham com ambientes oferecem quatro escopos:

- *Selecionar ambientes na vista*: usa a seleção atual. Se não houver seleção, pede que você selecione.
  Clicar na etiqueta do ambiente também vale.
- *Todos os ambientes visíveis na vista ativa*.
- *Todos os ambientes do nível da vista ativa*.
- *Todos os ambientes do projeto*.

**Um único Desfazer.** Cada execução pode ser desfeita inteira com um único **Ctrl+Z**.

**Relatório final.** Ao terminar, cada ferramenta mostra quantos itens criou. Expanda o relatório para ver
os avisos, por exemplo um ambiente pequeno demais para sanca ou uma família sem planos de referência.

**Qualquer template.** Os nomes de tipos de cota, modelos de vista e carimbos são procurados no projeto
aberto. Se um nome não existir, o plugin usa o padrão do Revit e avisa no relatório.

---

## Cotas

> As ferramentas de cotas funcionam em **plantas** (de piso ou de forro); os botões ficam desabilitados em
> outras vistas. A exceção é **Cotar Componentes**, que também funciona em cortes, elevações e vistas 3D isométricas.

### Cotas por Ambiente

Cria as cotas internas de cada ambiente.

- **Cota horizontal e cota vertical:** uma cadeia face a face em cada direção principal do ambiente. A
  cadeia inclui todas as paredes perpendiculares, além de pilares e recortes. Em ambientes em "L", ela
  mostra todos os trechos.
- **Cotar vãos de portas e janelas:** para cada parede que tem esquadrias, cria uma cadeia com os cantos
  do ambiente e as ombreiras dos vãos, paralela à parede e dentro do ambiente.
- **Posição da linha:** no centro do ambiente ou junto à parede (lado inferior ou esquerdo), com o afastamento definido.

A direção principal é a do maior trecho reto de parede do ambiente, então ambientes rotacionados também funcionam.

- **Paredes curvas:** as cadeias chegam às **pontas** e ao **ponto mais extremo** de cada trecho curvo do
  contorno (linhas de referência invisíveis nesses pontos), então a cota do ambiente fecha até a curva.
- **Paredes inclinadas:** paredes fora das duas direções principais (por exemplo, a 30° ou 45°) recebem uma
  cota **alinhada com a própria parede**, medindo a face de acabamento de canto a canto.

### Cotas por Parede

É a forma mais rápida de cotar fachadas e paredes longas.

1. Escolha quais linhas criar: **vãos**, **paredes internas** e **total**. Ajuste também os afastamentos.
2. Clique em **Começar**.
3. Clique na parede.
4. Clique do lado em que as cotas devem ficar (normalmente o lado externo).
5. Repita com outras paredes. Pressione **ESC** para terminar.

O plugin encontra sozinho:

- as **paredes alinhadas** com a parede clicada (mesma face externa e contíguas);
- os **cantos** e as **ombreiras** de todas as portas e janelas (1ª linha);
- as **paredes internas** que chegam na fachada, com a espessura de cada uma (2ª linha);
- a **cota total** (3ª linha).

Linhas repetidas não são criadas. Por exemplo, em uma fachada sem janelas, a 1ª linha seria igual à total e é omitida.

**Paredes curvas:** quando uma parede curva está ligada à ponta da fachada, a linha de paredes e a cota total
vão até as **pontas e o ponto mais extremo do arco**. Se você clicar diretamente em uma **parede curva**, o
DetalhaBIM cota a face do lado clicado com **raio**, **comprimento do arco** e **corda** (distância reta entre
as pontas).

### Cotas Externas

Cria de uma vez as cotas externas de todo o pavimento.

- O plugin identifica as **paredes de fachada** de cada lado (inferior, superior, esquerdo e direito). Para
  isso, ele varre a edificação "de fora para dentro".
- Em cada lado, cria as linhas de **vãos**, **paredes** e **total**, afastadas da face mais externa daquele lado.
- Os lados seguem a direção predominante das paredes, o que funciona também em edificações rotacionadas.
- Paredes curvas ligadas às fachadas entram na cota de paredes e na total, pelas pontas e pelo ponto extremo.

> Dica: em fachadas muito recortadas, complemente com a ferramenta *Cotas por Parede*.

### Cota Alinhada

Cota **paralela à parede, em qualquer inclinação**: paredes a 30°, 45° ou em qualquer ângulo, chanfros e
plantas rotacionadas.

**Modo parede:** clique na parede e depois do lado da face que você quer cotar (é também o lado onde as
cotas ficam). São criadas até três linhas paralelas à face:

1. **Vãos:** cantos da face + ombreiras das portas e janelas;
2. **Paredes que chegam:** as paredes que encostam na face;
3. **Total:** o comprimento da face.

Quando a parede termina **chanfrada** (encontro com outra parede inclinada), não existe face perpendicular
para o Revit cotar. Nesse caso, o DetalhaBIM cria uma **linha de detalhe invisível** exatamente no canto da
face e cota até ela, e a medida sai correta. Opcionalmente, cota também a **espessura** no ponto clicado.
Clicando em uma **parede curva**, a cota é de **raio**, **comprimento do arco** e **corda** da face clicada.

**Modo livre:** clique em uma parede, eixo, plano de referência ou linha (que define a direção), depois em
**dois pontos quaisquer** (com snaps de extremidade, interseção, ponto médio...) e onde a linha de cota deve
passar. A cota mede a distância entre os dois pontos **na direção escolhida**.

Os dois modos são contínuos. Pressione **ESC** para terminar.

### Cotar Componentes

Cota **componentes** — as famílias inseridas pelo comando *Componente* do Revit: mobiliário, marcenaria, blocos,
louças, equipamentos, luminárias... — e também **paredes**. Funciona em:

| Vista | O que é cotado |
|---|---|
| **3D isométrica** | Largura e profundidade sobre o topo do objeto e altura na quina, do lado voltado para você |
| Planta | Largura e profundidade |
| Corte / elevação | Largura e altura |

- **Vistas 3D:** o Revit só aceita cotas em vistas 3D **travadas**. O DetalhaBIM salva a orientação e trava a
  vista sozinho. Se for a vista `{3D}` padrão, que não pode ser travada sem nome, ele cria a cópia
  "DetalhaBIM - 3D cotado", travada, e a abre. Vistas em perspectiva não são suportadas.
- **Como mede:** tenta, nesta ordem, os planos de referência *Esquerda/Direita*, *Frente/Fundo* e
  *Inferior/Superior* da família; as faces; e as arestas/linhas simbólicas (em planta, muitos blocos só têm
  linhas 2D). Todas as referências vêm da geometria do **símbolo** da família — a única que o Revit aceita em
  cotas. **Cada cota criada é conferida**: se não medir o tamanho real do objeto, é apagada e a próxima
  alternativa é tentada.
- **Famílias sem nada cotável** (blocos importados, malhas): a cota é feita por **linhas auxiliares
  invisíveis** nas extremidades do objeto. A medida sai correta, mas não acompanha o objeto se ele for
  movido; o relatório informa quantas cotas ficaram assim.
- **Paredes:** comprimento (ponta a ponta), espessura e altura. A opção *vãos* acrescenta a cadeia com as
  ombreiras das portas e janelas.
- **Posição:** distância em milímetros na folha; em plantas e elevações, escolha abaixo/à esquerda ou acima/à direita.

### Por Seleção

Cria uma única cadeia com os elementos selecionados.

- Os elementos aceitos são paredes (ambas as faces), eixos, pilares (faces), portas e janelas, e planos de referência.
- **Direção:** automática (perpendicular ao primeiro elemento), horizontal ou vertical.
- **Portas e janelas:** cota o eixo da esquadria ou o vão (esquerda e direita).
- Depois de selecionar, clique onde a linha de cota deve passar.

### Por Pontos

Clique em um **ponto inicial** e em um **ponto final**. Tudo o que a linha atravessar (paredes, pilares e
eixos) é cotado em uma única cadeia, com as espessuras das paredes e os vãos livres entre elas.

- **Forçar direção ortogonal:** alinha a linha às paredes do projeto.
- **3º clique:** permite posicionar a linha de cota fora do trajeto clicado.
- O modo é contínuo. Pressione **ESC** para terminar.

### Níveis de Piso

Insere a **cota de nível** do piso acabado em cada ambiente, com a leitura do piso modelado sob o ambiente
(por exemplo, +0,00 ou −0,02).

- A cota fica logo abaixo do centro do ambiente, afastada da etiqueta. O afastamento é configurável.
- Por padrão, ambientes que já têm cota de nível são ignorados.
- É preciso ter um piso (Floor) modelado sob o ambiente.

---

## Vistas

### Vistas por Ambiente

Gera o **kit completo de detalhamento** de cada ambiente:

| Vista | Resultado |
|---|---|
| Planta baixa | Recortada no ambiente (com folga), na escala e no modelo de vista escolhidos. |
| Planta de forro | Mesmo recorte, em planta de forro. |
| Elevações internas | **Uma elevação para cada parede**, identificadas pelas letras A, B, C… e recortadas do piso ao teto e de canto a canto. |
| Isométrico 3D | Caixa de corte ajustada ao ambiente e orientação SE, SO, NE ou NO. |

**Nomes:** seguem o padrão das Configurações, por exemplo `101 - COZINHA - PLANTA` e `101 - COZINHA - ELEVAÇÃO A`.

**Automações opcionais:**

- **Cotar a planta do ambiente:** cotas face a face mais os vãos.
- **Etiquetar ambiente, portas e janelas** na planta.
- **Cotar a largura de cada parede** nas elevações.
- **Montar uma prancha por ambiente:** coloca todas as vistas do ambiente em uma prancha, organizadas automaticamente.

### Isométrico e Elevações (atalhos)

São atalhos de um clique: selecione os ambientes e clique no botão. As opções usadas (modelos e escalas)
vêm das **Configurações**.

### Plantas Técnicas

Cria o conjunto de plantas técnicas de **vários níveis de uma vez**.

1. Marque os **níveis**.
2. Marque as **plantas**, que vêm da lista das Configurações: planta baixa, layout, demolir/construir, pisos,
   pontos elétricos, pontos hidráulicos, forro e luminotécnica.
3. Clique em **Criar plantas**.

Cada planta recebe o nome `<PLANTA> - <NÍVEL>`, a escala, o modelo de vista e as automações configuradas:
etiquetar ambientes, etiquetar esquadrias, cotas externas e cotas internas. Plantas que já existem com o
mesmo nome podem ser mantidas (opção padrão). Também é possível criar uma prancha para cada planta.

> Personalize a lista em **Configurações → Plantas técnicas**. Lá você adiciona, remove, reordena e escolhe o modelo de cada planta.

### Vistas de Esquadrias

Cria uma **elevação para cada código de esquadria** (P01, P02, J01…), com um representante por Marca de
tipo ou por tipo de família.

- A vista é **recortada** na esquadria e **cotada** com largura (acima), altura (à direita) e peitoril (à esquerda, medido a partir do nível).
- **Vista pelo lado:** externo, que é a face da família, ou interno.
- **Ocultar marcadores nas plantas:** evita poluir as plantas com os símbolos de elevação.
- **Montar prancha:** distribui todas as vistas em pranchas "DETALHAMENTO DE ESQUADRIAS".

> As cotas usam os planos de referência *Esquerda, Direita, Superior e Inferior* das famílias, que existem
> nas famílias padrão do Revit. Em famílias sem esses planos, o relatório indica quais não puderam ser cotadas.

## Interiores

Ferramentas para projetos de **interiores** e para o detalhamento de acabamentos: rodapé, paginação, pintura,
marcenaria, locação e quantitativos.

### Rodapés

Cria o **rodapé** de cada ambiente pelo contorno de acabamento das paredes.

**Método "Parede de rodapé"** (recomendado):

- Cria paredes finas com a **altura**, a **espessura** e o **material** escolhidos. O tipo
  "DetalhaBIM - Rodapé 1,5 cm - material" é criado sozinho. Se preferir, escolha um tipo de parede do projeto.
- Resolve os **cantos** (internos e externos), **contorna pilares** e é **interrompido nas portas**. Opcionalmente,
  também em portas-janelas e janelas com peitoril abaixo do topo do rodapé. O vão é medido nas ombreiras da
  parede.
- Fica **apoiado sobre o piso modelado** do ambiente (por exemplo, o piso de **Pisos por Ambiente**), com
  deslocamento adicional opcional.
- As paredes de rodapé **não delimitam ambientes** (as áreas não mudam) e levam no *Comentários* o texto
  "RODAPÉ - número - nome". Por isso, a opção *Ignorar ambientes que já têm rodapé* evita duplicar.
- No fim, o relatório mostra o **comprimento total de rodapé em metros**.

**Método "Perfil de parede":** usa um tipo de *Perfil de parede* (Wall Sweep) do projeto, na face da parede
voltada para o ambiente, cortado pelas portas. Como no Revit, o perfil percorre a parede inteira daquele lado.

### Paginação de Piso

**Modela o piso** de revestimento de cada ambiente, placa por placa. Não é hachura: as placas são elementos
reais, com a junta como vão entre elas, e aparecem na planta, no 3D, nos cortes e nas tabelas.

- **Placa:** descrição, largura e comprimento (por exemplo, 60×60, 90×90, 20×120 para régua), **junta**
  (rejunte, em mm), **espessura** e **material** (digite um nome para criar). O tipo de piso
  "DetalhaBIM - Porcelanato 60x60 - 1 cm - material" é criado sozinho, ou escolha um tipo do projeto.
- **Ponto de partida:**
  - *Junta centralizada* — uma junta passa pelo centro do ambiente (distribui os recortes nas bordas);
  - *Placa centralizada* — uma placa fica no centro;
  - *Placa inteira no canto* — começa inteira no canto do ambiente;
  - *Clicar um ponto* — um ponto em comum para todos os ambientes, com as juntas alinhadas entre eles.
- **Ângulo:** em relação à maior parede do ambiente (0°, 45° para diagonal ou qualquer valor).
- **Modelagem:**
  - *Um piso por ambiente dividido nas juntas* (recomendado): o piso do ambiente é dividido em **peças**
    (Parts) do Revit, uma por placa, com o vão da junta. É leve e fácil de refazer. As placas aparecem nas
    vistas com *Visibilidade de peças = Mostrar peças*; o DetalhaBIM ajusta isso na planta ativa, nas plantas
    do mesmo nível e nas vistas 3D.
  - *Um piso separado para cada placa*: cada placa é um piso (bom para trocar o material de placas
    específicas). Limite de 2.500 placas por ambiente.
- **Base:** apoia a paginação sobre o piso existente do ambiente (laje ou contrapiso); o **Rodapé** criado
  depois se apoia sobre ela.
- Refazer a paginação de um ambiente **substitui** a anterior (e remove as linhas da versão 1.3).
- **Quantitativo:** placas **inteiras** e **cortadas** (cada placa cortada conta como uma comprada), área,
  área com **perda** e número de **caixas** (se você informar os m² por caixa). A tabela final pode ser
  **copiada para o Excel** ou **exportada em CSV**.

### Acabamentos

Escolha o **material** e **clique onde ele vai**. O modo é contínuo; **ESC** encerra.

**O que aplicar:**

- **Pintura:** aplica o material na face (ferramenta *Pintura* do Revit), sem alterar a composição da parede —
  tinta, textura, papel de parede. Vale para paredes, pisos e forros. *Remover pintura* desfaz.
- **Revestimento modelado:** cria a **camada** sobre a face da parede (cerâmica, porcelanato, painel), com a
  espessura e o material escolhidos. Vai do **piso acabado** (com deslocamento opcional, por exemplo, para
  começar acima do rodapé) até a **altura definida** (ex.: 1,20 m) ou **até o forro**. **Portas e janelas são
  recortadas**. A camada não altera a área dos ambientes e leva "REVESTIMENTO" no *Comentários*.

**Onde aplicar:**

- **Clicar nas faces, uma a uma:** melhor em vistas 3D, cortes e elevações.
- **Clicar dentro dos ambientes** (em planta): todas as paredes do ambiente de uma vez. Na pintura, opcionalmente
  também o piso e o teto. Preenche o quadro de acabamentos do ambiente (parede/piso/teto).

Materiais novos recebem a **cor** escolhida. A pintura aparece nos estilos visuais *Sombreado*, *Cores
consistentes* e *Realista*.

> Pintura: se a mesma face de parede atravessa vários ambientes, ela é pintada inteira. Use *Dividir face* do
> Revit antes, ou use o revestimento modelado, que segue o contorno de cada ambiente.

### Detalhar Marcenaria

Selecione **marcenaria, bancadas ou mobiliário**. Para cada peça (ou para cada **tipo**, sem repetir peças
iguais), são criadas:

- **Planta** recortada na peça;
- **Vista frontal** e **vista lateral** (lado direito), recortadas e com profundidade limitada à peça;
- **Isométrico 3D** com caixa de corte ajustada à peça e vista travada;
- **Cotas** de largura, profundidade e altura em todas as vistas;
- **Prancha** "MARCENARIA - código" com as vistas distribuídas.

As cotas usam a mesma técnica de **Cotar Componentes** (planos de referência, faces, linhas simbólicas e, se
preciso, linhas auxiliares), então todas as vistas saem cotadas. O recorte das vistas já deixa espaço para as cotas.

O nome vem da *Marca de tipo* (por exemplo, M01), da *Marca* ou de *Família - Tipo*. A vista frontal olha para a
face *Frente* da família, que é a convenção dos modelos de família do Revit. Se a sua família foi modelada ao
contrário, escolha *O lado oposto*.

### Locação

Selecione **móveis, marcenaria, louças, tomadas, interruptores, luminárias** etc.

- **Planta:** cadeia de cotas do objeto até as **paredes mais próximas**, ao longo da largura e da
  profundidade (até a parede mais próxima ou até as duas).
- **Elevação/corte:** locação horizontal até as paredes laterais e **altura em relação ao piso acabado**
  (h = 30 cm, 110 cm...), medida na face superior do piso ou, se não houver piso, no nível.
- **Referência do objeto:** pelas **faces** (mobiliário e marcenaria) ou pelo **eixo/centro** (pontos elétricos
  e hidráulicos, luminárias). Opcionalmente, inclui a dimensão do próprio objeto na cadeia.

### Levantamento

Quantitativo de acabamentos por ambiente, sem alterar nada no modelo:

| Coluna | Como é calculada |
|---|---|
| Piso (m²) | Área do ambiente |
| Perímetro (m) | Contorno de acabamento |
| Pé-direito (m) | Até o forro sobre o ambiente, quando houver (marcado "forro"); senão, a altura do ambiente |
| Paredes bruta / líquida (m²) | Contorno com paredes × pé-direito, menos portas e janelas (só a parte abaixo do forro) |
| Rodapé (m) | Contorno com paredes menos a largura das portas |
| Teto (m²) | Área do ambiente |
| + perda | Piso e paredes com o percentual de perda informado |

A última linha traz os **totais**. **Copiar para o Excel** cola direto na planilha. **Exportar CSV** gera um
arquivo que abre no Excel com acentos e vírgula decimal.

---

## Documentação

### Etiquetar Vistas

Etiqueta automaticamente as vistas marcadas na lista, que pode incluir várias plantas, cortes e elevações.

- **Categorias:** ambientes, portas, janelas, mobiliário, peças sanitárias e luminárias. Para cada uma, escolha
  o tipo de etiqueta ou deixe o padrão do projeto.
- **Ignorar já etiquetados:** evita etiquetas duplicadas.
- Nas plantas, as etiquetas de **portas** ficam do lado interno e as de **janelas** do lado externo, afastadas da parede.

### Quadros e Tabelas

Cria tabelas prontas para prancha:

| Quadro | Colunas | Organização |
|---|---|---|
| Portas | CÓDIGO, LARGURA (m), ALTURA (m), DESCRIÇÃO, QTD. | Agrupado por código, com total |
| Janelas | CÓDIGO, LARGURA (m), ALTURA (m), PEITORIL (m), DESCRIÇÃO, QTD. | Agrupado por código e peitoril |
| Áreas | PAVIMENTO, Nº, AMBIENTE, ÁREA (m²), PERÍMETRO (m) | Por pavimento, com subtotal e total |
| Acabamentos | Nº, AMBIENTE, PISO, RODAPÉ, PAREDES, FORRO | Por número |
| Mobiliário, Marcenaria, Louças e metais, Luminárias | CÓDIGO, ITEM, DESCRIÇÃO, FABRICANTE, MODELO, QTD. | Agrupado por código e item, com total |

Se já existir um quadro com o mesmo nome, você escolhe entre criar outro com nome numerado ou substituir o anterior.

### Criar Pranchas

1. Marque as vistas. A lista mostra apenas as vistas que ainda não estão em pranchas. Vistas selecionadas no
   Navegador de Projeto antes de abrir a ferramenta já vêm marcadas.
2. Escolha o modo:
   - **Uma vista por prancha:** o nome da prancha é o nome da vista.
   - **Agrupar:** as vistas são distribuídas em linhas, da esquerda para a direita e de cima para baixo,
     dentro da área útil do carimbo. Quando o espaço acaba, novas pranchas são criadas.
3. Defina o carimbo, o prefixo e os dígitos da numeração (A-01, A-02…) e as faixas reservadas ao selo.

### Renumerar Elementos

Numera portas, janelas, ambientes, pilares, vagas, mobiliário e outras categorias.

- **Parâmetro:**
  - *Marca*: cada elemento recebe um número.
  - *Marca de tipo*: cada tipo recebe um código, por exemplo P01, P02, J01.
  - Em ambientes, o parâmetro é sempre o *Número*.
- **Ordem automática:** segue a regra de leitura da prancha, de cima para baixo e da esquerda para a direita.
  Elementos com diferença vertical menor que a *tolerância de linha* ficam na mesma linha. Com *Todo o
  projeto*, a numeração segue nível por nível.
- **Ordem manual:** clique nos elementos na ordem desejada. O próximo código aparece na barra de status.
  Pressione **ESC** para terminar.
- **Ordem dos tipos** (Marca de tipo): pela ordem de leitura da planta ou por tamanho (largura × altura).
- **Formato:** prefixo, sufixo, número inicial, dígitos (01, 001…) e incremento. O prefixo sugerido muda conforme a categoria.

> Dica para ambientes por pavimento: rode uma vez por nível com o escopo *Visíveis na vista ativa*, prefixo
> "1" e 2 dígitos. O resultado é 101, 102… No pavimento seguinte, use o prefixo "2".

---

## Organização

### Editor de Níveis

Mostra todos os níveis do projeto em uma tabela editável:

- **Nome** e **Elevação (m):** edite direto na célula.
- **Plantas:** quantas plantas cada nível possui.
- **Criar planta / Criar forro:** cria as plantas ao aplicar.
- **Excluir:** remove o nível. Antes disso, o plugin pede confirmação e informa quantas plantas serão excluídas junto.
- **+ Adicionar nível:** cria uma nova linha 3 m acima do nível mais alto.
- **Renomear em lote:** aplica prefixo, sufixo, localizar/substituir e MAIÚSCULAS às linhas selecionadas (ou a todas).
- **Renomear também as plantas:** as plantas cujo nome contém o nome antigo do nível acompanham a mudança.

Nada é alterado até você clicar em **Aplicar**. Trocar nomes entre níveis também funciona.

### Eixos Automáticos

1. Escolha a origem: todas as paredes da vista ou só as selecionadas. Defina o comprimento mínimo do
   alinhamento, a espessura mínima (para ignorar divisórias) e a tolerância.
2. O plugin cria **um eixo para cada alinhamento de paredes**, prolongado além da edificação.
3. **Nomes:** os eixos verticais recebem 1, 2, 3 (da esquerda para a direita) e os horizontais A, B, C (de
   cima para baixo). É possível inverter.
4. **Reaproveitar eixos existentes** evita duplicar eixos coincidentes.
5. **Plantas de eixos:** marque os níveis para criar `PLANTA DE EIXOS - <nível>` com cadeias parciais e totais
   nos dois sentidos. A vista ativa também pode ser cotada.

Opção **Face externa**: os eixos extremos são posicionados na face externa das paredes de fachada.

### Limpar Ambientes

Lista os ambientes com problema:

- **Não colocado:** existe no projeto, mas não está em nenhuma planta.
- **Não delimitado:** não está totalmente fechado por paredes ou linhas separadoras.
- **Redundante:** há mais de um ambiente na mesma região.

Os ambientes com problema já vêm marcados. Clique em **Excluir marcados** para removê-los definitivamente.
As etiquetas desses ambientes também são removidas. Para ver os demais, marque *Mostrar também ambientes sem problemas*.

---

## Modelagem

### Forros por Ambiente

- Cria o forro de cada ambiente pelo contorno das paredes, na **altura** informada, que corresponde ao pé-direito.
- **Sanca perimetral:** cria um segundo forro na faixa junto às paredes (largura configurável), mais baixo que
  o forro central (rebaixo configurável) e com tipo próprio.
- Por padrão, ambientes que já têm forro são ignorados.

### Pisos por Ambiente

- Cria o piso de acabamento de cada ambiente pelo contorno das paredes.
- O **desnível** é contado a partir do nível. Por exemplo, use −2 cm em banheiros e áreas de serviço.
- É possível ignorar ambientes que já têm um piso **do mesmo tipo**, o que evita duplicar o acabamento sem
  confundir com a laje estrutural.
- O nome do ambiente pode ser gravado nos *Comentários* do piso.

### Distribuir Luminárias

- Monta uma malha uniforme: *n = arredondar(dimensão ÷ espaçamento)*, com **meio espaçamento junto às
  paredes**. Isso segue o critério luminotécnico usual.
- O **afastamento mínimo** das paredes reduz a quantidade de luminárias em ambientes estreitos.
- A malha acompanha a direção das paredes do ambiente.
- **Hospedagem:** se existir um forro, famílias baseadas em face ou hospedadas em forro são colocadas no
  forro. Sem forro, a luminária fica na *altura de instalação* informada.
- Em ambientes em "L", pontos fora do ambiente são descartados automaticamente.

---

## Configurações

Os padrões do escritório ficam organizados em abas:

| Aba | O que define |
|---|---|
| Cotas | Tipo de cota linear, tipo de cota de nível, afastamentos (1ª linha, espaçamento, cotas internas) e menor segmento |
| Vistas | Padrão de nome (`{NUMERO}`, `{NOME}`, `{NIVEL}`), sufixos, modelos de vista, escalas, folgas de recorte, distância do marcador e orientação do isométrico |
| Plantas técnicas | A lista de plantas, com nome, tipo (piso ou forro), modelo, escala e automações |
| Renumeração | Prefixos por categoria, dígitos e tolerância da linha de leitura |
| Modelagem | Tipos e medidas padrão de forro, sanca, piso e luminárias |
| Pranchas | Carimbo, numeração, faixas do selo, margem e espaço entre vistas |

- **Exportar / Importar:** gera um arquivo `.json` para padronizar todos os computadores do escritório.
- As configurações ficam em `%AppData%\DetalhaBIM\configuracoes.json`.

---

## Solução de problemas

| Situação | Solução |
|---|---|
| A aba DetalhaBIM não aparece | Siga a seção "Se a aba ainda não aparecer" do `COMO-INSTALAR.txt`. Em resumo: confira se `DetalhaBIM.addin` e `DetalhaBIM.dll` estão lado a lado em `%AppData%\Autodesk\Revit\Addins\2027`; veja se existe `%AppData%\DetalhaBIM\logs`; e procure "DetalhaBIM" no journal mais recente do Revit (`%LocalAppData%\Autodesk\Revit\Autodesk Revit 2027\Journals`). |
| "Controle de Aplicativo Inteligente" bloqueou o arquivo | Esse recurso do Windows 11 bloqueia DLLs sem assinatura digital e não aceita exceções. Desative-o (Iniciar → digite *Controle de Aplicativo Inteligente* → Desativado) ou use uma DLL assinada. |
| "Failed to initialize the add-in... the assembly does not exist" (a DLL não existe) | A `DetalhaBIM.dll` não está na mesma pasta do `DetalhaBIM.addin`, ou foi removida pelo antivírus (veja *Segurança do Windows → Histórico de proteção*). Copie de novo os 2 arquivos, lado a lado. |
| O plugin aparece duas vezes ou dá erro de "AddInId duplicado" | Sobrou uma instalação anterior. Apague `%AppData%\Autodesk\ApplicationPlugins\DetalhaBIM.bundle` e a pasta `DetalhaBIM` dentro de `Addins\2027`, se existirem. |
| Botões de cotas desabilitados | Eles funcionam somente em plantas. Abra uma planta de piso ou de forro. **Cotar Componentes** também funciona em cortes, elevações e vistas 3D isométricas, e **Locação** também em cortes e elevações. |
| Paginação: o piso aparece sem as placas | A vista está com *Visibilidade de peças = Mostrar original*. Nas propriedades da vista, mude para *Mostrar peças*. |
| Pintura "não aparece" | A pintura só é vista nos estilos *Sombreado*, *Cores consistentes* e *Realista* (3D, cortes, elevações). Em planta não se vê a face da parede. |
| Cotas em 3D não aparecem | A vista 3D precisa estar travada e não pode ser perspectiva. O DetalhaBIM trava a vista sozinho. Se a vista foi destravada depois, as cotas podem sumir: trave de novo com *Restaurar orientação e travar vista*. |
| Objeto sem cota ("não há faces ou planos de referência") | A família não tem planos de referência nem faces planas naquela direção (por exemplo, móveis curvos). Cote manualmente ou ajuste a família. |
| Rodapé não foi cortado em uma porta | A porta precisa estar hospedada na parede que delimita o ambiente. Portas de modelos vinculados não são detectadas. |
| "Nenhum ambiente delimitado foi encontrado" | Os ambientes precisam estar colocados e fechados. Rode **Limpar Ambientes** para diagnosticar. |
| Algumas cotas não foram criadas | Veja o relatório expandido. Paredes curvas e de vínculo não são cotadas automaticamente. |
| Esquadrias sem cota nas elevações | A família não tem os planos de referência Esquerda, Direita, Superior e Inferior definidos como referência. |
| Modelo de vista não aplicado | O nome configurado não existe no projeto. Ajuste em Configurações. |
| Erro inesperado | Consulte o log em **Ajuda → Pasta de logs** (`%AppData%\DetalhaBIM\logs`). |
