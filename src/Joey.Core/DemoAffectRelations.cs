namespace Joey.Core;

public enum DemoAffectKind { Equipment, Field, Power, Restriction }

/// <summary>Public board relationships only. No hidden card identities,
/// visual clock, rule commands or separate scheduler.</summary>
public readonly record struct DemoAffectRelation(CardTarget Target,int Slot,DemoAffectKind Kind);

public sealed partial class DemoDuel
{
    public IReadOnlyList<DemoAffectRelation> AffectRelations(CardTarget focus)
    {
        if (!TryResolveCardTarget(focus,out int slot,out var card) ||
            !FaceUp(focus.Side,focus.Zone,slot)) return [];
        List<DemoAffectRelation> result=[];
        void Add(int side,DuelZone zone,int index,DemoAffectKind kind)
        {
            if (FaceUp(side,zone,index) && TryCaptureCardTarget(side,zone,index,out var target))
            {
                var relation=new DemoAffectRelation(target,index,kind);
                if (!result.Contains(relation)) result.Add(relation);
            }
        }
        // Match the live occurrence at both endpoints; never infer links
        // from two copies of the same catalogue card.
        foreach(var e in equipment)
        {
            if(!ReferenceEquals(Spells[e.Owner,e.SourceZone],e.Card) ||
                !ReferenceEquals(Monsters[e.TargetPlayer,e.TargetZone],e.TargetCard) ||
                SpellFaceDown[e.Owner,e.SourceZone] || MonsterFaceDown[e.TargetPlayer,e.TargetZone]) continue;
            if(focus.Zone==DuelZone.Monster && focus.Side==e.TargetPlayer && slot==e.TargetZone)
                Add(e.Owner,DuelZone.SpellTrap,e.SourceZone,DemoAffectKind.Equipment);
            if(focus.Zone==DuelZone.SpellTrap && focus.Side==e.Owner && slot==e.SourceZone)
                Add(e.TargetPlayer,DuelZone.Monster,e.TargetZone,DemoAffectKind.Equipment);
        }
        var field=ActiveFieldSpell;
        for(int side=0;side<2;side++)
        {
            if(field is not null && ReferenceEquals(FieldSpells[side],field) && !FieldSpellFaceDown[side])
            {
                if(focus.Zone==DuelZone.Monster && FieldApplies(field,card,focus.Side,slot))
                    Add(side,DuelZone.FieldSpell,0,DemoAffectKind.Field);
                if(focus.Zone==DuelZone.FieldSpell && ReferenceEquals(card,field))
                    for(int p=0;p<2;p++)
                    for(int z=0;z<5;z++)
                        if(Monsters[p,z] is Card unit && !MonsterFaceDown[p,z] && FieldApplies(field,unit,p,z))
                            Add(p,DuelZone.Monster,z,DemoAffectKind.Field);
            }
            if(focus.Zone!=DuelZone.Monster)continue;
            for(int z=0;z<5;z++)
            {
                if(Monsters[side,z] is not Card other || MonsterFaceDown[side,z])continue;
                if(MonsterAuraRules.AttackDelta(other.Id,(byte)(card.Properties>>29))!=0 ||
                   MonsterAuraRules.AttackDelta(card.Id,(byte)(other.Properties>>29))!=0)
                    Add(side,DuelZone.Monster,z,DemoAffectKind.Power);
                if(restrictedAttackTargets.TryGetValue(card,out var restricted) && ReferenceEquals(restricted,other) ||
                   restrictedAttackTargets.TryGetValue(other,out var restrictedByOther) && ReferenceEquals(restrictedByOther,card))
                    Add(side,DuelZone.Monster,z,DemoAffectKind.Restriction);
            }
        }
        return result.AsReadOnly();
    }

    private bool FaceUp(int side,DuelZone zone,int slot) => zone switch
    {
        DuelZone.Monster=>Monsters[side,slot] is not null && !MonsterFaceDown[side,slot],
        DuelZone.SpellTrap=>Spells[side,slot] is not null && !SpellFaceDown[side,slot],
        DuelZone.FieldSpell=>FieldSpells[side] is not null && !FieldSpellFaceDown[side],
        _=>false
    };

    private bool FieldApplies(Card field,Card monster,int side,int slot)
    {
        // Exactly the same contribution as the current sandbox stats
        // calculation, not a second implementation of the native rules.
        var bonus=FieldSpellRules.Bonus(field.Id,monster.ClassCode,
            (byte)(monster.Properties>>29),DefensePosition[side,slot]);
        return bonus.Attack!=0 || bonus.Defense!=0;
    }
}
