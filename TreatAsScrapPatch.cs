using System;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts.Skill;

namespace Klehrik_GunUpgrade
{
    [HarmonyPatch(typeof(Tinkering_Disassemble), nameof(Tinkering_Disassemble.ToggleKey))]
    public class TinkeringDisassemblePatch
    {
        static void Postfix(ref string __result, ref GameObject obj)
        {
            if (obj.HasPart<XRL.World.Parts.Klehrik_GunUpgrade>())
            {
                var part = obj.GetPart<XRL.World.Parts.Klehrik_GunUpgrade>();
                if (part.Level > 0)
                {
                    __result += "/GU+" + part.Level;
                }
            }
        }
    }
}