using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Harmony;
using Verse;
using RimWorld;

namespace RangedStaggerRebalanced
{
    class RangedStaggerRebalancedSettings : ModSettings
    {

        public static bool useExperimentalCurve = false;

        public void DoWindowContents(Rect wrect)
        {
            Listing_Standard options = new Listing_Standard();
            Color defaultColor = GUI.color;
            options.Begin(wrect);

            GUI.color = defaultColor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            options.Gap();
            options.CheckboxLabeled("SettingUseExperimentalCurve".Translate(), ref useExperimentalCurve, "SettingUseExperimentalCurveToolTip".Translate());

            options.End();

            Mod.GetSettings<RangedStaggerRebalancedSettings>().Write();

        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useExperimentalCurve, "useExperimentalCurve", false);
        }

    }

    class RangedStaggerRebalanced: Mod
    {
        public RangedStaggerRebalancedSettings settings;

        public RangedStaggerRebalanced(ModContentPack content) : base(content)
        {
            GetSettings<RangedStaggerRebalancedSettings>();
        }

        public override string SettingsCategory() => "RangedStaggerRebalancedSettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            GetSettings<RangedStaggerRebalancedSettings>().DoWindowContents(inRect);
        }
    }
}
