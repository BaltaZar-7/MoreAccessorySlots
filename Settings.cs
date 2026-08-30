#nullable disable
using Il2CppNewtonsoft.Json.Linq;
using MelonLoader;
using ModSettings;
using System.ComponentModel;

namespace MoreAccessorySlots
{
    internal class MoreAccessorySlotsSettings : JsonModSettings
    {
        public static MoreAccessorySlotsSettings Instance { get; private set; }

        [Section("Accessory Slots")]
        [Name("Extra Accessory Slots")]
        [ModSettings.Description("How many extra accessory slots to add beyond the vanilla 2 (Base, Mid). 0 = vanilla only, 1 = +1 (Top), 2 = +2 (Top, Top2). Default: 2. Requires restarting the game (or reloading the save) to take effect.")]
        [Slider(0, 2)]
        public int ExtraAccessorySlots = 2;

        protected override void OnConfirm()
        {
            base.OnConfirm();
            Save();
            MelonLogger.Msg("[MoreAccessorySlots] Saved mod settings.");
        }

        public static void OnLoad()
        {
            Instance = new MoreAccessorySlotsSettings();
            Instance.AddToModSettings("More Accessory Slots");
        }
    }
}