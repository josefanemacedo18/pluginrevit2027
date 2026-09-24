using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DetalhaBIM.Verificador
{
    /// <summary>
    /// Simula o carregamento do add-in pelo Revit 2027 e confere tudo o que pode ser conferido
    /// sem o Revit. Termina com código 1 se qualquer verificação falhar.
    /// </summary>
    internal static class Program
    {
        private static int _falhas;
        private static int _ok;

        private static int Main(string[] args)
        {
            var opt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i + 1 < args.Length; i += 2) opt[args[i].TrimStart('-')] = args[i + 1];

            if (opt.TryGetValue("hash-fontes", out string fontesHash))
            {
                Console.WriteLine(HashFontes(fontesHash));
                return 0;
            }
            if (!opt.TryGetValue("pasta", out string pastaArg))
            {
                Console.WriteLine("Uso: Verificador --pasta <pasta com DetalhaBIM.addin> [--versao 1.2.3] [--catalogo ToolCatalog.cs] [--fontes src/DetalhaBIM]");
                Console.WriteLine("     Verificador --hash-fontes src/DetalhaBIM");
                return 2;
            }

            string pasta = Path.GetFullPath(pastaArg);
            opt.TryGetValue("versao", out string versao);
            opt.TryGetValue("catalogo", out string catalogo);
            opt.TryGetValue("fontes", out string fontes);
            Console.WriteLine($"=== Verificando instalação em: {pasta}\n");

            // 1) Arquivos da pasta (o que o usuário copia para Addins\2027)
            string addinPath = Path.Combine(pasta, "DetalhaBIM.addin");
            Check(File.Exists(addinPath), "DetalhaBIM.addin existe na pasta");
            if (!File.Exists(addinPath)) return Fim();

            string[] arquivos = Directory.GetFiles(pasta).Select(Path.GetFileName).OrderBy(n => n).ToArray();
            Console.WriteLine("   Arquivos: " + string.Join(", ", arquivos));
            Check(!arquivos.Any(a => a.StartsWith("RevitAPI", StringComparison.OrdinalIgnoreCase)),
                "Nenhuma cópia de RevitAPI*.dll junto do plugin (causaria conflito)");

            // 2) Manifesto .addin
            XDocument xml;
            try
            {
                xml = XDocument.Load(addinPath);
            }
            catch (Exception ex)
            {
                Check(false, "DetalhaBIM.addin é um XML válido: " + ex.Message);
                return Fim();
            }
            Check(true, "DetalhaBIM.addin é um XML válido");
            XElement root = xml.Root;
            Check(root?.Name.LocalName == "RevitAddIns", "Raiz do manifesto é <RevitAddIns>");
            List<XElement> addins = root?.Elements("AddIn").ToList() ?? new List<XElement>();
            Check(addins.Count == 1, $"Manifesto declara exatamente 1 <AddIn> (encontrados: {addins.Count})");
            if (addins.Count != 1) return Fim();
            XElement addin = addins[0];
            string Val(string n) => addin.Element(n)?.Value?.Trim();

            Check((string)addin.Attribute("Type") == "Application", "Tipo do add-in é Application");
            Check(!string.IsNullOrEmpty(Val("Name")), "Nome informado: " + Val("Name"));
            Check(Guid.TryParse(Val("AddInId") ?? Val("ClientId"), out _), "AddInId é um GUID válido: " + (Val("AddInId") ?? Val("ClientId")));
            Check(!string.IsNullOrEmpty(Val("VendorId")), "VendorId informado: " + Val("VendorId"));
            string fullClassName = Val("FullClassName");
            Check(!string.IsNullOrEmpty(fullClassName), "FullClassName informado: " + fullClassName);
            string assemblyValue = Val("Assembly");
            Check(!string.IsNullOrEmpty(assemblyValue), "Assembly informado: " + assemblyValue);
            if (string.IsNullOrEmpty(assemblyValue)) return Fim();

            // 3) Resolução do caminho da DLL exatamente como o Revit: relativo à pasta do .addin
            string dllPath = Path.IsPathRooted(assemblyValue)
                ? assemblyValue
                : Path.GetFullPath(Path.Combine(pasta, assemblyValue.Replace('\\', Path.DirectorySeparatorChar)));
            Check(File.Exists(dllPath), $"A DLL indicada no manifesto EXISTE: {dllPath}");
            if (!File.Exists(dllPath)) return Fim();
            Check(Path.GetDirectoryName(dllPath) == pasta, "A DLL fica na mesma pasta do .addin (sem subpasta)");
            Check(new FileInfo(dllPath).Length > 50_000, $"A DLL tem tamanho plausível ({new FileInfo(dllPath).Length:N0} bytes)");

            // 4) Cabeçalho PE e referências
            using (var fs = File.OpenRead(dllPath))
            using (var pe = new PEReader(fs))
            {
                Check(pe.HasMetadata, "A DLL é um assembly .NET");
                Machine m = pe.PEHeaders.CoffHeader.Machine;
                bool ilOnly = (pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != 0;
                Check(m == Machine.Amd64 || (m == Machine.I386 && ilOnly && (pe.PEHeaders.CorHeader.Flags & CorFlags.Requires32Bit) == 0),
                    $"Compatível com Revit 64 bits (Machine={m}, ILOnly={ilOnly})");

                MetadataReader md = pe.GetMetadataReader();
                AssemblyDefinition def = md.GetAssemblyDefinition();
                Console.WriteLine($"   Assembly: {md.GetString(def.Name)} {def.Version}");
                if (versao != null)
                    Check(def.Version.ToString(3) == versao, $"Versão da DLL = {versao} (encontrada {def.Version.ToString(3)})");

                if (fontes != null)
                {
                    string esperado = HashFontes(fontes);
                    string gravado = def.GetCustomAttributes().Select(h => md.GetCustomAttribute(h))
                        .Where(a => AttrName(md, a) == "System.Reflection.AssemblyMetadataAttribute")
                        .Select(a => a.DecodeValue(new StringDecoder()))
                        .Where(v => (string)v.FixedArguments[0].Value == "SourceHash")
                        .Select(v => (string)v.FixedArguments[1].Value).FirstOrDefault();
                    Check(gravado == esperado,
                        $"A DLL foi compilada exatamente do código-fonte atual (impressão digital {Short(gravado)} = {Short(esperado)})");
                }

                bool isRef = def.GetCustomAttributes().Select(h => AttrName(md, md.GetCustomAttribute(h)))
                    .Any(n => n == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute");
                Check(!isRef, "A DLL é de implementação (não é assembly de referência)");

                string tfm = def.GetCustomAttributes().Select(h => md.GetCustomAttribute(h))
                    .Where(a => AttrName(md, a) == "System.Runtime.Versioning.TargetFrameworkAttribute")
                    .Select(a => System.Text.Encoding.UTF8.GetString(md.GetBlobBytes(a.Value)))
                    .FirstOrDefault() ?? "";
                Check(tfm.Contains(".NETCoreApp,Version=v10.0"), "Compilada para .NET 10 (runtime do Revit 2027)");

                var framework = new[] { "System", "Microsoft", "netstandard", "mscorlib", "PresentationCore", "PresentationFramework", "WindowsBase", "System.Xaml", "UIAutomation" };
                foreach (AssemblyReferenceHandle h in md.AssemblyReferences)
                {
                    AssemblyReference r = md.GetAssemblyReference(h);
                    string n = md.GetString(r.Name);
                    if (n == "RevitAPI" || n == "RevitAPIUI")
                    {
                        Check(r.Version.Major == 27 && r.Version.Minor == 0 && r.Version.Build == 0 && r.Version.Revision == 0,
                            $"{n} referenciado na versão base {r.Version} (carrega em qualquer atualização do Revit 2027)");
                    }
                    else if (!framework.Any(f => n == f || n.StartsWith(f + ".")))
                    {
                        Check(File.Exists(Path.Combine(pasta, n + ".dll")), $"Dependência {n} presente ao lado da DLL");
                    }
                }
            }

            // 5) Tipos, como o Revit os procura (sem executar código)
            string refs = Path.Combine(AppContext.BaseDirectory, "refs");
            var paths = new List<string>(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
            var nomes = new HashSet<string>(paths.Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);
            foreach (string p in Directory.GetFiles(refs, "*.dll"))
                if (nomes.Add(Path.GetFileNameWithoutExtension(p))) paths.Add(p);
            paths.Add(dllPath);

            using (var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths)))
            {
                Assembly asm = mlc.LoadFromAssemblyPath(dllPath);
                Type app = asm.GetType(fullClassName);
                Check(app != null, $"Classe {fullClassName} existe na DLL");
                if (app != null)
                {
                    Check(app.IsPublic && app.IsClass && !app.IsAbstract, $"{fullClassName} é pública e concreta");
                    Check(app.GetConstructor(Type.EmptyTypes) != null, $"{fullClassName} tem construtor público sem parâmetros");
                    Check(Implements(app, "Autodesk.Revit.UI.IExternalApplication"), $"{fullClassName} implementa IExternalApplication");
                }

                Type[] tipos = asm.GetTypes();
                List<Type> comandos = tipos.Where(t => t.IsClass && !t.IsAbstract && Implements(t, "Autodesk.Revit.UI.IExternalCommand")).ToList();
                Console.WriteLine($"   Comandos encontrados: {comandos.Count}");
                foreach (Type c in comandos)
                {
                    CustomAttributeData tx = c.GetCustomAttributesData()
                        .FirstOrDefault(a => a.AttributeType.FullName == "Autodesk.Revit.Attributes.TransactionAttribute");
                    bool manual = tx != null && tx.ConstructorArguments.Count == 1 && Convert.ToInt32(tx.ConstructorArguments[0].Value) == 1;
                    Check(c.IsPublic && c.GetConstructor(Type.EmptyTypes) != null && manual,
                        $"Comando {c.Name}: público, construtor vazio e [Transaction(Manual)] na própria classe");
                }

                List<Type> disponibilidade = tipos.Where(t => t.IsClass && !t.IsAbstract && Implements(t, "Autodesk.Revit.UI.IExternalCommandAvailability")).ToList();
                foreach (Type a in disponibilidade)
                    Check(a.IsPublic && a.GetConstructor(Type.EmptyTypes) != null, $"Disponibilidade {a.Name}: pública com construtor vazio");

                // 6) Cada botão da faixa de opções aponta para um comando válido
                if (catalogo != null && File.Exists(catalogo))
                {
                    string src = File.ReadAllText(catalogo);
                    var cmdNames = Regex.Matches(src, @"Command\s*=\s*typeof\((\w+)\)").Select(x => x.Groups[1].Value).ToList();
                    var avNames = Regex.Matches(src, @"Availability\s*=\s*typeof\((\w+)\)").Select(x => x.Groups[1].Value).Distinct().ToList();
                    Check(cmdNames.Count > 0, $"Catálogo da faixa de opções lido: {cmdNames.Count} botões");
                    Check(cmdNames.Count == cmdNames.Distinct().Count(), "Nenhum botão repetido no catálogo");
                    foreach (string n in cmdNames)
                        Check(comandos.Any(c => c.Name == n), $"Botão → comando {n} existe e é válido");
                    foreach (string n in avNames)
                        Check(disponibilidade.Any(a => a.Name == n), $"Disponibilidade {n} existe e é válida");
                    Check(comandos.Count == cmdNames.Count, $"Todo comando da DLL tem um botão ({comandos.Count} comandos, {cmdNames.Count} botões)");
                }
            }

            return Fim();
        }

        /// <summary>
        /// Impressão digital do código-fonte: SHA-256 de todos os .cs, do .csproj e do .addin, com
        /// quebras de linha normalizadas (igual em Windows e Linux).
        /// </summary>
        private static string HashFontes(string dir)
        {
            string root = Path.GetFullPath(dir);
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".csproj") || f.EndsWith(".addin"))
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Where(r => !r.StartsWith("bin/") && !r.StartsWith("obj/"))
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();
            using var sha = System.Security.Cryptography.SHA256.Create();
            var sb = new System.Text.StringBuilder();
            foreach (string r in files)
            {
                string text = File.ReadAllText(Path.Combine(root, r)).Replace("\r", string.Empty).TrimStart('\uFEFF');
                sb.Append(r).Append('\n').Append(text).Append('\n');
            }
            return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
        }

        private static string Short(string h) => string.IsNullOrEmpty(h) ? "(ausente)" : h.Substring(0, 12);

        /// <summary>Decodificador mínimo para ler atributos com argumentos string.</summary>
        private sealed class StringDecoder : ICustomAttributeTypeProvider<object>
        {
            public object GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode;
            public object GetSystemType() => typeof(Type);
            public object GetSZArrayType(object elementType) => elementType;
            public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromSerializedName(string name) => name;
            public PrimitiveTypeCode GetUnderlyingEnumType(object type) => PrimitiveTypeCode.Int32;
            public bool IsSystemType(object type) => type is Type;
        }

        private static bool Implements(Type t, string iface)
        {
            try
            {
                return t.GetInterfaces().Any(i => i.FullName == iface);
            }
            catch
            {
                return false;
            }
        }

        private static string AttrName(MetadataReader md, CustomAttribute a)
        {
            EntityHandle ctor = a.Constructor;
            EntityHandle typeHandle = ctor.Kind == HandleKind.MemberReference
                ? md.GetMemberReference((MemberReferenceHandle)ctor).Parent
                : md.GetMethodDefinition((MethodDefinitionHandle)ctor).GetDeclaringType();
            if (typeHandle.Kind == HandleKind.TypeReference)
            {
                TypeReference tr = md.GetTypeReference((TypeReferenceHandle)typeHandle);
                return md.GetString(tr.Namespace) + "." + md.GetString(tr.Name);
            }
            if (typeHandle.Kind == HandleKind.TypeDefinition)
            {
                TypeDefinition td = md.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                return md.GetString(td.Namespace) + "." + md.GetString(td.Name);
            }
            return "";
        }

        private static void Check(bool ok, string descricao)
        {
            if (ok) _ok++;
            else _falhas++;
            Console.WriteLine((ok ? "  [OK]    " : "  [FALHA] ") + descricao);
        }

        private static int Fim()
        {
            Console.WriteLine($"\n=== Resultado: {_ok} verificações OK, {_falhas} falha(s).");
            Console.WriteLine(_falhas == 0 ? "=== INSTALAÇÃO VÁLIDA" : "=== INSTALAÇÃO COM PROBLEMAS");
            return _falhas == 0 ? 0 : 1;
        }
    }
}
