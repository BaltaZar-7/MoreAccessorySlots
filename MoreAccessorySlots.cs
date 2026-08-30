#nullable disable
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace MoreAccessorySlots
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("MoreAccessorySlots Mod initialized!");
            MoreAccessorySlotsSettings.OnLoad();
        }

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.GetNumLayersForClothingRegion))]
        internal static class Patch_GetNumLayersForClothingRegion
        {
            private static void Postfix(ClothingRegion clothingRegion, ref int __result)
            {
                if (clothingRegion == ClothingRegion.Accessory)
                {
                    int extra = GetExtraSlotCount();
                    __result = 2 + extra;
                }
            }
        }

        [HarmonyPatch(typeof(Panel_Clothing), nameof(Panel_Clothing.Enable))]
        internal static class Patch_PanelClothing_Enable
        {
            private static bool s_ExtraSlotsCreated = false;
            private static void Prefix(Panel_Clothing __instance, bool enable)
            {
                if (!enable || s_ExtraSlotsCreated)
                {
                    return;
                }
                AccessorySlotExpander.SetupAccessorySlots(__instance);
                s_ExtraSlotsCreated = true;
            }
        }

        [HarmonyPatch(typeof(ClothingItem), "Awake")]
        internal static class Patch_ClothingItem_Awake_WidenAccessoryMaxLayer
        {
            private static void Postfix(ClothingItem __instance)
            {
                if (__instance.m_Region != ClothingRegion.Accessory) return;

                int extra = GetExtraSlotCount();
                if (extra <= 0) return;

                ClothingLayer targetMax = (ClothingLayer)(1 + extra); /// Mid(1)+1=Top(2), +2=Top2(3)
                if (__instance.m_MaxLayer < targetMax)
                {
                    __instance.m_MaxLayer = targetMax;
                }
            }
        }

        [HarmonyPatch(typeof(ClothingSlot), nameof(ClothingSlot.UpdateMaskingFromLayeredClothing))]
        internal static class Patch_ClothingSlot_SkipAccessoryMasking
        {
            private static bool Prefix(ClothingSlot __instance)
            {
                /// In Accessory, layer masking (which is intended to make, for example, a coat
                /// cover the sweater underneath it) is misleadingly "mixed" with
                /// separate accessories if there is another item on a layer higher than
                /// its own layer—which is why we never run it here.
                if (__instance.GetClothingRegion() == ClothingRegion.Accessory)
                {
                    return false;
                }
                return true;
            }
        }

        internal static int GetExtraSlotCount()
        {
            int value = MoreAccessorySlotsSettings.Instance != null ? MoreAccessorySlotsSettings.Instance.ExtraAccessorySlots : 2;
            return (value);
        }
    }

    internal static class AccessorySlotExpander
    {
        public static void SetupAccessorySlots(Panel_Clothing panel)
        {
            int extraSlots = Main.GetExtraSlotCount();
            if (extraSlots <= 0)
            {
                return; /// vanilla untouched
            }

            GenericClothingSlotSpawner slotA = null;
            GenericClothingSlotSpawner slotB = null;
            foreach (GenericClothingSlotSpawner spawner in panel.GetComponentsInChildren<GenericClothingSlotSpawner>(true))
            {
                if (spawner.m_ClothingRegion != ClothingRegion.Accessory) continue;
                if (slotA == null) slotA = spawner;
                else if (slotB == null) slotB = spawner;
            }
            if (slotA == null || slotB == null)
            {
                MelonLogger.Warning("[MoreAccessorySlots] Two Accessory spawners cannot be found.");
                return;
            }

            ClothingSlot slotABase = slotA.m_SpawnedObject != null ? slotA.m_SpawnedObject.GetComponent<ClothingSlot>() : null;
            UITexture[] paperDollTemplate = slotABase != null ? slotABase.m_PaperDollSlots : null;

            const float shrink = 0.87f; /// positioning, scale
            Vector3 verticalNudge = new Vector3(0f, 3f, 0f);

            int totalSlots = 2 + extraSlots;
            Vector3 originalDelta = slotB.transform.localPosition - slotA.transform.localPosition;
            Vector3 midpoint = (slotA.transform.localPosition + slotB.transform.localPosition) * 0.5f + verticalNudge;
            Vector3 step = originalDelta * shrink;

            Vector3[] positions = new Vector3[totalSlots];
            float center = (totalSlots - 1) / 2f;
            for (int i = 0; i < totalSlots; i++)
            {
                positions[i] = midpoint + step * (i - center);
            }

            slotA.transform.localScale *= shrink;
            slotA.transform.localPosition = positions[0];
            slotB.transform.localScale *= shrink;
            slotB.transform.localPosition = positions[1];

            ClothingLayer[] extraLayers = { ClothingLayer.Top, ClothingLayer.Top2 };
            for (int i = 0; i < extraSlots; i++)
            {
                CreateAccessorySlot(
                    panel, slotB, extraLayers[i], 1000 + i, positions[2 + i],
                    paperDollTemplate, extraLayers[i].ToString());
            }
        }

        private static GenericClothingSlotSpawner CreateAccessorySlot(Panel_Clothing panel, GenericClothingSlotSpawner template, ClothingLayer layer,
            int newRow, Vector3 localPosition, UITexture[] paperDollTemplate, string labelSuffix)
        {
            GameObject cloneGO = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            cloneGO.name = "GenericClothingSlotSpawner_Accessory_" + layer;
            GenericClothingSlotSpawner spawner = cloneGO.GetComponent<GenericClothingSlotSpawner>();

            if (spawner.m_SpawnedObject != null)
            {
                GameObject stale = spawner.m_SpawnedObject;
                spawner.m_SpawnedObject = null;
                UnityEngine.Object.DestroyImmediate(stale);
            }

            spawner.m_ClothingLayer = layer;
            spawner.m_LayoutRowIndex = newRow;
            cloneGO.transform.localPosition = localPosition;

            if (paperDollTemplate != null)
            {
                spawner.m_PaperDollSlots = CloneVisiblePaperDollSlots(paperDollTemplate, labelSuffix);
            }

            spawner.InstantiateObjectIfNoneExists();

            ClothingSlot newSlot = spawner.m_SpawnedObject != null
                ? spawner.m_SpawnedObject.GetComponent<ClothingSlot>()
                : null;

            if (newSlot != null)
            {
                newSlot.DoSetup(panel, spawner.m_EmptySlotSpriteName, ClothingRegion.Accessory, layer,
                                 spawner.m_LayoutColumnIndex, spawner.m_LayoutRowIndex,
                                 spawner.m_PaperDollSlots, spawner.m_LayerLocID);
            }
            else
            {
                MelonLogger.Warning($"[MoreAccessorySlots] Nem sikerült létrehozni a ClothingSlot-ot ({layer}).");
            }

            return spawner;
        }

        private static UITexture[] CloneVisiblePaperDollSlots(UITexture[] source, string labelSuffix)
        {
            if (source == null) return null;

            UITexture[] clones = new UITexture[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                {
                    clones[i] = null;
                    continue;
                }
                GameObject cloneGO = UnityEngine.Object.Instantiate(source[i].gameObject, source[i].transform.parent);
                cloneGO.name = source[i].gameObject.name + "_" + labelSuffix;
                clones[i] = cloneGO.GetComponent<UITexture>();
            }
            return clones;
        }
    }
}