using System.Collections.Generic;
using System.Linq;
using JunimoOrchestra.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;

namespace JunimoOrchestra.Game
{
    /// <summary>Adds the instrument blocks to the game's item and recipe data, and serves our pictures as game assets,
    /// so a Content Patcher pack can edit them as well as a content pack for this mod replace them (see
    /// <see cref="CustomPacks"/>).</summary>
    internal sealed class ContentHooks
    {
        public const string BlocksTexture = "Mods/link1412.JunimoOrchestra/Blocks";
        public const string FxTexture = "Mods/link1412.JunimoOrchestra/Fx";
        public const string UiTexture = "Mods/link1412.JunimoOrchestra/UI";
        public const string ItemsTexture = "Mods/link1412.JunimoOrchestra/Items";
        public const string ClassicalTexture = "Mods/link1412.JunimoOrchestra/Classical";
        public const string BandTexture = "Mods/link1412.JunimoOrchestra/Band";
        public const string OrchestraTexture = "Mods/link1412.JunimoOrchestra/Orchestra";

        /// <summary>Each picture's asset and its file in assets/textures (and a content pack's textures).</summary>
        private static readonly (string Asset, string File)[] Textures =
        {
            (BlocksTexture, "blocks.png"), (FxTexture, "fx.png"), (UiTexture, "ui.png"), (ItemsTexture, "items.png"),
            (ClassicalTexture, "junimo_classical.png"), (BandTexture, "junimo_blocks.png"), (OrchestraTexture, "orchestra.png"),
        };

        /// <summary>Text for game text tokens, e.g. a sign's "[LocalizedText Mods\link1412.JunimoOrchestra\Strings:sign.elise]":
        /// the i18n keys starting "sign.", in the player's language.</summary>
        public const string StringsAsset = "Mods/link1412.JunimoOrchestra/Strings";

        private readonly IModHelper helper;
        private readonly CustomPacks packs;

        public ContentHooks(IModHelper helper, CustomPacks packs)
        {
            this.helper = helper;
            this.packs = packs;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.Content.LocaleChanged += (_, _) =>
            {
                helper.GameContent.InvalidateCache("Data/Objects");
                helper.GameContent.InvalidateCache("Data/CraftingRecipes");
                helper.GameContent.InvalidateCache(StringsAsset);
            };
        }

        public string FamilyName(Family family) => this.helper.Translation.Get($"family.{family.Key}.name");

        public string ItemName(Family family) => this.helper.Translation.Get("item.name", new { family = this.FamilyName(family) });

        public string ItemName(JunimoKind kind) => this.helper.Translation.Get($"item.junimo.{kind.Key}.name");

        /// <summary>What a placed block is called: a Junimo item's name if it was placed as one.</summary>
        public string ItemName(Family family, string? look)
            => JunimoKinds.All.FirstOrDefault(k => k.Look == look) is JunimoKind kind ? this.ItemName(kind) : this.ItemName(family);

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            foreach ((string asset, string file) in Textures)
            {
                // low: a Content Patcher "Load" of the whole picture wins over ours
                if (e.NameWithoutLocale.IsEquivalentTo(asset))
                {
                    e.LoadFrom(() => this.packs.Texture(file), AssetLoadPriority.Low);
                    return;
                }
            }
            if (e.NameWithoutLocale.IsEquivalentTo(StringsAsset))
            {
                e.LoadFrom(() => this.helper.Translation.GetTranslations()
                    .Where(t => t.Key.StartsWith("sign.", System.StringComparison.Ordinal))
                    .ToDictionary(t => t.Key, t => t.ToString()), AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    IDictionary<string, ObjectData> data = asset.AsDictionary<string, ObjectData>().Data;
                    ObjectData Item(string id, string name, string description, string texture, int sprite) => new()
                    {
                        Name = id,
                        DisplayName = name,
                        Description = description,
                        Type = "Crafting",
                        Category = 0,
                        Price = 0,
                        Texture = texture,
                        SpriteIndex = sprite,
                        Edibility = -300,
                        CanBeGivenAsGift = false,
                        ExcludeFromShippingCollection = true,
                        ExcludeFromRandomSale = true,
                        ExcludeFromFishingCollection = true,
                        ContextTags = new List<string> { "junimo_orchestra_block", "not_giftable" }
                    };
                    foreach (Family family in Families.All)
                        data[family.ItemId] = Item(family.ItemId, this.ItemName(family), this.helper.Translation.Get("item.desc"), BlocksTexture, family.Index);
                    foreach (JunimoKind kind in JunimoKinds.All)
                        data[kind.ItemId] = Item(kind.ItemId, this.ItemName(kind), this.helper.Translation.Get($"item.junimo.{kind.Key}.desc"), ItemsTexture, kind.Index);
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
                    // ingredients/unused/yield/big craftable/unlock (ours, see ModEntry.LearnRecipes)/display name
                    foreach (Family family in Families.All)
                        data[family.ItemId] = $"{Families.BlockRecipe}/Home/{family.ItemId} {Families.CraftYield}/false/null/{this.ItemName(family)}";
                    foreach (JunimoKind kind in JunimoKinds.All)   // free: the Junimos' thanks
                        data[kind.ItemId] = $"/Home/{kind.ItemId} {Families.CraftYield}/false/null/{this.ItemName(kind)}";
                });
            }
        }
    }
}
