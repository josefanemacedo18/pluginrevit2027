using System.Collections.Generic;

namespace DetalhaBIM.Core
{
    /// <summary>
    /// Configurações do escritório. Todos os nomes (tipos de cota, modelos de vista, carimbos)
    /// são guardados como texto para que o plugin funcione com qualquer template: se o nome não
    /// existir no projeto aberto, o padrão do Revit é usado.
    /// </summary>
    public class DetalhaSettings
    {
        public CotasSettings Cotas { get; set; } = new CotasSettings();
        public VistasSettings Vistas { get; set; } = new VistasSettings();
        public List<PlantaTecnicaDef> PlantasTecnicas { get; set; } = PlantaTecnicaDef.Padrao();
        public RenumeracaoSettings Renumeracao { get; set; } = new RenumeracaoSettings();
        public ModelagemSettings Modelagem { get; set; } = new ModelagemSettings();
        public PranchasSettings Pranchas { get; set; } = new PranchasSettings();
    }

    public class CotasSettings
    {
        /// <summary>Nome do tipo de cota linear. Vazio = tipo padrão do projeto.</summary>
        public string TipoCota { get; set; } = "";
        /// <summary>Distância da 1ª linha de cota até a parede, em mm na folha.</summary>
        public double PrimeiraLinhaMm { get; set; } = 8;
        /// <summary>Espaçamento entre linhas de cota paralelas, em mm na folha.</summary>
        public double EspacamentoMm { get; set; } = 7;
        /// <summary>Distância das cotas internas até a parede, em mm na folha.</summary>
        public double AfastamentoInternoMm { get; set; } = 6;
        /// <summary>Segmentos menores que isto (cm) são ignorados para evitar cotas sobrepostas.</summary>
        public double MenorSegmentoCm { get; set; } = 1;
        /// <summary>Nome do tipo de cota de nível (spot elevation). Vazio = padrão.</summary>
        public string TipoCotaNivel { get; set; } = "";
    }

    public class VistasSettings
    {
        public string PadraoNome { get; set; } = "{NUMERO} - {NOME}";
        public string SufixoPlanta { get; set; } = "PLANTA";
        public string SufixoForro { get; set; } = "FORRO";
        public string SufixoElevacao { get; set; } = "ELEVAÇÃO";
        public string SufixoIsometrico { get; set; } = "ISOMÉTRICO";

        public string ModeloPlanta { get; set; } = "";
        public string ModeloForro { get; set; } = "";
        public string ModeloElevacao { get; set; } = "";
        public string Modelo3D { get; set; } = "";
        public string ModeloEsquadria { get; set; } = "";

        public int EscalaPlanta { get; set; } = 25;
        public int EscalaElevacao { get; set; } = 25;
        public int Escala3D { get; set; } = 50;
        public int EscalaEsquadria { get; set; } = 25;

        /// <summary>Folga do recorte das vistas em torno do ambiente (cm).</summary>
        public double FolgaRecorteCm { get; set; } = 40;
        /// <summary>Folga lateral/vertical do recorte das elevações internas (cm).</summary>
        public double FolgaElevacaoCm { get; set; } = 15;
        /// <summary>Distância máxima do marcador de elevação até a parede (cm).</summary>
        public double DistanciaMarcadorCm { get; set; } = 120;
        /// <summary>Paredes com comprimento menor que isto não geram elevação (cm).</summary>
        public double MenorParedeElevacaoCm { get; set; } = 40;
        /// <summary>Oculta a caixa de corte nos isométricos.</summary>
        public bool OcultarCaixaCorte { get; set; } = true;
        /// <summary>Orientação do isométrico: SE, SO, NE, NO.</summary>
        public string OrientacaoIsometrico { get; set; } = "SE";
    }

    public class PlantaTecnicaDef
    {
        public bool Ativa { get; set; } = true;
        public string Nome { get; set; } = "PLANTA BAIXA";
        /// <summary>"Piso" ou "Forro".</summary>
        public string Tipo { get; set; } = "Piso";
        public string Modelo { get; set; } = "";
        public int Escala { get; set; } = 50;
        public bool EtiquetarAmbientes { get; set; }
        public bool EtiquetarEsquadrias { get; set; }
        public bool CotasExternas { get; set; }
        public bool CotasInternas { get; set; }

        public static List<PlantaTecnicaDef> Padrao()
        {
            return new List<PlantaTecnicaDef>
            {
                new PlantaTecnicaDef { Nome = "PLANTA BAIXA", EtiquetarAmbientes = true, EtiquetarEsquadrias = true, CotasExternas = true, CotasInternas = true },
                new PlantaTecnicaDef { Nome = "PLANTA DE LAYOUT", EtiquetarAmbientes = true },
                new PlantaTecnicaDef { Nome = "PLANTA DE DEMOLIR E CONSTRUIR", Ativa = false },
                new PlantaTecnicaDef { Nome = "PLANTA DE PISOS", EtiquetarAmbientes = true },
                new PlantaTecnicaDef { Nome = "PLANTA DE PONTOS ELÉTRICOS", Ativa = false },
                new PlantaTecnicaDef { Nome = "PLANTA DE PONTOS HIDRÁULICOS", Ativa = false },
                new PlantaTecnicaDef { Nome = "PLANTA DE FORRO", Tipo = "Forro", EtiquetarAmbientes = true },
                new PlantaTecnicaDef { Nome = "PLANTA DE LUMINOTÉCNICA", Tipo = "Forro", Ativa = false },
            };
        }
    }

    public class RenumeracaoSettings
    {
        public string PrefixoPortas { get; set; } = "P";
        public string PrefixoJanelas { get; set; } = "J";
        public string PrefixoAmbientes { get; set; } = "";
        public string PrefixoPilares { get; set; } = "P";
        public string PrefixoOutros { get; set; } = "";
        public int Digitos { get; set; } = 2;
        /// <summary>Tolerância para considerar elementos na mesma "linha" de leitura (cm).</summary>
        public double ToleranciaLinhaCm { get; set; } = 100;
    }

    public class ModelagemSettings
    {
        public string TipoForro { get; set; } = "";
        public double AlturaForroCm { get; set; } = 260;
        public double LarguraSancaCm { get; set; } = 40;
        public double RebaixoSancaCm { get; set; } = 20;
        public string TipoPiso { get; set; } = "";
        public double DesnivelPisoCm { get; set; } = 0;
        public string TipoLuminaria { get; set; } = "";
        public double EspacamentoLuminariaCm { get; set; } = 150;
        public double AfastamentoLuminariaCm { get; set; } = 50;
    }

    public class PranchasSettings
    {
        public string Carimbo { get; set; } = "";
        public string PrefixoNumero { get; set; } = "A-";
        public int Digitos { get; set; } = 2;
        /// <summary>Faixa reservada ao carimbo, à direita da folha (mm).</summary>
        public double FaixaCarimboDireitaMm { get; set; } = 185;
        /// <summary>Faixa reservada ao carimbo, na base da folha (mm).</summary>
        public double FaixaCarimboInferiorMm { get; set; } = 0;
        public double MargemMm { get; set; } = 12;
        public double EspacoEntreVistasMm { get; set; } = 15;
    }
}
