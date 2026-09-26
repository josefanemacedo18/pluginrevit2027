using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace DetalhaBIM.UI
{
    /// <summary>
    /// Tema visual único para todas as janelas do plugin. É carregado a partir de XAML embutido
    /// (sem dependência de URIs "pack://"), garantindo funcionamento dentro do Revit.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Cotas = Color.FromRgb(0x1E, 0x6F, 0xD9);
        public static readonly Color Vistas = Color.FromRgb(0x2E, 0x9E, 0x5B);
        public static readonly Color Documentacao = Color.FromRgb(0xE8, 0x83, 0x0C);
        public static readonly Color Organizacao = Color.FromRgb(0x8E, 0x44, 0xAD);
        public static readonly Color Modelagem = Color.FromRgb(0x00, 0x89, 0x7B);
        public static readonly Color Interiores = Color.FromRgb(0xB0, 0x3A, 0x6E);
        public static readonly Color Geral = Color.FromRgb(0x45, 0x5A, 0x64);

        private static ResourceDictionary _dictionary;

        public static ResourceDictionary Dictionary
        {
            get
            {
                if (_dictionary == null) _dictionary = (ResourceDictionary)XamlReader.Parse(Xaml);
                return _dictionary;
            }
        }

        public static SolidColorBrush Brush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static Color Lighten(Color c, double amount)
        {
            byte L(byte v) => (byte)Math.Min(255, v + (255 - v) * amount);
            return Color.FromRgb(L(c.R), L(c.G), L(c.B));
        }

        private const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <SolidColorBrush x:Key='Accent' Color='#1E6FD9'/>
  <SolidColorBrush x:Key='Border' Color='#D5DAE1'/>
  <SolidColorBrush x:Key='Muted' Color='#6B7785'/>
  <SolidColorBrush x:Key='Surface' Color='#F5F7FA'/>

  <Style TargetType='TextBlock'>
    <Setter Property='TextWrapping' Value='Wrap'/>
    <Setter Property='VerticalAlignment' Value='Center'/>
  </Style>

  <Style x:Key='SectionTitle' TargetType='TextBlock'>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Margin' Value='0,14,0,6'/>
    <Setter Property='Foreground' Value='#26323F'/>
  </Style>

  <Style x:Key='Hint' TargetType='TextBlock'>
    <Setter Property='FontSize' Value='11.5'/>
    <Setter Property='Foreground' Value='#6B7785'/>
    <Setter Property='TextWrapping' Value='Wrap'/>
    <Setter Property='Margin' Value='0,2,0,4'/>
  </Style>

  <Style TargetType='TextBox'>
    <Setter Property='Padding' Value='6,4'/>
    <Setter Property='BorderBrush' Value='#C9D0D9'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='MinHeight' Value='28'/>
  </Style>

  <Style TargetType='ComboBox'>
    <Setter Property='MinHeight' Value='28'/>
    <Setter Property='Padding' Value='6,4'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
  </Style>

  <Style TargetType='CheckBox'>
    <Setter Property='Margin' Value='0,4'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
  </Style>

  <Style TargetType='RadioButton'>
    <Setter Property='Margin' Value='0,3'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
  </Style>

  <Style x:Key='BaseButton' TargetType='Button'>
    <Setter Property='MinWidth' Value='96'/>
    <Setter Property='MinHeight' Value='32'/>
    <Setter Property='Padding' Value='14,4'/>
    <Setter Property='Margin' Value='6,0,0,0'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Background' Value='White'/>
    <Setter Property='Foreground' Value='#26323F'/>
    <Setter Property='BorderBrush' Value='#C9D0D9'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='bd' CornerRadius='5' Background='{TemplateBinding Background}'
                  BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='bd' Property='Opacity' Value='0.88'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='bd' Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='Button' BasedOn='{StaticResource BaseButton}'/>

  <Style x:Key='PrimaryButton' TargetType='Button' BasedOn='{StaticResource BaseButton}'>
    <Setter Property='Background' Value='{DynamicResource Accent}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Accent}'/>
    <Setter Property='Foreground' Value='White'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
  </Style>

  <Style x:Key='LinkButton' TargetType='Button' BasedOn='{StaticResource BaseButton}'>
    <Setter Property='MinWidth' Value='0'/>
    <Setter Property='MinHeight' Value='24'/>
    <Setter Property='Padding' Value='8,2'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderBrush' Value='Transparent'/>
    <Setter Property='Foreground' Value='{DynamicResource Accent}'/>
  </Style>

  <Style TargetType='DataGrid'>
    <Setter Property='AutoGenerateColumns' Value='False'/>
    <Setter Property='CanUserAddRows' Value='False'/>
    <Setter Property='CanUserDeleteRows' Value='False'/>
    <Setter Property='HeadersVisibility' Value='Column'/>
    <Setter Property='GridLinesVisibility' Value='Horizontal'/>
    <Setter Property='HorizontalGridLinesBrush' Value='#E6EAF0'/>
    <Setter Property='BorderBrush' Value='#D5DAE1'/>
    <Setter Property='RowHeight' Value='28'/>
    <Setter Property='Background' Value='White'/>
    <Setter Property='AlternatingRowBackground' Value='#F8FAFC'/>
    <Setter Property='SelectionMode' Value='Extended'/>
  </Style>

  <Style TargetType='DataGridColumnHeader'>
    <Setter Property='Padding' Value='8,6'/>
    <Setter Property='Background' Value='#EEF2F6'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='BorderBrush' Value='#D5DAE1'/>
    <Setter Property='BorderThickness' Value='0,0,1,1'/>
  </Style>

  <Style TargetType='TabItem'>
    <Setter Property='Padding' Value='14,6'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
  </Style>
</ResourceDictionary>";
    }
}
