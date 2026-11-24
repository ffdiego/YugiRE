using Joey.Core;

internal static class ElementalPositionRuleChecks
{
    private static Card Monster(ushort id) => new(id,id,"Monster","",(3u<<20)|(4u<<25));
    public static void Run(Action<bool,string> check)
    {
        foreach(int controller in new[]{0,1})
        foreach(int active in new[]{0,1})
        foreach(ushort id in new ushort[]{1517,1519,1518,24})
        foreach(var phase in Enum.GetValues<ElementalPositionOpportunity>())
        foreach(bool faceUp in new[]{false,true})
        foreach(bool suppressed in new[]{false,true})
        foreach(bool offered in new[]{false,true})
        foreach(bool ended in new[]{false,true})
        {
            bool expected=controller!=active&&faceUp&&!suppressed&&!offered&&!ended&&
                (id==1517&&phase==ElementalPositionOpportunity.Standby||
                 id==1519&&phase==ElementalPositionOpportunity.End);
            var intent=ElementalPositionRules.AtOpportunity(id,controller,active,phase,faceUp,suppressed,offered,ended);
            check(intent.HasValue==expected,"Elemental position opportunity filter");
            if(intent is not { } i)continue;
            check(i.SourceResourceId==id&&i.Controller==controller&&i.TargetSide==active&&
                i.Opportunity==phase&&i.RestrictsManualPositionForTurn==(id==1517),
                "Aqua/Garuda distinct opportunity and turn restriction intent");
            var target=Monster(24);
            check(ElementalPositionRules.IsEligible(i,active,target,false,false)&&
                !ElementalPositionRules.IsEligible(i,controller,target,false,false)&&
                !ElementalPositionRules.IsEligible(i,active,target,true,false)&&
                !ElementalPositionRules.IsEligible(i,active,target,false,true)&&
                !ElementalPositionRules.IsEligible(i,active,null,false,false)&&
                !ElementalPositionRules.IsEligible(i,active,new Card(342,342,"Spell","",22u<<20),false,false),
                "Only live opposing face-up monster eligible");
        }
    }
}
