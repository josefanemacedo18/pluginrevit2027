using System;
using System.Collections.Generic;
using System.Windows.Media;
using DetalhaBIM.Commands.Cotas;
using DetalhaBIM.Commands.Documentacao;
using DetalhaBIM.Commands.Geral;
using DetalhaBIM.Commands.Interiores;
using DetalhaBIM.Commands.Modelagem;
using DetalhaBIM.Commands.Organizacao;
using DetalhaBIM.Commands.Vistas;
using DetalhaBIM.UI;

namespace DetalhaBIM.Ribbon
{
    public class PanelDef
    {
        public string Name { get; set; }
        public Color Color { get; set; }
    }

    public class ToolDef
    {
        public string Panel { get; set; }
        public string Id { get; set; }
        /// <summary>Texto do botão ("\n" quebra a linha nos botões grandes).</summary>
        public string Text { get; set; }
        public Type Command { get; set; }
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public string Description { get; set; }
        public Type Availability { get; set; } = typeof(ProjectAvailability);
        /// <summary>Botões pequenos são empilhados (até 3 por coluna).</summary>
        public bool Small { get; set; }
    }

    /// <summary>
    /// Catálogo único das ferramentas: usado para montar a faixa de opções e a janela de ajuda.
    /// </summary>
    public static class ToolCatalog
    {
        public static readonly List<PanelDef> Panels = new List<PanelDef>
        {
            new PanelDef { Name = "Cotas", Color = Theme.Cotas },
            new PanelDef { Name = "Vistas", Color = Theme.Vistas },
            new PanelDef { Name = "Interiores", Color = Theme.Interiores },
            new PanelDef { Name = "Documentação", Color = Theme.Documentacao },
            new PanelDef { Name = "Organização", Color = Theme.Organizacao },
            new PanelDef { Name = "Modelagem", Color = Theme.Modelagem },
            new PanelDef { Name = "Geral", Color = Theme.Geral },
        };

        public static readonly List<ToolDef> Tools = new List<ToolDef>
        {
            // ------------------------------------------------------------ Cotas
            new ToolDef
            {
                Panel = "Cotas", Id = "CotasAmbiente", Text = "Cotas por\nAmbiente", Command = typeof(CotasAmbienteCommand), Icon = "cotas-ambiente",
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Cotas internas automáticas dos ambientes selecionados.",
                Description = "Cria, para cada ambiente, uma cadeia de cotas horizontal e outra vertical, face a face das paredes (incluindo pilares e recortes), e opcionalmente cadeias com os vãos de portas e janelas de cada parede. Funciona com ambientes selecionados, da vista ou do nível.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "CotasParede", Text = "Cotas por\nParede", Command = typeof(CotasParedeCommand), Icon = "cotas-parede",
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Clique em uma parede: o DetalhaBIM encontra o resto sozinho.",
                Description = "Clique em uma parede de fachada e depois do lado em que as cotas devem ficar. São encontradas as paredes alinhadas, os vãos de portas e janelas e as paredes internas que chegam na fachada, gerando até três linhas: vãos, paredes e total. Continue clicando em outras paredes; ESC encerra.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "CotasExternas", Text = "Cotas\nExternas", Command = typeof(CotasExternasCommand), Icon = "cotas-externas",
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Cotas externas de todo o pavimento, nos quatro lados.",
                Description = "Identifica automaticamente as paredes de fachada da planta ativa (inclusive em edificações rotacionadas) e cria as linhas de cota de vãos, paredes e total em cada lado escolhido.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "CotaAlinhada", Text = "Cota\nAlinhada", Command = typeof(CotaAlinhadaCommand), Icon = "cota-alinhada",
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Cota paralela à parede em qualquer inclinação.",
                Description = "Clique em uma parede (inclinada, chanfrada ou em planta rotacionada) e no lado a cotar: são criadas cotas alinhadas com a face — vãos, paredes que chegam e comprimento total —, medindo até os cantos reais mesmo quando as pontas são chanfradas. No modo livre, mede entre dois pontos quaisquer paralelamente a uma parede, eixo ou linha.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "CotarObjetos", Text = "Cotar\nObjetos 3D", Command = typeof(CotarObjetosCommand), Icon = "cotas-3d",
                Availability = typeof(DimensionViewAvailability),
                Tooltip = "Cota paredes e qualquer família — inclusive em vistas isométricas 3D.",
                Description = "Selecione paredes, móveis, marcenaria, louças ou qualquer família: largura, profundidade e altura são cotadas na vista ativa — vistas 3D isométricas (a vista é travada automaticamente), plantas, cortes e elevações. Paredes recebem comprimento, espessura, altura e os vãos.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "CotasSelecao", Text = "Por Seleção", Command = typeof(CotasSelecaoCommand), Icon = "cotas-selecao", Small = true,
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Uma cadeia de cotas com os elementos selecionados.",
                Description = "Selecione paredes, eixos, pilares, portas, janelas ou planos de referência e clique onde a linha de cota deve passar. A direção é detectada automaticamente.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "CotasPontos", Text = "Por Pontos", Command = typeof(CotasPontosCommand), Icon = "cotas-pontos", Small = true,
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Cota tudo o que a linha entre dois pontos atravessar.",
                Description = "Clique em dois pontos: todas as paredes, pilares e eixos atravessados pela linha são cotados em uma única cadeia (espessuras e vãos livres). Modo contínuo até ESC.",
            },
            new ToolDef
            {
                Panel = "Cotas", Id = "NiveisAmbiente", Text = "Níveis de Piso", Command = typeof(NiveisAmbienteCommand), Icon = "niveis-ambiente", Small = true,
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Cota de nível do piso acabado em cada ambiente.",
                Description = "Insere uma cota de nível (spot elevation) sobre o piso modelado de cada ambiente, logo abaixo da etiqueta, sem duplicar as existentes.",
            },

            // ------------------------------------------------------------ Vistas
            new ToolDef
            {
                Panel = "Vistas", Id = "VistasAmbiente", Text = "Vistas por\nAmbiente", Command = typeof(VistasAmbienteCommand), Icon = "vistas-ambiente",
                Tooltip = "Planta, forro, elevações internas e isométrico de cada ambiente.",
                Description = "Gera o kit de detalhamento de cada ambiente: planta e forro recortados, uma elevação interna para cada parede e isométrico 3D com caixa de corte — tudo nomeado, na escala e com modelo de vista. Pode cotar, etiquetar e montar uma prancha por ambiente.",
            },
            new ToolDef
            {
                Panel = "Vistas", Id = "PlantasTecnicas", Text = "Plantas\nTécnicas", Command = typeof(PlantasTecnicasCommand), Icon = "plantas-tecnicas",
                Tooltip = "Conjunto de plantas técnicas para vários níveis de uma vez.",
                Description = "Cria as plantas técnicas (baixa, layout, pisos, forro, pontos...) de todos os níveis escolhidos, com nome, escala e modelo padronizados, aplicando etiquetas e cotas automaticamente conforme a configuração de cada planta.",
            },
            new ToolDef
            {
                Panel = "Vistas", Id = "VistasEsquadrias", Text = "Vistas de\nEsquadrias", Command = typeof(VistasEsquadriasCommand), Icon = "esquadrias",
                Tooltip = "Elevação cotada de cada código de esquadria (P01, J01...).",
                Description = "Cria uma elevação para cada tipo de porta e janela, recortada e cotada com largura, altura e peitoril, e monta as pranchas do detalhamento de esquadrias.",
            },
            new ToolDef
            {
                Panel = "Vistas", Id = "Isometrico", Text = "Isométrico", Command = typeof(IsometricoCommand), Icon = "isometrico", Small = true,
                Tooltip = "Isométrico 3D dos ambientes selecionados.",
                Description = "Atalho de um clique: cria o isométrico com caixa de corte ajustada para cada ambiente selecionado, usando as configurações padrão.",
            },
            new ToolDef
            {
                Panel = "Vistas", Id = "Elevacoes", Text = "Elevações", Command = typeof(ElevacoesCommand), Icon = "elevacoes", Small = true,
                Tooltip = "Elevações internas (uma por parede) dos ambientes selecionados.",
                Description = "Atalho de um clique: para cada parede dos ambientes selecionados, cria uma elevação interna recortada e pronta para detalhar.",
            },

            // ------------------------------------------------------------ Interiores
            new ToolDef
            {
                Panel = "Interiores", Id = "Rodapes", Text = "Rodapés", Command = typeof(RodapesCommand), Icon = "rodapes",
                Tooltip = "Rodapé em todo o contorno dos ambientes, cortado nas portas.",
                Description = "Cria o rodapé de cada ambiente pelo contorno de acabamento: como parede fina (altura, espessura e material à escolha, cantos resolvidos, contornando pilares, interrompido em portas e portas-janelas, apoiado sobre o piso) ou como perfil de parede (Wall Sweep) do projeto. Informa o total em metros.",
            },
            new ToolDef
            {
                Panel = "Interiores", Id = "PaginacaoPiso", Text = "Paginação\nde Piso", Command = typeof(PaginacaoPisoCommand), Icon = "paginacao",
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Juntas do piso desenhadas e quantidade de peças por ambiente.",
                Description = "Desenha a paginação do revestimento dentro de cada ambiente (peça, junta, ângulo e ponto de partida: junta ou peça centralizada, canto ou ponto clicado), contornando pilares, e calcula peças inteiras e cortadas, área com perda e caixas. Tabela exportável para Excel.",
            },
            new ToolDef
            {
                Panel = "Interiores", Id = "Acabamentos", Text = "Acabamentos", Command = typeof(AcabamentosCommand), Icon = "acabamentos",
                Tooltip = "Pinta paredes, piso e teto dos ambientes e preenche o quadro de acabamentos.",
                Description = "Aplica materiais de acabamento (ferramenta Pintura) nas faces de paredes, piso e teto que delimitam cada ambiente, criando o material se necessário, e preenche os parâmetros de acabamento de piso, parede, teto e rodapé usados nos quadros.",
            },
            new ToolDef
            {
                Panel = "Interiores", Id = "DetalharMarcenaria", Text = "Detalhar\nMarcenaria", Command = typeof(DetalharMarcenariaCommand), Icon = "marcenaria",
                Tooltip = "Planta, vistas, isométrico cotados e prancha de cada peça.",
                Description = "Para cada móvel de marcenaria, bancada ou mobiliário selecionado: planta recortada, vista frontal, vista lateral e isométrico com caixa de corte — nomeados, na escala, cotados (largura, profundidade e altura) e montados em prancha.",
            },
            new ToolDef
            {
                Panel = "Interiores", Id = "Locacao", Text = "Locação", Command = typeof(LocacaoCommand), Icon = "locacao", Small = true,
                Availability = typeof(PlanOrSectionAvailability),
                Tooltip = "Cotas de locação de móveis e pontos até as paredes e alturas.",
                Description = "Selecione móveis, marcenaria, louças, tomadas, interruptores ou luminárias: em planta, cotas até as paredes mais próximas; em elevações, também a altura em relação ao piso acabado (pela face ou pelo eixo do objeto).",
            },
            new ToolDef
            {
                Panel = "Interiores", Id = "Levantamento", Text = "Levantamento", Command = typeof(LevantamentoCommand), Icon = "levantamento", Small = true,
                Tooltip = "Quantitativo de piso, paredes, teto e rodapé por ambiente.",
                Description = "Calcula por ambiente a área de piso, perímetro, paredes (descontando portas e janelas, até o forro), teto e rodapé (descontando portas), com perda para compra. Copie para o Excel ou exporte em CSV.",
            },

            // ------------------------------------------------------------ Documentação
            new ToolDef
            {
                Panel = "Documentação", Id = "Etiquetar", Text = "Etiquetar\nVistas", Command = typeof(EtiquetarCommand), Icon = "etiquetar",
                Tooltip = "Etiqueta ambientes, portas, janelas e mais em várias vistas.",
                Description = "Etiqueta automaticamente ambientes, portas, janelas, mobiliário, louças e luminárias nas vistas escolhidas, sem duplicar etiquetas existentes.",
            },
            new ToolDef
            {
                Panel = "Documentação", Id = "Quadros", Text = "Quadros e\nTabelas", Command = typeof(QuadrosCommand), Icon = "quadros",
                Tooltip = "Quadros de esquadrias, áreas e acabamentos prontos.",
                Description = "Cria quadros de portas e janelas (código, largura, altura, peitoril, quantidade), de áreas e de acabamentos dos ambientes, com cabeçalhos em português, agrupamentos e totais.",
            },
            new ToolDef
            {
                Panel = "Documentação", Id = "Pranchas", Text = "Criar\nPranchas", Command = typeof(PranchasCommand), Icon = "pranchas",
                Tooltip = "Pranchas em lote com arranjo automático das vistas.",
                Description = "Cria pranchas em lote, numera e distribui as vistas automaticamente dentro da área útil do carimbo — uma vista por prancha ou várias vistas agrupadas.",
            },
            new ToolDef
            {
                Panel = "Documentação", Id = "Renumerar", Text = "Renumerar\nElementos", Command = typeof(RenumerarCommand), Icon = "renumerar",
                Tooltip = "Renumeração automática ou manual por Marca ou Marca de tipo.",
                Description = "Renumera portas, janelas, ambientes, pilares, vagas e outros: automaticamente pela regra de leitura (de cima para baixo, da esquerda para a direita) ou manualmente, clicando na ordem desejada. Escolha entre Marca e Marca de tipo, prefixo, dígitos e incremento.",
            },

            // ------------------------------------------------------------ Organização
            new ToolDef
            {
                Panel = "Organização", Id = "EditorNiveis", Text = "Editor de\nNíveis", Command = typeof(EditorNiveisCommand), Icon = "niveis",
                Tooltip = "Todos os níveis em uma tabela editável.",
                Description = "Veja todos os níveis do projeto e, em uma única interface, renomeie (inclusive em lote), altere elevações, crie plantas de piso e forro, adicione e exclua níveis.",
            },
            new ToolDef
            {
                Panel = "Organização", Id = "Eixos", Text = "Eixos\nAutomáticos", Command = typeof(EixosCommand), Icon = "eixos",
                Availability = typeof(PlanViewAvailability),
                Tooltip = "Eixos a partir das paredes, numerados e cotados.",
                Description = "Cria eixos nos alinhamentos das paredes, nomeia (1, 2, 3 / A, B, C) e gera plantas de eixos já cotadas para vários níveis de uma vez.",
            },
            new ToolDef
            {
                Panel = "Organização", Id = "RemoverAmbientes", Text = "Limpar\nAmbientes", Command = typeof(RemoverAmbientesCommand), Icon = "remover-ambientes",
                Tooltip = "Localiza e exclui ambientes não colocados, não delimitados e redundantes.",
                Description = "Lista os ambientes com problema (não colocados, não delimitados e redundantes) e permite excluí-los definitivamente do projeto.",
            },

            // ------------------------------------------------------------ Modelagem
            new ToolDef
            {
                Panel = "Modelagem", Id = "Forros", Text = "Forros por\nAmbiente", Command = typeof(ForrosCommand), Icon = "forros",
                Tooltip = "Forros pelo contorno dos ambientes, com sanca opcional.",
                Description = "Cria o forro de cada ambiente seguindo o contorno das paredes, na altura desejada, com opção de sanca/rebaixo perimetral configurável.",
            },
            new ToolDef
            {
                Panel = "Modelagem", Id = "Pisos", Text = "Pisos por\nAmbiente", Command = typeof(PisosCommand), Icon = "pisos",
                Tooltip = "Pisos de acabamento pelo contorno dos ambientes.",
                Description = "Cria o piso de acabamento de cada ambiente com o tipo e o desnível desejados (ex.: -2 cm em áreas molhadas).",
            },
            new ToolDef
            {
                Panel = "Modelagem", Id = "Luminarias", Text = "Distribuir\nLuminárias", Command = typeof(LuminariasCommand), Icon = "luminarias",
                Tooltip = "Luminárias em malha uniforme em cada ambiente.",
                Description = "Distribui luminárias em cada ambiente seguindo o perímetro do forro, com espaçamento uniforme e meio espaçamento junto às paredes, hospedando no forro quando existir.",
            },

            // ------------------------------------------------------------ Geral
            new ToolDef
            {
                Panel = "Geral", Id = "Configuracoes", Text = "Configurações", Command = typeof(ConfiguracoesCommand), Icon = "config", Small = true,
                Availability = typeof(AlwaysAvailable),
                Tooltip = "Padrões do escritório: cotas, vistas, plantas técnicas, pranchas...",
                Description = "Defina os padrões do seu escritório (tipos de cota, afastamentos, modelos de vista, escalas, nomes, lista de plantas técnicas, carimbo, prefixos). Exporte e importe para compartilhar com a equipe.",
            },
            new ToolDef
            {
                Panel = "Geral", Id = "Sobre", Text = "Ajuda", Command = typeof(SobreCommand), Icon = "sobre", Small = true,
                Availability = typeof(AlwaysAvailable),
                Tooltip = "Guia rápido das ferramentas e versão.",
                Description = "Mostra todas as ferramentas com instruções rápidas, a versão instalada e atalhos para as pastas de configurações e logs.",
            },
        };
    }
}
