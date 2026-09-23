# Manual do DetalhaBIM (Revit 2027)

Este manual explica cada ferramenta: para que serve, como usar, o que cada opção faz e dicas de uso.

## Sumário

- [Conceitos gerais](#conceitos-gerais)
- [Cotas](#cotas)
  - [Cotas por Ambiente](#cotas-por-ambiente) · [Cotas por Parede](#cotas-por-parede) · [Cotas Externas](#cotas-externas) · [Por Seleção](#por-seleção) · [Por Pontos](#por-pontos) · [Níveis de Piso](#níveis-de-piso)
- [Vistas](#vistas)
  - [Vistas por Ambiente](#vistas-por-ambiente) · [Isométrico e Elevações](#isométrico-e-elevações-atalhos) · [Plantas Técnicas](#plantas-técnicas) · [Vistas de Esquadrias](#vistas-de-esquadrias)
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

> As ferramentas de cotas funcionam em **plantas** (de piso ou de forro). Os botões ficam desabilitados em outras vistas.

### Cotas por Ambiente

Cria as cotas internas de cada ambiente.

- **Cota horizontal e cota vertical:** uma cadeia face a face em cada direção principal do ambiente. A
  cadeia inclui todas as paredes perpendiculares, além de pilares e recortes. Em ambientes em "L", ela
  mostra todos os trechos.
- **Cotar vãos de portas e janelas:** para cada parede que tem esquadrias, cria uma cadeia com os cantos
  do ambiente e as ombreiras dos vãos, paralela à parede e dentro do ambiente.
- **Posição da linha:** no centro do ambiente ou junto à parede (lado inferior ou esquerdo), com o afastamento definido.

A direção principal é a do maior trecho reto de parede do ambiente, então ambientes rotacionados também funcionam.

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

### Cotas Externas

Cria de uma vez as cotas externas de todo o pavimento.

- O plugin identifica as **paredes de fachada** de cada lado (inferior, superior, esquerdo e direito). Para
  isso, ele varre a edificação "de fora para dentro".
- Em cada lado, cria as linhas de **vãos**, **paredes** e **total**, afastadas da face mais externa daquele lado.
- Os lados seguem a direção predominante das paredes, o que funciona também em edificações rotacionadas.

> Dica: em fachadas muito recortadas, complemente com a ferramenta *Cotas por Parede*.

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
| A aba DetalhaBIM não aparece | Siga a seção "Se a aba ainda não aparecer" do `COMO-INSTALAR.txt`. Em resumo: confira se `DetalhaBIM.addin` está em `%AppData%\Autodesk\Revit\Addins\2027` e a DLL em `...\2027\DetalhaBIM\`; veja se existe `%AppData%\DetalhaBIM\logs`; e procure "DetalhaBIM" no journal mais recente do Revit (`%LocalAppData%\Autodesk\Revit\Autodesk Revit 2027\Journals`). |
| "Controle de Aplicativo Inteligente" bloqueou o arquivo | Esse recurso do Windows 11 bloqueia DLLs sem assinatura digital e não aceita exceções. Desative-o (Iniciar → digite *Controle de Aplicativo Inteligente* → Desativado) ou use uma DLL assinada. |
| O plugin aparece duas vezes ou dá erro de "AddInId duplicado" | Sobrou uma instalação anterior. Apague `%AppData%\Autodesk\ApplicationPlugins\DetalhaBIM.bundle`, se existir. |
| Botões de cotas desabilitados | Eles funcionam somente em plantas. Abra uma planta de piso ou de forro. |
| "Nenhum ambiente delimitado foi encontrado" | Os ambientes precisam estar colocados e fechados. Rode **Limpar Ambientes** para diagnosticar. |
| Algumas cotas não foram criadas | Veja o relatório expandido. Paredes curvas e de vínculo não são cotadas automaticamente. |
| Esquadrias sem cota nas elevações | A família não tem os planos de referência Esquerda, Direita, Superior e Inferior definidos como referência. |
| Modelo de vista não aplicado | O nome configurado não existe no projeto. Ajuste em Configurações. |
| Erro inesperado | Consulte o log em **Ajuda → Pasta de logs** (`%AppData%\DetalhaBIM\logs`). |
