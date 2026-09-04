#nullable disable
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MoreAccessorySlots
{
    internal static class LocalizationManager
    {
        // Название мода (используется в имени ресурса, пути UserData и логах).
        // The name of the mod (used in the resource name, UserData path, and logs).
        private const string namespace_name = "MoreAccessorySlots";

        // Сокращённое название мода (префикс ключей локализации, например "RGM.").
        // The abbreviated name of the mod (prefix of localization keys, for example, “CCM.”
        private const string locale_name = "MASM.";

        private static Dictionary<string, Dictionary<string, string>> _data;

        internal static Dictionary<string, Dictionary<string, string>> Data
        {
            get { return _data ?? (_data = Load()); }
        }

        internal static void Reload() => _data = null;

        // Имя встроенного ресурса: {RootNamespace}.{filename}
        // Name embedded resource: {RootNamespace}.{filename}
        private const string EmbeddedResourceName = namespace_name + ".localization.json";

        private static Dictionary<string, Dictionary<string, string>> Load()
        {

            string dllDir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

            string userPath = Path.Combine( Path.GetDirectoryName(dllDir) ?? dllDir,   // выходим из Mods/ в корень игры
                "UserData", namespace_name, "localization.json");

            if (File.Exists(userPath))
            {
                var fromFile = TryLoadJson(File.ReadAllText(userPath, Encoding.UTF8));
                if (fromFile != null)
                {
                    MelonLogger.Msg($"[{namespace_name}] Localization override loaded from: {userPath}");
                    return fromFile;
                }
            }


            var asm    = Assembly.GetExecutingAssembly();
            var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
            if (stream != null)
            {
                using var reader  = new StreamReader(stream, Encoding.UTF8);
                var fromEmbedded  = TryLoadJson(reader.ReadToEnd());
                if (fromEmbedded != null)
                {
                    MelonLogger.Msg($"[{namespace_name}] Localization loaded from embedded resource.");
                    return fromEmbedded;
                }
            }
            else
            {
                MelonLogger.Warning($"[{namespace_name}] Embedded resource '{EmbeddedResourceName}' not found — using built-in English strings.");
            }

            return FallbackEnglish;
        }

        private static Dictionary<string, Dictionary<string, string>> TryLoadJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{namespace_name}] Failed to parse localization JSON: {ex.Message}");
                return null;
            }
        }

        // Minimal English fallback so the mod works even without the JSON file.
        private static readonly Dictionary<string, Dictionary<string, string>> FallbackEnglish = new()
        {
            ["English"] = new()
            {

                    //    [Section("MAS.AccessorySlots.Section", Localize = true)]
                    //      [Name("MAS.AccessorySlots.Name", Localize = true)]
                    //      [ModSettings.Description("MASM.AccessorySlots.Decription", Localize = true)]

                [locale_name + "AccessorySlots.Section"] = "Accessory Slots",
                [locale_name + "AccessorySlots.Name"] = "Extra Accessory Slots",
                [locale_name + "AccessorySlots.Decription"] = "How many extra accessory slots to add beyond the vanilla 2 (Base, Mid). 0 = vanilla only, 1 = +1 (Top), 2 = +2 (Top, Top2). Default: 2. Requires restarting the game (or reloading the save) to take effect."


            }
        };

        internal static string Get(string key)
        {
            string lang = Localization.Language ?? "English";
            var data = Data;
            if (data.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out string val))
                return val;
            if (data.TryGetValue("English", out var en) && en.TryGetValue(key, out string enVal))
                return enVal;
            return key;
        }

        internal static bool IsLocaleKey(string key) => key != null && key.StartsWith(locale_name);
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.Get))]
    internal static class LocalizationPatch
    {
        static void Postfix(string __0, ref string __result)
        {
            if (!LocalizationManager.IsLocaleKey(__0))
                return;
            __result = LocalizationManager.Get(__0);
        }
    }

    /// <summary>
    /// Патч на DescriptionHolder.get_Text (ModSettings) —
    /// перехватывает чтение описания в момент показа, когда язык уже установлен.
    /// </summary>
    [HarmonyPatch]
    internal static class DescriptionTextTranslatePatch
    {
        static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.PropertyGetter(
                AccessTools.TypeByName("ModSettings.DescriptionHolder"), "Text");

        static void Postfix(ref string __result)
        {
            if (!LocalizationManager.IsLocaleKey(__result))
                return;
            __result = LocalizationManager.Get(__result);
        }
    }
}