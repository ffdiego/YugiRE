namespace Joey.Core;

public readonly record struct DemoSuppressedTrap(CardTarget Target,int Slot);

public sealed partial class DemoDuel
{
    /// <summary>Public snapshot for the 0059CCF0-style Jinzo sweep.
    /// This does not set suppression flags or change trap legality.</summary>
    public IReadOnlyList<DemoSuppressedTrap> JinzoSuppressionPresentationTargets(int side,int slot,Card source)
    {
        if(side is not (0 or 1) || slot is <0 or >=5 || source.Id!=FlipEffectSuppressionRules.Jinzo ||
            !ReferenceEquals(Monsters[side,slot],source) || MonsterFaceDown[side,slot])return [];
        List<DemoSuppressedTrap> targets=[];
        for(int p=0;p<2;p++)
        for(int z=0;z<5;z++)
        {
            if(Spells[p,z] is {ClassCode:21} && !SpellFaceDown[p,z] &&
                TryCaptureCardTarget(p,DuelZone.SpellTrap,z,out var target))
                targets.Add(new(target,z));
        }
        return targets.AsReadOnly();
    }

    public bool IsCurrentSuppressionPresentationTarget(DemoSuppressedTrap target) =>
        TryCaptureCardTarget(target.Target.Side,DuelZone.SpellTrap,target.Slot,out var current) &&
        current==target.Target && Spells[current.Side,target.Slot] is {ClassCode:21} &&
        !SpellFaceDown[current.Side,target.Slot];
}
