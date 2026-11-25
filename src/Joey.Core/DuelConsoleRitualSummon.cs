namespace Joey.Core;

public sealed partial class DuelConsoleSession
{
    private DuelConsoleResult ExecuteRitualSummon(string[] words)
    {
        const string usage = "Expected ritual hand|set SPELL MONSTER SLOT attack|defense (hand|field INDEX)...";
        if (words.Length < 8 || words.Length % 2 != 0 || words[1] is not ("hand" or "set") ||
            !Index(words[2], out int spellIndex) || !Index(words[3], out int monsterIndex) ||
            !Slot(words[4], out int slot) || words[5] is not ("attack" or "defense"))
            return new(false, usage);

        int side = duel.ActivePlayer;
        DuelZone sourceZone = words[1] == "hand" ? DuelZone.Hand : DuelZone.SpellTrap;
        if (!duel.TryCaptureCardTarget(side, sourceZone, spellIndex, out var spell) ||
            !duel.TryCaptureCardTarget(side, DuelZone.Hand, monsterIndex, out var monster))
            return new(false, "Choose a tracked Ritual spell and monster.");

        var materials = new List<CardTarget>();
        for (int i = 6; i < words.Length; i += 2)
        {
            if (words[i] is not ("hand" or "field") || !Index(words[i + 1], out int index))
                return new(false, usage);
            DuelZone materialZone = words[i] == "hand" ? DuelZone.Hand : DuelZone.Monster;
            if (!duel.TryCaptureCardTarget(side, materialZone, index, out var material))
                return new(false, "Choose a tracked material.");
            materials.Add(material);
        }

        bool admitted = duel.TryQueueRitualSummon(spell, monster, slot, materials,
            words[5] == "defense", out string message, ActionOrigin.Console);
        return new(admitted, message, admitted);
    }
}
