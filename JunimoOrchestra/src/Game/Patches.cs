using System;
using HarmonyLib;
using JunimoOrchestra.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace JunimoOrchestra.Game
{
    /// <summary>Harmony hooks into vanilla objects. Kept tiny: each one hands off to <see cref="ModEntry"/>.</summary>
    internal static class Patches
    {
        private static ModEntry mod = null!;

        public static void Apply(ModEntry entry)
        {
            mod = entry;
            var harmony = new Harmony(entry.ModManifest.UniqueID);
            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.checkForAction)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(CheckForAction_Prefix)));
            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Draw_Prefix)));
            harmony.Patch(
                AccessTools.Method(typeof(Item), nameof(Item.canStackWith)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(CanStackWith_Postfix)));
            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.performToolAction)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(PerformToolAction_Prefix)));
            harmony.Patch(
                AccessTools.Method(typeof(Farmer), nameof(Farmer.getMovementSpeed)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(GetMovementSpeed_Postfix)));
            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.placementAction)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(PlacementAction_Prefix)));
        }

        /// <summary>A Junimo item is placed as a block (<see cref="Families.JunimoStart"/>) with its look; the stack it came
        /// from goes down by one as for any placed item.</summary>
        private static bool PlacementAction_Prefix(SObject __instance, GameLocation location, int x, int y, Farmer who, ref bool __result)
        {
            try
            {
                if (__instance.bigCraftable.Value || JunimoKinds.FromItemId(__instance.ItemId) is not JunimoKind kind)
                    return true;
                Family family = Families.JunimoStart;
                var block = ItemRegistry.Create<SObject>(family.QualifiedItemId);
                BlockSettings settings = BlockSettings.CreateDefault(family);
                settings.Look = kind.Look;
                settings.Write(block);
                __result = block.placementAction(location, x, y, who);
                return false;
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"placementAction failed: {ex}", LogLevel.Error);
                return true;
            }
        }

        /// <summary>The player's own speed setting (<see cref="MoveSpeed"/>), outside cutscenes.</summary>
        private static void GetMovementSpeed_Postfix(Farmer __instance, ref float __result)
        {
            if (MoveSpeed.Percent != 100 && __instance.IsLocalPlayer && !Game1.eventUp)
                __result *= MoveSpeed.Percent / 100f;
        }

        /// <summary>The block family for a placed/held object, or null if it isn't one of ours.</summary>
        public static Family? BlockFamily(Item? item)
            => item is SObject obj && !obj.bigCraftable.Value && item.TypeDefinitionId == "(O)" ? Families.FromItemId(item.ItemId) : null;

        private static bool CheckForAction_Prefix(SObject __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            try
            {
                Family? family = BlockFamily(__instance);
                if (family == null || __instance.isTemporarilyInvisible)
                    return true;
                if (!justCheckingForActivity)
                    mod.OnBlockActivated(__instance, family, who);
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"checkForAction failed: {ex}", LogLevel.Error);
                return true;
            }
        }

        private static bool Draw_Prefix(SObject __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
        {
            Family? family = BlockFamily(__instance);
            if (family == null)
                return true;
            if (__instance.isTemporarilyInvisible)
                return false;
            if (Game1.eventUp && Game1.CurrentEvent?.isTileWalkedOn(x, y) == true)
                return false;
            try
            {
                mod.Visuals.Draw(__instance, family, spriteBatch, x, y, alpha);
                return false;
            }
            catch (Exception ex)
            {
                mod.Monitor.LogOnce($"Block draw failed, falling back to vanilla: {ex}", LogLevel.Error);
                return true;
            }
        }

        /// <summary>
        /// Knocking a block loose with a pickaxe/axe: vanilla drops a brand-new item by ID, which would forget the
        /// tune. Drop a copy of this block instead (getOne keeps modData) and tell the tool nothing else is needed.
        /// </summary>
        private static bool PerformToolAction_Prefix(SObject __instance, Tool t, ref bool __result)
        {
            try
            {
                if (BlockFamily(__instance) == null || t == null || t is MeleeWeapon || !t.isHeavyHitter() || __instance.isTemporarilyInvisible)
                    return true;
                GameLocation? location = __instance.Location;
                if (location == null)
                    return true;
                Vector2 tile = __instance.TileLocation;
                location.playSound("hammer", tile);
                location.debris.Add(new Debris(__instance.getOne(), tile * 64f + new Vector2(32f, 32f)));
                __instance.performRemoveAction();
                location.objects.Remove(tile);
                mod.Player.StopBlock(location, tile);
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"performToolAction failed: {ex}", LogLevel.Error);
                return true;
            }
        }

        /// <summary>Blocks tuned differently shouldn't merge into one stack (picked-up blocks keep their tune).</summary>
        private static void CanStackWith_Postfix(Item __instance, ISalable other, ref bool __result)
        {
            if (!__result || other is not Item item || BlockFamily(__instance) == null)
                return;
            __instance.modData.TryGetValue(BlockSettings.ModDataKey, out string? a);
            item.modData.TryGetValue(BlockSettings.ModDataKey, out string? b);
            __instance.modData.TryGetValue(Songs.SongLibrary.ModDataKey, out string? songA);
            item.modData.TryGetValue(Songs.SongLibrary.ModDataKey, out string? songB);
            if (a != b || songA != songB)
                __result = false;
        }
    }
}
