using Joey.Core;

internal static class EquipmentChecks
{
    private static Card Monster(int type, int attribute = 2, int defense = 1000) =>
        new(42, 77, "Synthetic target", "", ((uint)type << 20) | ((uint)attribute << 29) |
            (100u << 9) | (uint)(defense / 10));
    private static Card EquipCard(ushort id) => new(500 + id, id, $"Synthetic equipment {id}", "", 22u << 20);
    private static DemoDuel Duel(ushort id, Card target, int owner = 0, int targetSide = 0, bool resolved = true) 
    {
        var sides = new[] { new Card?[5], new Card?[5] };
        sides[targetSide][0] = target;
        return DemoDuel.FromPartialObservation(owner, [EquipCard(id)], sides[0], sides[1], 8000,
            equipmentContextResolved: resolved);
    }
    private static string State(DemoDuel d) => string.Join(";", d.Turn, d.Phase, d.Winner,
        string.Join(",", d.Hand.Select(c => c.Id)), string.Join(",", d.Life),
        string.Join(",", d.Spells.Cast<Card?>().Select(c => c?.Id.ToString() ?? "null")),
        string.Join(",", d.Monsters.Cast<Card?>().Select(c => c?.Index.ToString() ?? "null")),
        string.Join(",", d.SpellFaceDown.Cast<bool>()), string.Join(",", d.MonsterFaceDown.Cast<bool>()),
        string.Join(",", Enumerable.Range(0,2).Select(p => string.Join("/", d.Graveyard(p).Select(c => c.Id)))),
        string.Join(",", d.EquipmentCards));

    public static void Run(Action<bool, string> check)
    {
        var context = new EquipmentTargetContext(1, 4, 42, 77, true, 15, 2, true);
        foreach (var (id, type, attack, defense) in new (ushort, int, int, int)[]
            { (300,15,1300,1300), (307,11,1300,1300), (301,7,1400,800) })
        {
            var correct = context with { DynamicType = (byte)type };
            var positive = EquipmentRules.Eligibility(id, correct);
            check(positive.IsSupported && positive.Value, $"P025 {id}: explicit resolved eligibility");
            var wrong = id == 301 ? correct with { DynamicAttribute = 1 } : correct with { DynamicType = 1 };
            var negative = EquipmentRules.Eligibility(id, wrong);
            check(negative.IsSupported && !negative.Value, $"P025 {id}: wrong dynamic type/attribute rejected");
            foreach (var missing in new EquipmentTargetContext?[] { null, new(),
                correct with { Player = null }, correct with { Zone = null }, correct with { InternalIndex = null },
                correct with { ResourceId = null }, correct with { FaceUp = null }, correct with { DynamicType = null },
                correct with { DynamicAttribute = null }, correct with { GenericGuardsSatisfied = null } })
                check(!EquipmentRules.Eligibility(id, missing).IsSupported, $"P025 {id}: incomplete target remains Unsupported");
            foreach (var invalid in new[] { correct with { Player = -1 }, correct with { Player = 2 },
                correct with { Zone = -1 }, correct with { Zone = 5 }, correct with { InternalIndex = 0 },
                correct with { InternalIndex = 4096 }, correct with { ResourceId = ushort.MaxValue },
                correct with { FaceUp = false }, correct with { GenericGuardsSatisfied = false } })
            {
                var rejected = EquipmentRules.Eligibility(id, invalid);
                check(rejected.IsSupported && !rejected.Value, $"P025 {id}: known invalid target is ineligible");
            }
            // Source resource ID, target resource ID, internal index, and position are deliberately different.
            foreach (int owner in new[] { 0, 1 })
            foreach (int targetSide in new[] { 0, 1 })
            {
                var d = Duel(id, Monster(type), owner, targetSide);
                check(d.CanActivateFromHand(0) && d.EquipmentEligibility(0,targetSide,0).Value &&
                    d.Equip(0,targetSide,0,out _) && d.Hand.Count == 0 &&
                    d.Spells[owner,0]?.Id == id && d.EquipmentCards.Single() is { } link &&
                    link.Owner == owner && link.SourceZone == 0 && link.TargetPlayer == targetSide && link.TargetZone == 0,
                    $"P025 {id}: owner {owner} target side {targetSide}, correct link and zone");
                check(d.AttackValue(targetSide,0) == attack && d.DefenseValue(targetSide,0) == defense,
                    $"P025 {id}: independent ATK/DEF side {targetSide}");
                check(d.RemoveEquipment(owner,0) && d.AttackValue(targetSide,0) == 1000 &&
                    d.DefenseValue(targetSide,0) == 1000 && d.EquipmentCards.Count == 0 &&
                    d.Spells[owner,0] == null && !d.SpellFaceDown[owner,0] && d.Graveyard(owner).Single().Id == id,
                    $"P025 {id}: isolated removal restores stats and owner graveyard");
                string after = State(d);
                check(!d.RemoveEquipment(owner,0) && State(d) == after, $"P025 {id}: removal does not duplicate graveyard");
                d.Spells[owner,0] = EquipCard(302);
                check(d.AttackValue(targetSide,0) == 1000 && d.DefenseValue(targetSide,0) == 1000,
                    $"P025 {id}: reused source slot has no inherited link");
            }
            var unknown = Duel(id, Monster(type), resolved:false); string before = State(unknown);
            check(!unknown.EquipmentEligibility(0,0,0).IsSupported && !unknown.Equip(0,0,0,out _) && State(unknown)==before,
                $"P025 {id}: partial observation cannot assume dynamic context or generic guard");
            var hidden = Duel(id, Monster(type)); hidden.MonsterFaceDown[0,0] = true; before = State(hidden);
            check(!hidden.Equip(0,0,0,out _) && State(hidden)==before, $"P025 {id}: face-down rejection atomic");
            var full = Duel(id, Monster(type));
            for (int z=0;z<5;z++) full.Spells[0,z]=EquipCard(342);
            before=State(full);
            check(!full.CanActivateFromHand(0) && !full.Equip(0,0,0,out _) && State(full)==before,
                $"P025 {id}: full own field rejection atomic");
            var stale = Duel(id, Monster(type));
            check(stale.EquipmentEligibility(0,0,0).Value, $"P025 {id}: selection initially eligible");
            stale.MonsterFaceDown[0,0]=true; before=State(stale);
            check(!stale.Equip(0,0,0,out _) && State(stale)==before, $"P025 {id}: resolution revalidates changed face");
            stale.MonsterFaceDown[0,0]=false;
            stale.Monsters[0,0]=Monster(id==301?type:1,attribute:1); before=State(stale);
            check(!stale.Equip(0,0,0,out _) && State(stale)==before, $"P025 {id}: resolution revalidates replacement properties");
            stale.Monsters[0,0]=null; before=State(stale);
            check(!stale.Equip(0,0,0,out _) && State(stale)==before, $"P025 {id}: departed target leaves hand and zone intact");
            var invalidDuel = Duel(id,Monster(type)); before=State(invalidDuel);
            check(!invalidDuel.Equip(-1,0,0,out _) && !invalidDuel.Equip(1,0,0,out _) &&
                !invalidDuel.Equip(0,2,0,out _) && !invalidDuel.Equip(0,0,5,out _) && State(invalidDuel)==before,
                $"P025 {id}: invalid indices preserve state");
            var battle = Duel(id,Monster(type),owner:1); battle.BeginBattle(out _); before=State(battle);
            check(!battle.Equip(0,0,0,out _) && State(battle)==before, $"P025 {id}: no activation in battle");
            var knownWrong = Duel(id,Monster(id==301?15:1, attribute:1)); before=State(knownWrong);
            check(!knownWrong.Equip(0,0,0,out _) && State(knownWrong)==before, $"P025 {id}: wrong predicate is atomic in demo");

            // Real demo destruction path, equipment owned by both sides; reuse the vacated monster slot.
            var target=Monster(type); var e=EquipCard(id);
            var destruction=new DemoDuel([target,target,target,e]);
            check(destruction.Summon(0,0,0,true,out _) && destruction.Equip(2,0,0,out _), $"P025 {id}: destruction setup own equip");
            destruction.NextTurn();
            check(destruction.Equip(3,0,0,out _), $"P025 {id}: destruction setup enemy equip");
            destruction.Monsters[1,0]=new Card(43,78,"Attacker","",(15u<<20)|(400u<<9));
            check(destruction.BeginBattle(out _) && destruction.Attack(0,0,out _) &&
                destruction.Monsters[0,0] is null && destruction.EquipmentCards.Count==0 &&
                destruction.Spells[0,0] is null && destruction.Spells[1,0] is null &&
                destruction.Graveyard(0).Count==2 && destruction.Graveyard(1).Single().Id==id,
                $"P025 {id}: destruction clears both owners' links and spell slots");
            destruction.NextTurn();
            check(destruction.Summon(0,0,0,true,out _) && destruction.AttackValue(0,0)==1000 &&
                destruction.DefenseValue(0,0)==1000, $"P025 {id}: reused monster slot has base stats");
        }
        // All added cards use the existing activation, link, stat and cleanup path.
        foreach (var (id, type, attribute, atk, def) in new (ushort, int, int, int, int)[]
        {
            (302, 3, 0, 300, 300),
            (304, 10, 0, 300, 300),
            (306, 0, 1, 400, -200),
            (308, 0, 3, 400, -200),
            (309, 13, 0, 300, 300),
            (311, 17, 0, 300, 300),
            (314, 1, 0, 300, 300),
            (315, 19, 0, 300, 300),
            (318, 12, 0, 300, 300),
            (321, 2, 0, 300, 300),
            (322, 18, 0, 300, 300),
            (324, 7, 0, 300, 300),
            (325, 9, 0, 300, 300),
            (326, 16, 0, 300, 300),
            (327, 14, 0, 300, 300),
            (653, 0, 4, 700, 0),
            (1012, 0, 4, 400, -200),
            (1013, 0, 6, 400, -200),
        })
        {
            int validType = type == 0 ? 15 : type;
            int validAttribute = attribute == 0 ? 2 : attribute;
            var correct = context with { DynamicType = (byte)validType, DynamicAttribute = (byte)validAttribute };
            check(EquipmentRules.Supports(id) && EquipmentRules.Eligibility(id, correct) is { IsSupported: true, Value: true },
                $"P025 expanded {id}: correct dynamic predicate");
            var wrong = type != 0 ? correct with { DynamicType = (byte)(type == 1 ? 2 : 1) }
                : correct with { DynamicAttribute = (byte)(attribute == 1 ? 2 : 1) };
            check(EquipmentRules.Eligibility(id, wrong) is { IsSupported: true, Value: false } &&
                EquipmentRules.Eligibility(id, correct with { FaceUp = false }).Value == false &&
                !EquipmentRules.Eligibility(id, correct with { GenericGuardsSatisfied = null }).IsSupported,
                $"P025 expanded {id}: incompatible, hidden and unresolved targets");
            foreach (int owner in new[] { 0, 1 })
            foreach (int targetSide in new[] { 0, 1 })
            {
                var d = Duel(id, Monster(validType, validAttribute), owner, targetSide);
                check(d.CanActivateFromHand(0) && d.Equip(0,targetSide,0,out _) &&
                    d.Hand.Count == 0 && d.Spells[owner,0]?.Id == id &&
                    d.EquipmentCards.Single() is { } link && link.Owner == owner &&
                    link.TargetPlayer == targetSide && link.TargetZone == 0,
                    $"P025 expanded {id}: live link owner {owner}, target {targetSide}");
                check(d.AttackValue(targetSide,0) == 1000 + atk &&
                    d.DefenseValue(targetSide,0) == 1000 + def,
                    $"P025 expanded {id}: correct asymmetric stats");
                check(d.RemoveEquipment(owner,0) && d.EquipmentCards.Count == 0 &&
                    d.Spells[owner,0] == null && d.Graveyard(owner).Single().Id == id &&
                    d.AttackValue(targetSide,0) == 1000 && d.DefenseValue(targetSide,0) == 1000,
                    $"P025 expanded {id}: cleanup restores stats");
            }
            var rejected = Duel(id, Monster(type == 0 ? validType : (type == 1 ? 2 : 1),
                attribute == 0 ? validAttribute : (attribute == 1 ? 2 : 1)));
            string before = State(rejected);
            check(!rejected.Equip(0,0,0,out _) && State(rejected) == before,
                $"P025 expanded {id}: incompatible activation is atomic");
        }
        foreach (ushort id in new ushort[] { 0,305,316,323,656,657,667,1046,1047,1058,1060,1182,1313,1419,1420,1422,1449,1450,1540,1548,1550,2300,2301,2307,65535 })
        {
            check(!EquipmentRules.Supports(id) && !EquipmentRules.Eligibility(id,context).IsSupported,
                $"P025 {id}: outside whitelist even if contribution helper knows this ID");
            var d=Duel(id,Monster(15));string before=State(d);
            check(!d.Equip(0,0,0,out _) && State(d)==before, $"P025 {id}: unsupported card cannot create link");
        }
        foreach (ushort symmetric in new ushort[] { 300,307 })
        foreach (bool darkFirst in new[] { false,true })
        {
            var cards=new[] { EquipCard(301),EquipCard(symmetric) };
            if (!darkFirst) Array.Reverse(cards);
            var d=DemoDuel.FromPartialObservation(0,cards,[Monster(symmetric==300?15:11,defense:100),null,null,null,null],
                new Card?[5],8000,equipmentContextResolved:true);
            check(d.Equip(0,0,0,out _) && d.Equip(0,0,0,out _) && d.AttackValue(0,0)==1700 && d.DefenseValue(0,0)==200,
                $"P025 {symmetric}+301 order {darkFirst}: sum before clamp, distinct ATK/DEF");
        }
        var low=Duel(301,Monster(1,defense:100));
        check(low.Equip(0,0,0,out _) && low.AttackValue(0,0)==1400 && low.DefenseValue(0,0)==0,
            "P025 negative final DEF clamps to zero without reducing ATK");
        low.MonsterFaceDown[0,0]=true;
        check(low.AttackValue(0,0)==1000 && low.DefenseValue(0,0)==100, "P025 face-down stats skip equipment contribution");
        low.MonsterFaceDown[0,0]=false;
        low.SpellFaceDown[0,0]=true;
        check(low.AttackValue(0,0)==1000 && low.DefenseValue(0,0)==100, "P025 face-down equipment does not contribute");
        check(EquipmentRules.Eligibility(300, context with { DynamicType=11 }).Value==false &&
            EquipmentRules.Eligibility(307, context with { DynamicType=11, DynamicAttribute=1 }).Value &&
            EquipmentRules.Eligibility(301, context with { DynamicType=11 }).Value,
            "P025 dynamic type and attribute are independent of target resource identity");
    }
}
