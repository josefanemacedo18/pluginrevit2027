using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DetalhaBIM.Core
{
    /// <summary>Carrega e grava as configurações e o "último valor usado" de cada diálogo.</summary>
    public static class SettingsService
    {
        private static DetalhaSettings _current;
        private static Dictionary<string, string> _uiState;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
        };

        public static string SettingsPath => Path.Combine(Logger.AppDataFolder, "configuracoes.json");
        public static string UiStatePath => Path.Combine(Logger.AppDataFolder, "ultimos-valores.json");

        public static DetalhaSettings Current
        {
            get
            {
                if (_current == null) _current = Load(SettingsPath) ?? new DetalhaSettings();
                return _current;
            }
        }

        public static void Save(DetalhaSettings settings)
        {
            _current = settings;
            Write(SettingsPath, settings);
        }

        public static void Export(string path) => Write(path, Current);

        public static bool Import(string path)
        {
            DetalhaSettings s = Load(path);
            if (s == null) return false;
            Save(s);
            return true;
        }

        public static void Reset() => Save(new DetalhaSettings());

        public static DetalhaSettings Clone()
        {
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            return JsonSerializer.Deserialize<DetalhaSettings>(json, JsonOptions);
        }

        // ------------------------------------------------------------------ últimos valores

        public static string GetUiValue(string key)
        {
            EnsureUiState();
            return _uiState.TryGetValue(key, out string v) ? v : null;
        }

        public static void SetUiValues(IDictionary<string, string> values)
        {
            EnsureUiState();
            foreach (KeyValuePair<string, string> kv in values) _uiState[kv.Key] = kv.Value;
            try
            {
                File.WriteAllText(UiStatePath, JsonSerializer.Serialize(_uiState, JsonOptions));
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao gravar últimos valores", ex);
            }
        }

        private static void EnsureUiState()
        {
            if (_uiState != null) return;
            try
            {
                if (File.Exists(UiStatePath))
                {
                    _uiState = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(UiStatePath), JsonOptions);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao ler últimos valores", ex);
            }
            if (_uiState == null) _uiState = new Dictionary<string, string>();
        }

        // ------------------------------------------------------------------ arquivo

        private static DetalhaSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                DetalhaSettings s = JsonSerializer.Deserialize<DetalhaSettings>(File.ReadAllText(path), JsonOptions);
                if (s == null) return null;
                s.Cotas ??= new CotasSettings();
                s.Vistas ??= new VistasSettings();
                s.PlantasTecnicas ??= PlantaTecnicaDef.Padrao();
                s.Renumeracao ??= new RenumeracaoSettings();
                s.Modelagem ??= new ModelagemSettings();
                s.Pranchas ??= new PranchasSettings();
                return s;
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao ler configurações de " + path, ex);
                return null;
            }
        }

        private static void Write(string path, DetalhaSettings settings)
        {
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao gravar configurações em " + path, ex);
                throw;
            }
        }
    }
}
