using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DetalhaBIM.Core;
using Microsoft.Win32;

namespace DetalhaBIM.UI
{
    /// <summary>Listas de nomes do projeto aberto para sugerir nas configurações.</summary>
    public class ProjectLists
    {
        public List<string> DimensionTypes { get; set; } = new List<string>();
        public List<string> SpotTypes { get; set; } = new List<string>();
        public List<string> PlanTemplates { get; set; } = new List<string>();
        public List<string> CeilingTemplates { get; set; } = new List<string>();
        public List<string> ElevationTemplates { get; set; } = new List<string>();
        public List<string> Templates3D { get; set; } = new List<string>();
        public List<string> CeilingTypes { get; set; } = new List<string>();
        public List<string> FloorTypes { get; set; } = new List<string>();
        public List<string> Lights { get; set; } = new List<string>();
        public List<string> TitleBlocks { get; set; } = new List<string>();
    }

    /// <summary>Configurações do escritório organizadas em abas.</summary>
    public class SettingsWindow : DetalhaWindow
    {
        private readonly ProjectLists _lists;
        private DetalhaSettings _s;
        private readonly TabControl _tabs = new TabControl();
        private readonly Dictionary<string, FormBuilder> _forms = new Dictionary<string, FormBuilder>();
        private ObservableCollection<PlantaTecnicaDef> _plans;

        public SettingsWindow(DetalhaSettings settings, ProjectLists lists, IntPtr owner)
            : base("Configurações", "Padrões do escritório usados por todas as ferramentas. Os nomes de tipos e modelos são procurados no projeto aberto; se não existirem, o padrão do Revit é usado.",
                Theme.Geral, owner, 760)
        {
            _s = settings;
            _lists = lists;
            SizeToContent = SizeToContent.Manual;
            Height = 680;
            BuildTabs();
            SetBody(_tabs, scroll: false);

            AddButton("Exportar...", (s, e) => Export(), left: true);
            AddButton("Importar...", (s, e) => Import(), left: true);
            AddButton("Restaurar padrões", (s, e) =>
            {
                if (MessageBox.Show(this, "Restaurar todas as configurações para o padrão?", "DetalhaBIM", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                _s = new DetalhaSettings();
                BuildTabs();
            }, left: true);
            AddButton("Cancelar", (s, e) => DialogResult = false);
            AddButton("Salvar", (s, e) => Save(), primary: true);
        }

        public DetalhaSettings Result { get; private set; }

        private static List<string> With(string current, List<string> items)
        {
            var list = new List<string> { string.Empty };
            list.AddRange(items);
            if (!string.IsNullOrWhiteSpace(current) && !list.Contains(current)) list.Add(current);
            return list;
        }

        private FormBuilder Tab(string key, string header)
        {
            var form = new FormBuilder("config-" + key);
            _forms[key] = form;
            var scroll = new ScrollViewer { Content = form.Panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14, 4, 14, 10) };
            _tabs.Items.Add(new TabItem { Header = header, Content = scroll });
            return form;
        }

        private void BuildTabs()
        {
            _tabs.Items.Clear();
            _forms.Clear();

            CotasSettings c = _s.Cotas;
            FormBuilder f = Tab("cotas", "Cotas");
            f.Section("Tipos");
            f.Combo("tipo", "Tipo de cota linear", With(c.TipoCota, _lists.DimensionTypes), c.TipoCota, true, "Vazio = tipo padrão do projeto");
            f.Combo("tipoNivel", "Tipo de cota de nível", With(c.TipoCotaNivel, _lists.SpotTypes), c.TipoCotaNivel, true);
            f.Section("Afastamentos (mm na folha — independem da escala)");
            f.Number("primeira", "Da parede até a 1ª linha de cota", c.PrimeiraLinhaMm, "mm");
            f.Number("espaco", "Entre linhas de cota paralelas", c.EspacamentoMm, "mm");
            f.Number("interno", "Cotas internas até a parede", c.AfastamentoInternoMm, "mm");
            f.Section("Precisão");
            f.Number("menor", "Ignorar segmentos menores que", c.MenorSegmentoCm, "cm");

            VistasSettings v = _s.Vistas;
            f = Tab("vistas", "Vistas");
            f.Section("Nomes das vistas por ambiente");
            f.Text("padrao", "Padrão de nome", v.PadraoNome, "Use {NUMERO}, {NOME} e {NIVEL}");
            f.Hint("Exemplo: \"{NUMERO} - {NOME}\" gera \"101 - COZINHA - PLANTA\".");
            f.Text("sPlanta", "Sufixo da planta", v.SufixoPlanta);
            f.Text("sForro", "Sufixo do forro", v.SufixoForro);
            f.Text("sElev", "Sufixo das elevações", v.SufixoElevacao);
            f.Text("sIso", "Sufixo do isométrico", v.SufixoIsometrico);
            f.Section("Modelos de vista (View Templates)");
            f.Combo("mPlanta", "Planta do ambiente", With(v.ModeloPlanta, _lists.PlanTemplates), v.ModeloPlanta, true);
            f.Combo("mForro", "Forro do ambiente", With(v.ModeloForro, _lists.CeilingTemplates), v.ModeloForro, true);
            f.Combo("mElev", "Elevações internas", With(v.ModeloElevacao, _lists.ElevationTemplates), v.ModeloElevacao, true);
            f.Combo("m3d", "Isométrico", With(v.Modelo3D, _lists.Templates3D), v.Modelo3D, true);
            f.Combo("mEsq", "Vistas de esquadrias", With(v.ModeloEsquadria, _lists.ElevationTemplates), v.ModeloEsquadria, true);
            f.Section("Escalas");
            f.Combo("ePlanta", "Plantas de ambiente", Choices.Scales, Choices.Scale(v.EscalaPlanta), true);
            f.Combo("eElev", "Elevações internas", Choices.Scales, Choices.Scale(v.EscalaElevacao), true);
            f.Combo("e3d", "Isométrico", Choices.Scales, Choices.Scale(v.Escala3D), true);
            f.Combo("eEsq", "Esquadrias", Choices.Scales, Choices.Scale(v.EscalaEsquadria), true);
            f.Section("Recortes e elevações");
            f.Number("folga", "Folga do recorte em torno do ambiente", v.FolgaRecorteCm, "cm");
            f.Number("folgaElev", "Folga do recorte das elevações", v.FolgaElevacaoCm, "cm");
            f.Number("marcador", "Distância máxima do marcador à parede", v.DistanciaMarcadorCm, "cm");
            f.Number("menorParede", "Não criar elevação em paredes menores que", v.MenorParedeElevacaoCm, "cm");
            f.Combo("orientacao", "Orientação do isométrico", new List<string> { "SE", "SO", "NE", "NO" }, v.OrientacaoIsometrico);
            f.Check("ocultarCaixa", "Ocultar a caixa de corte nos isométricos", v.OcultarCaixaCorte);

            BuildPlansTab();

            RenumeracaoSettings r = _s.Renumeracao;
            f = Tab("renumeracao", "Renumeração");
            f.Section("Prefixos padrão");
            f.Text("pPortas", "Portas", r.PrefixoPortas);
            f.Text("pJanelas", "Janelas", r.PrefixoJanelas);
            f.Text("pAmbientes", "Ambientes", r.PrefixoAmbientes);
            f.Text("pPilares", "Pilares", r.PrefixoPilares);
            f.Text("pOutros", "Outros", r.PrefixoOutros);
            f.Section("Formato");
            f.Number("digitos", "Dígitos", r.Digitos);
            f.Number("tolerancia", "Tolerância da linha de leitura", r.ToleranciaLinhaCm, "cm");

            ModelagemSettings m = _s.Modelagem;
            f = Tab("modelagem", "Modelagem");
            f.Section("Forros");
            f.Combo("tForro", "Tipo de forro", With(m.TipoForro, _lists.CeilingTypes), m.TipoForro, true);
            f.Number("altura", "Altura do forro", m.AlturaForroCm, "cm");
            f.Number("sanca", "Largura da sanca", m.LarguraSancaCm, "cm");
            f.Number("rebaixo", "Rebaixo da sanca", m.RebaixoSancaCm, "cm");
            f.Section("Pisos");
            f.Combo("tPiso", "Tipo de piso", With(m.TipoPiso, _lists.FloorTypes), m.TipoPiso, true);
            f.Number("desnivel", "Desnível padrão", m.DesnivelPisoCm, "cm");
            f.Section("Luminárias");
            f.Combo("tLum", "Luminária (Família : Tipo)", With(m.TipoLuminaria, _lists.Lights), m.TipoLuminaria, true);
            f.Number("espLum", "Espaçamento máximo", m.EspacamentoLuminariaCm, "cm");
            f.Number("afLum", "Afastamento das paredes", m.AfastamentoLuminariaCm, "cm");

            PranchasSettings p = _s.Pranchas;
            f = Tab("pranchas", "Pranchas");
            f.Section("Folha");
            f.Combo("carimbo", "Carimbo", With(p.Carimbo, _lists.TitleBlocks), p.Carimbo, true);
            f.Text("prefixo", "Prefixo da numeração", p.PrefixoNumero);
            f.Number("digitos", "Dígitos da numeração", p.Digitos);
            f.Section("Área útil (mm)");
            f.Number("faixaDir", "Faixa do carimbo à direita", p.FaixaCarimboDireitaMm, "mm");
            f.Number("faixaInf", "Faixa do carimbo na base", p.FaixaCarimboInferiorMm, "mm");
            f.Number("margem", "Margem interna", p.MargemMm, "mm");
            f.Number("espaco", "Espaço entre vistas", p.EspacoEntreVistasMm, "mm");
            f.Hint("O arranjo automático usa os limites do carimbo, descontando as faixas acima (onde fica o selo/legenda).");
        }

        private void BuildPlansTab()
        {
            _plans = new ObservableCollection<PlantaTecnicaDef>(_s.PlantasTecnicas.Select(Copy));
            var root = new DockPanel { Margin = new Thickness(10) };
            var hint = new TextBlock
            {
                Text = "Lista usada pela ferramenta Plantas Técnicas. Nome final: \"<Nome> - <Nível>\". Marque as automações desejadas para cada planta.",
                Margin = new Thickness(0, 0, 0, 8),
            };
            hint.SetResourceReference(StyleProperty, "Hint");
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            var bar = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);

            var grid = new DataGrid { ItemsSource = _plans, CanUserAddRows = false };
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Ativa", Binding = new Binding(nameof(PlantaTecnicaDef.Ativa)), Width = 50 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Nome", Binding = new Binding(nameof(PlantaTecnicaDef.Nome)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Tipo",
                ItemsSource = new[] { "Piso", "Forro" },
                SelectedItemBinding = new Binding(nameof(PlantaTecnicaDef.Tipo)),
                Width = 70,
            });
            var templates = new List<string> { string.Empty };
            templates.AddRange(_lists.PlanTemplates.Concat(_lists.CeilingTemplates).Distinct());
            templates.AddRange(_plans.Select(p => p.Modelo).Where(m => !string.IsNullOrWhiteSpace(m) && !templates.Contains(m)).Distinct().ToList());
            grid.Columns.Add(new DataGridComboBoxColumn
            {
                Header = "Modelo de vista",
                ItemsSource = templates,
                SelectedItemBinding = new Binding(nameof(PlantaTecnicaDef.Modelo)),
                Width = new DataGridLength(1.6, DataGridLengthUnitType.Star),
            });
            grid.Columns.Add(new DataGridTextColumn { Header = "Escala 1:", Binding = new Binding(nameof(PlantaTecnicaDef.Escala)), Width = 65 });
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Etiq. amb.", Binding = new Binding(nameof(PlantaTecnicaDef.EtiquetarAmbientes)), Width = 70 });
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Etiq. esq.", Binding = new Binding(nameof(PlantaTecnicaDef.EtiquetarEsquadrias)), Width = 70 });
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Cotas ext.", Binding = new Binding(nameof(PlantaTecnicaDef.CotasExternas)), Width = 70 });
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Cotas int.", Binding = new Binding(nameof(PlantaTecnicaDef.CotasInternas)), Width = 70 });
            root.Children.Add(grid);

            Button B(string text, Action a)
            {
                var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0), MinWidth = 0 };
                b.Click += (s, e) =>
                {
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                    a();
                };
                bar.Children.Add(b);
                return b;
            }

            B("+ Adicionar", () =>
            {
                var p = new PlantaTecnicaDef { Nome = "NOVA PLANTA" };
                _plans.Add(p);
                grid.SelectedItem = p;
            });
            B("Remover", () =>
            {
                if (grid.SelectedItem is PlantaTecnicaDef p) _plans.Remove(p);
            });
            B("↑ Subir", () => Move(grid, -1));
            B("↓ Descer", () => Move(grid, 1));
            B("Lista padrão", () =>
            {
                _plans.Clear();
                foreach (PlantaTecnicaDef p in PlantaTecnicaDef.Padrao()) _plans.Add(p);
            });

            _tabs.Items.Insert(2, new TabItem { Header = "Plantas técnicas", Content = root });
        }

        private void Move(DataGrid grid, int delta)
        {
            if (!(grid.SelectedItem is PlantaTecnicaDef p)) return;
            int i = _plans.IndexOf(p), j = i + delta;
            if (j < 0 || j >= _plans.Count) return;
            _plans.Move(i, j);
            grid.SelectedItem = p;
        }

        private static PlantaTecnicaDef Copy(PlantaTecnicaDef p) => new PlantaTecnicaDef
        {
            Ativa = p.Ativa,
            Nome = p.Nome,
            Tipo = p.Tipo,
            Modelo = p.Modelo,
            Escala = p.Escala,
            EtiquetarAmbientes = p.EtiquetarAmbientes,
            EtiquetarEsquadrias = p.EtiquetarEsquadrias,
            CotasExternas = p.CotasExternas,
            CotasInternas = p.CotasInternas,
        };

        private DetalhaSettings Collect()
        {
            foreach (FormBuilder form in _forms.Values)
            {
                string error = form.Validate();
                if (error != null) throw new InvalidOperationException(error);
            }

            FormBuilder c = _forms["cotas"], v = _forms["vistas"], r = _forms["renumeracao"], m = _forms["modelagem"], p = _forms["pranchas"];
            return new DetalhaSettings
            {
                Cotas = new CotasSettings
                {
                    TipoCota = c.String("tipo"),
                    TipoCotaNivel = c.String("tipoNivel"),
                    PrimeiraLinhaMm = c.Double("primeira"),
                    EspacamentoMm = c.Double("espaco"),
                    AfastamentoInternoMm = c.Double("interno"),
                    MenorSegmentoCm = c.Double("menor"),
                },
                Vistas = new VistasSettings
                {
                    PadraoNome = v.String("padrao"),
                    SufixoPlanta = v.String("sPlanta"),
                    SufixoForro = v.String("sForro"),
                    SufixoElevacao = v.String("sElev"),
                    SufixoIsometrico = v.String("sIso"),
                    ModeloPlanta = v.String("mPlanta"),
                    ModeloForro = v.String("mForro"),
                    ModeloElevacao = v.String("mElev"),
                    Modelo3D = v.String("m3d"),
                    ModeloEsquadria = v.String("mEsq"),
                    EscalaPlanta = Choices.ParseScale(v.String("ePlanta"), 25),
                    EscalaElevacao = Choices.ParseScale(v.String("eElev"), 25),
                    Escala3D = Choices.ParseScale(v.String("e3d"), 50),
                    EscalaEsquadria = Choices.ParseScale(v.String("eEsq"), 25),
                    FolgaRecorteCm = v.Double("folga"),
                    FolgaElevacaoCm = v.Double("folgaElev"),
                    DistanciaMarcadorCm = v.Double("marcador"),
                    MenorParedeElevacaoCm = v.Double("menorParede"),
                    OrientacaoIsometrico = v.String("orientacao"),
                    OcultarCaixaCorte = v.Bool("ocultarCaixa"),
                },
                PlantasTecnicas = _plans.Where(x => !string.IsNullOrWhiteSpace(x.Nome)).Select(Copy).ToList(),
                Renumeracao = new RenumeracaoSettings
                {
                    PrefixoPortas = r.String("pPortas"),
                    PrefixoJanelas = r.String("pJanelas"),
                    PrefixoAmbientes = r.String("pAmbientes"),
                    PrefixoPilares = r.String("pPilares"),
                    PrefixoOutros = r.String("pOutros"),
                    Digitos = Math.Max(1, r.Int("digitos")),
                    ToleranciaLinhaCm = r.Double("tolerancia"),
                },
                Modelagem = new ModelagemSettings
                {
                    TipoForro = m.String("tForro"),
                    AlturaForroCm = m.Double("altura"),
                    LarguraSancaCm = m.Double("sanca"),
                    RebaixoSancaCm = m.Double("rebaixo"),
                    TipoPiso = m.String("tPiso"),
                    DesnivelPisoCm = m.Double("desnivel"),
                    TipoLuminaria = m.String("tLum"),
                    EspacamentoLuminariaCm = m.Double("espLum"),
                    AfastamentoLuminariaCm = m.Double("afLum"),
                },
                Pranchas = new PranchasSettings
                {
                    Carimbo = p.String("carimbo"),
                    PrefixoNumero = p.String("prefixo"),
                    Digitos = Math.Max(1, p.Int("digitos")),
                    FaixaCarimboDireitaMm = p.Double("faixaDir"),
                    FaixaCarimboInferiorMm = p.Double("faixaInf"),
                    MargemMm = p.Double("margem"),
                    EspacoEntreVistasMm = p.Double("espaco"),
                },
            };
        }

        private void Save()
        {
            try
            {
                Result = Collect();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Export()
        {
            try
            {
                DetalhaSettings current = Collect();
                var dlg = new SaveFileDialog { Filter = "Configurações DetalhaBIM (*.json)|*.json", FileName = "DetalhaBIM-configuracoes.json" };
                if (dlg.ShowDialog(this) != true) return;
                SettingsService.Save(current);
                SettingsService.Export(dlg.FileName);
                MessageBox.Show(this, "Configurações exportadas. Compartilhe o arquivo com a equipe para padronizar o escritório.", "DetalhaBIM");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Import()
        {
            var dlg = new OpenFileDialog { Filter = "Configurações DetalhaBIM (*.json)|*.json" };
            if (dlg.ShowDialog(this) != true) return;
            if (!SettingsService.Import(dlg.FileName))
            {
                MessageBox.Show(this, "Arquivo inválido.", "DetalhaBIM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _s = SettingsService.Clone();
            BuildTabs();
            MessageBox.Show(this, "Configurações importadas.", "DetalhaBIM");
        }
    }
}
