using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BetterJunimos {
    public static class JunimoEditor {
        private static readonly Rectangle SpriteOverlayRect = new(0, 0, 128, 128);

        internal static void OnAssetRequested(object sender, AssetRequestedEventArgs e) {
            if (!e.NameWithoutLocale.IsEquivalentTo("Characters/Junimo")) return;

            bool useUmbrella = BetterJunimos.Config.FunChanges.JunimosAlwaysHaveLeafUmbrellas || Game1.isRaining;
            if (!useUmbrella && !Game1.IsWinter) return;

            e.Edit(asset => {
                var editor = asset.AsImage();
                if (useUmbrella) {
                    var umbrella = BetterJunimos.Config.FunChanges.MoreColorfulLeafUmbrellas ? "JunimoUmbrellaOnly_Grayscale" : "JunimoUmbrellaOnly";
                    var customTexture = BetterJunimos.SHelper.ModContent.Load<Texture2D>($"assets/{umbrella}.png");
                    editor.PatchImage(customTexture, SpriteOverlayRect, SpriteOverlayRect, PatchMode.Overlay);
                } else if (Game1.IsWinter) {
                    var customTexture = BetterJunimos.SHelper.ModContent.Load<Texture2D>("assets/JunimoBeanie.png");
                    editor.PatchImage(customTexture, SpriteOverlayRect, SpriteOverlayRect, PatchMode.Overlay);
                }
            });
        }
    }
}