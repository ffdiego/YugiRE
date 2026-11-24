using Joey.Core;

internal static class DuelArtworkPolicyChecks
{
    internal static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        Card a = new(7,120,"Example","",0);
        check(DuelArtworkPolicy.Require(a,@"card\sample.bmp",false)==@"card\sample.bmp" &&
            DuelArtworkPolicy.Require(a,@"mini\sample.bmp",true)==@"mini\sample.bmp",
            "V03: independent original artwork banks");
        reject(()=>DuelArtworkPolicy.Require(a,null,true),"V03: no full-size substitution for missing mini");
        reject(()=>DuelArtworkPolicy.Require(a,"",false),"V03: no name placeholder in faithful mode");
        DuelArtworkPolicy.ValidateMappings([a],_=>@"card\sample.bmp",_=>@"mini\sample.bmp");
        reject(()=>DuelArtworkPolicy.ValidateMappings([a],_=>@"card\sample.bmp",_=>null),
            "V03: preflight rejects incomplete playable mapping");
        check(DuelArtworkPolicy.AllowInspectionFallback(true,false,false) &&
            !DuelArtworkPolicy.AllowInspectionFallback(true,true,false) &&
            !DuelArtworkPolicy.AllowInspectionFallback(true,false,true) &&
            !DuelArtworkPolicy.AllowInspectionFallback(false,false,false),
            "V03: fallback restricted to explicit unused-record inspection");
        check(DuelArtworkPolicy.RequireBack(@"mini\card_ura.bmp",true)==@"mini\card_ura.bmp",
            "V03: preserve native mini back");
        reject(()=>DuelArtworkPolicy.RequireBack(null,true),"V03: no geometric back fallback");
    }
}
