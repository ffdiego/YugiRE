namespace Joey.LookAndFeel;

/// <summary>0045E240 is a last-write-wins, single-update mailbox.
/// Zero is a real key (tribute restriction), not an empty request.
/// Keep this independent of game commands, rules and animation completion.</summary>
public sealed class NativeHelpMailbox
{
    public int Key { get; private set; } = -1;
    public void Request(int key)
    {
        if(key is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(key));
        Key=key;
    }
    public int Consume()
    {
        int key=Key;
        Key=-1;
        return key;
    }
}

/// <summary>Ports of selected help producers, NOT rule evaluators.
/// Predicates are facts returned by the original checks (or equivalent
/// presentation inputs when A03 supplies them). Raw keys are not filenames.</summary>
public static class NativeHelpProducers
{
    // 0056B5A0: no card -> no write; flag 20000 first, then group
    // of position restrictions, then 0056B440 (Prohibition).
    public static int? Position(bool localSide,bool occupied,bool flag20000,
        bool otherPositionRestriction,bool prohibition)
    {
        if(!localSide||!occupied)return null;
        if(flag20000)return 3;
        if(otherPositionRestriction)return 0x12;
        return prohibition?0x10:null;
    }

    // 005A0530, native phase 3 path. The unspent branch emits 7
    // BEFORE calling 0056B730; its nested checks may replace the key.
    public static int BattleProbe(bool slotBitSet) => slotBitSet?8:7;

    // 005929D0: no side check; accepts hovered zone 0..11 inclusive.
    public static int? HoverZone(int zone) => zone is >=0 and <12?9:null;

    // 005AAB90: the normal summon eligibility probe can write 0x11
    // and continue into the tribute/space check, overwriting that key.
    // Only model those writes; do not return legality or action bits.
    public static int? NormalSummonProbe(bool localSide,bool monster,
        bool prohibition,bool canPayEntryCost,bool early00567A00,
        bool normalSummonGate,int nativeLevel,int eligibleTributes,
        bool eligibleEmptyZone,bool postTributeZoneAvailable)
    {
        if(!localSide||!monster)return null;
        if(prohibition)return 0x10;
        if(!canPayEntryCost)return 0x17;
        if(early00567A00)return null;
        int? key=normalSummonGate?null:0x11;
        if(nativeLevel<=4)
            return eligibleEmptyZone?key:0xb;
        if(eligibleTributes<(nativeLevel<=6?1:2))return 0;
        return postTributeZoneAvailable?key:0xb;
    }

    // 0057EAC0: exact side is derived by XOR of descriptor bits 0/14;
    // 00570A10/0057C450 compare cost strictly LESS than current LP.
    public static bool IsLocalActivation(ushort descriptor,int localSide) =>
        (((descriptor>>14)^descriptor)&1)==(localSide&1);

    public static int? ActivationCost(bool localActivation,int effectiveCost,int life) =>
        localActivation&&effectiveCost>=life?0x17:null;
}
