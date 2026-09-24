# DetalhaBIM — plugin de detalhamento arquitetônico para Revit 2027

O DetalhaBIM é um plugin para o **Autodesk Revit 2027** que automatiza as tarefas repetitivas do
detalhamento arquitetônico: **cotas, vistas por ambiente, plantas técnicas, esquadrias, quadros,
pranchas, renumeração, níveis, eixos, forros, pisos e luminárias**. Ele segue o fluxo de trabalho
de plugins como o DetBox (RR Engenharia), mas é um projeto independente.

Todas as ferramentas ficam na aba **DetalhaBIM** da faixa de opções, estão em português e usam
diálogos que **lembram as últimas opções escolhidas**. Os padrões do escritório ficam em
**Configurações**, e o plugin se adapta a qualquer template.

## ⬇️ Como instalar (5 minutos)

1. **Baixe o plugin:** [**DetalhaBIM-Revit2027.zip**](https://github.com/josefanemacedo18/pluginrevit2027/releases/latest/download/DetalhaBIM-Revit2027.zip).
   Se o navegador avisar que o arquivo "não é baixado com frequência", escolha **Manter**. Outra opção é
   baixar os 2 arquivos direto da pasta [`INSTALAR`](INSTALAR) deste repositório.
2. **Desligue o Controle de Aplicativo Inteligente do Windows 11:** clique em Iniciar, digite
   *Controle de Aplicativo Inteligente*, escolha **Desativado** e reinicie o computador. É obrigatório,
   porque o plugin não tem assinatura digital paga.
3. **Feche o Revit.** Clique com o botão direito no ZIP e vá em **Propriedades**. Se houver a caixa
   **Desbloquear**, marque-a e dê OK. Depois clique com o botão direito de novo e escolha **Extrair tudo**.
4. **Copie os 2 arquivos** da pasta `INSTALAR`, `DetalhaBIM.addin` e `DetalhaBIM.dll`, para esta pasta
   (cole o endereço na barra do Explorador de Arquivos):
   ```
   %AppData%\Autodesk\Revit\Addins\2027
   ```
   Os dois precisam ficar **lado a lado**, na mesma pasta. Se sobraram restos de tentativas anteriores
   (a pasta `DetalhaBIM` ali dentro, ou `%AppData%\Autodesk\ApplicationPlugins\DetalhaBIM.bundle`), apague-os.
5. **Abra o Revit 2027** e escolha **"Sempre carregar"**. A aba **DetalhaBIM** aparece na faixa de opções.

Passo a passo detalhado e solução de problemas: [`docs/COMO-INSTALAR.txt`](docs/COMO-INSTALAR.txt).

> Manual completo, ferramenta por ferramenta: [`docs/MANUAL.md`](docs/MANUAL.md)

---

## Ferramentas (22)

| Painel | Ferramenta | O que faz |
|---|---|---|
| **Cotas** | Cotas por Ambiente | Cria cadeias internas face a face (horizontal e vertical) em cada ambiente e cota os vãos de portas e janelas ao longo das paredes. |
| | Cotas por Parede | Você clica em uma parede e o plugin encontra sozinho as paredes alinhadas, os vãos e as paredes internas. Gera 3 linhas: vãos, paredes e total. |
| | Cotas Externas | Cria as cotas externas do pavimento inteiro, nos 4 lados. Também funciona em edificações rotacionadas. |
| | Por Seleção | Cria uma única cadeia com paredes, eixos, pilares, esquadrias e planos selecionados. |
| | Por Pontos | Você clica em 2 pontos e o plugin cota tudo o que a linha atravessar. |
| | Níveis de Piso | Insere a cota de nível do piso acabado em cada ambiente. |
| **Vistas** | Vistas por Ambiente | Cria planta, forro, uma elevação por parede e o isométrico de cada ambiente. As vistas já saem recortadas, nomeadas, com modelo de vista, cotadas e em prancha. |
| | Plantas Técnicas | Cria as plantas de layout, cotas, pisos, forro, pontos etc. para vários níveis de uma vez, já com etiquetas e cotas. |
| | Vistas de Esquadrias | Cria uma elevação de cada código (P01, J01…) cotada com largura, altura e peitoril, e monta a prancha. |
| | Isométrico / Elevações | Atalhos de um clique para os ambientes selecionados. |
| **Documentação** | Etiquetar Vistas | Etiqueta ambientes, portas, janelas, mobiliário, louças e luminárias em várias vistas, sem duplicar etiquetas. |
| | Quadros e Tabelas | Cria os quadros de portas, janelas, áreas (com total) e acabamentos, com cabeçalhos em português. |
| | Criar Pranchas | Cria pranchas em lote, numera e distribui as vistas automaticamente dentro do carimbo. |
| | Renumerar Elementos | Numera por Marca ou Marca de tipo, na ordem de leitura (de cima para baixo e da esquerda para a direita) ou clicando nos elementos. |
| **Organização** | Editor de Níveis | Mostra todos os níveis numa tabela. Nela você renomeia (inclusive em lote), altera elevações, cria plantas, adiciona e exclui níveis. |
| | Eixos Automáticos | Cria eixos a partir das paredes, com nomes 1, 2, 3 / A, B, C, e gera plantas de eixos cotadas para vários níveis. |
| | Limpar Ambientes | Encontra e exclui ambientes não colocados, não delimitados e redundantes. |
| **Modelagem** | Forros por Ambiente | Cria o forro pelo contorno do ambiente, na altura escolhida, com sanca perimetral opcional. |
| | Pisos por Ambiente | Cria o piso de acabamento pelo contorno do ambiente, com desnível (por exemplo, −2 cm em áreas molhadas). |
| | Distribuir Luminárias | Distribui luminárias em malha uniforme, com meio espaçamento junto às paredes, e as hospeda no forro. |
| **DetalhaBIM** | Configurações / Ajuda | Guarda os padrões do escritório (com importação e exportação) e traz o guia rápido das ferramentas. |

---

## Detalhes da instalação

- **Por que essa pasta:** no Revit 2027, `%AppData%\Autodesk\Revit\Addins\2027` é o local que o Revit
  garante carregar para plugins sem assinatura digital. O Revit 2027 ignora `C:\ProgramData\...\Addins\2027`.
- **Por que os 2 arquivos ficam juntos:** o `DetalhaBIM.addin` aponta para `DetalhaBIM.dll` na mesma pasta.
- **Atualizar:** substitua os 2 arquivos, com o Revit fechado.
- **Desinstalar:** apague `DetalhaBIM.addin` e `DetalhaBIM.dll`. As configurações em `%AppData%\DetalhaBIM`
  são mantidas.
- **Garantia de integridade:** a cada alteração, a compilação automática (GitHub Actions) roda em Windows e
  verifica os arquivos da pasta `INSTALAR`, o ZIP e a interface.
  - **O que é conferido:** se o manifesto é válido, se a DLL existe no caminho indicado, se a DLL foi
    compilada exatamente do código atual (impressão digital), se usa a versão da API compatível com
    qualquer atualização do Revit 2027, se os 23 comandos estão corretos, e os ícones e janelas.

### Controle de Aplicativo Inteligente (Smart App Control) do Windows 11

Esse recurso do Windows bloqueia **qualquer programa ou DLL sem assinatura digital** de uma autoridade
reconhecida pela Microsoft. Isso inclui scripts `.bat`/`.ps1` baixados e plugins do Revit sem assinatura.
O Windows **não permite abrir exceção** para um arquivo específico.

Quando há bloqueio, o Revit abre sem a aba DetalhaBIM ou mostra uma mensagem dizendo que "uma política de
Controle de Aplicativo bloqueou este arquivo". Há duas soluções:

1. **Desativar o recurso:** clique em Iniciar e digite *Controle de Aplicativo Inteligente* (ou vá em
   *Segurança do Windows → Controle de aplicativos e do navegador → Configurações do Controle de Aplicativo
   Inteligente*) e escolha **Desativado**. Evite *Avaliação*, porque nesse modo o Windows pode religar o
   recurso sozinho. O Microsoft Defender Antivírus
   continua ativo. A partir da atualização de abril de 2026 do Windows 11 (KB5083769), o recurso pode ser
   religado depois pela mesma tela. Nas versões anteriores, desligar era definitivo.
2. **Assinar o plugin** com um certificado de assinatura de código (veja abaixo). Com a assinatura, o
   recurso pode continuar ligado.

### Assinatura digital (opcional)

Com um certificado de *code signing* de uma autoridade reconhecida pela Microsoft (Certum, Sectigo,
DigiCert, SSL.com, Azure Artifact Signing etc.) instalado no computador, compile já assinando:

```powershell
dotnet build src/DetalhaBIM/DetalhaBIM.csproj -c Release -p:SignThumbprint=<impressão digital do certificado>
```

Outra opção é assinar a DLL já pronta:

```powershell
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a DetalhaBIM.dll
```

Certificados autoassinados não servem: o Windows só aceita certificados de autoridades do programa de
raízes confiáveis da Microsoft.

### Compilar a partir do código

Você precisa do Windows com o [.NET 10 SDK](https://dotnet.microsoft.com/download) e do Revit 2027.

```powershell
git clone https://github.com/josefanemacedo18/pluginrevit2027.git
cd pluginrevit2027
dotnet build -c Release     # gera src\DetalhaBIM\bin\Release\net10.0-windows\Pacote
```

Em **Debug** (`dotnet build`), o pacote é instalado automaticamente em
`%AppData%\Autodesk\Revit\Addins\2027`. Para desativar, use `-p:DeployToRevit=false`. Os scripts
`ferramentas/Instalar.ps1` e `ferramentas/Desinstalar.ps1` fazem a mesma cópia e servem para quem compila o código.

---

## Fluxo de trabalho sugerido

1. **Configurações** — defina o tipo de cota, os modelos de vista, as escalas, o carimbo e a lista de plantas técnicas.
   Depois exporte o arquivo `.json` e compartilhe com a equipe.
2. **Editor de Níveis** e **Eixos Automáticos** — organize a base do projeto.
3. Modele as paredes e os ambientes. Em seguida, rode **Limpar Ambientes**.
4. **Renumerar Elementos** — atribua os códigos das esquadrias (P01, J01…) e os números dos ambientes.
5. **Plantas Técnicas** — crie todas as plantas de todos os níveis, já etiquetadas e cotadas.
6. **Cotas por Parede**, **Cotas por Ambiente** e **Níveis de Piso** — faça os ajustes finos das cotas.
7. **Vistas por Ambiente** — detalhe as áreas molhadas, a cozinha etc., com uma prancha por ambiente.
8. **Vistas de Esquadrias** e **Quadros e Tabelas** — monte o caderno de esquadrias e os quadros de áreas.
9. **Criar Pranchas** — coloque em prancha o que ainda faltar.

---

## Para desenvolvedores

- **Plataforma:** Revit 2027 roda em **.NET 10**. O projeto usa `net10.0-windows` com WPF e é compilado contra
  os assemblies de referência `Nice3point.Revit.Api.RevitAPI/RevitAPIUI 2027.*`, que não são copiados para a saída.
- **Compila em Linux/macOS** (`EnableWindowsTargeting`), o que permite rodar CI em qualquer runner.
- **Sem dependências externas:** as interfaces são montadas em código WPF com tema próprio, e os ícones vetoriais
  são desenhados em tempo de execução. O pacote publicado tem só `DetalhaBIM.addin` e `DetalhaBIM.dll`.

```
INSTALAR/                   Os 2 arquivos prontos para copiar em %AppData%\Autodesk\Revit\Addins\2027
src/DetalhaBIM/
├── App.cs                  Ponto de entrada (IExternalApplication)
├── Ribbon/                 Aba, painéis, ícones vetoriais, disponibilidade e catálogo das ferramentas
├── Core/                   Unidades, transações, configurações, geometria, faces cotáveis, construtor de cotas
├── Services/               Lógica reutilizável: cotas de ambiente e de fachada, vistas, elevações, etiquetas, pranchas
├── Commands/               Um comando por ferramenta (Cotas, Vistas, Documentacao, Organizacao, Modelagem, Geral)
└── UI/                     Janelas WPF (diálogo de opções genérico, editor de níveis, limpeza de ambientes, configurações, ajuda)
tests/
├── Verificador/            Simula o carregamento pelo Revit (manifesto, caminho da DLL, classes, versão da API, impressão digital)
└── TesteInterface/         Executa no Windows os ícones, o tema e todas as janelas
ferramentas/                Scripts opcionais de instalação para quem compila o código
docs/                       Manual e guia de instalação
```

Para atualizar a pasta `INSTALAR` depois de alterar o código:

```powershell
$h = dotnet run --project tests/Verificador -- --hash-fontes src/DetalhaBIM
dotnet build src/DetalhaBIM/DetalhaBIM.csproj -c Release -p:ContinuousIntegrationBuild=true -p:SourceHash=$h
copy src\DetalhaBIM\bin\Release\net10.0-windows\Pacote\* INSTALAR\
dotnet run --project tests/Verificador -- --pasta INSTALAR --fontes src/DetalhaBIM
```

A compilação automática recusa uma pasta `INSTALAR` que não corresponda ao código atual.

Destaques da implementação:

- **Motor de cotas** (`Core/DimensionBuilder.cs`): elimina referências coincidentes. Se o Revit recusar alguma
  referência, ele refaz a cadeia descartando só a referência inválida, e não o lote inteiro.
- **Faces cotáveis** (`Core/FaceFinder.cs`): encontra as faces verticais atravessadas por uma linha em planta.
  É isso que permite "clicar em uma parede e o plugin encontrar o resto".
- **Transações** (`Core/Tx.cs`): descartam avisos automaticamente e agrupam tudo em um único *Desfazer* por comando.
- **Logs:** ficam em `%AppData%\DetalhaBIM\logs`, e as configurações em `%AppData%\DetalhaBIM\configuracoes.json`.

## Limitações conhecidas

- As cotas automáticas dependem de **ambientes delimitados** e de **paredes retas**. Paredes curvas e paredes de
  modelos vinculados são ignoradas, e o relatório final avisa quando isso acontece.
- As cotas das **Vistas de Esquadrias** usam os planos de referência *Esquerda/Direita/Superior/Inferior*
  das famílias. Famílias sem esses planos ficam sem cota, e o relatório final avisa.
- O desenho final (estilo das cotas, visibilidade e grafismo) vem dos **tipos e modelos de vista** do seu
  template. Configure-os em **Configurações** para ter o resultado padrão do escritório.

---

*DetalhaBIM é um projeto independente e não tem vínculo com a RR Engenharia, com o DetBox ou com a Autodesk.
Revit é marca registrada da Autodesk, Inc.*
