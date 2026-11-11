namespace San9.Core.Domain;

/// <summary>Deterministic, one-time issue from content-authored ordinary equipment definitions.</summary>
public static class InitialEquipment
{
    public static EquipmentInstance[] Issue(IEnumerable<EquipmentInstance> existing, IEnumerable<OfficerState> officers,
        EquipmentRulebook rules)
    {
        var items = existing.ToList();
        if (!EquipmentRules.ValidInventory(items, rules.Items)) throw new ArgumentException("Invalid equipment inventory.");
        var definitions = rules.Items.Values.Where(d => d.InitialIssue == true).OrderBy(d => d.Slot).ToArray();
        var held = items.Where(i => i.OfficerId is not null)
            .GroupBy(i => i.OfficerId!.Value).ToDictionary(g => g.Key, g => g.ToArray());
        long next = items.Select(i => i.Id).DefaultIfEmpty(-1).Max();
        foreach (var person in officers.Where(p => !p.IsDormant && p.Status <= OfficerStatus.NotAppeared).OrderBy(p => p.Id))
        {
            var current = held.GetValueOrDefault(person.Id) ?? [];
            foreach (var definition in definitions)
            {
                // Existing possessions take precedence, including currently unequipped named items.
                if (current.Any(item => rules.Items[item.DefinitionId].Slot == definition.Slot)) continue;
                if (next == long.MaxValue) throw new InvalidDataException("No equipment instance identities remain for initial issue.");
                bool equip = person.IsServing;
                if (equip && definition.Slot == EquipmentSlot.MainHand && current.FirstOrDefault(item => item.Equipped &&
                    rules.Items[item.DefinitionId].Slot == EquipmentSlot.OffHand) is { } off &&
                    !EquipmentRules.Compatible(definition, rules.Items[off.DefinitionId])) equip = false;
                items.Add(new EquipmentInstance(++next, definition.Id, person.Id, null, equip)
                    { EquipOnService = person.IsServing ? null : true });
            }
        }
        return items.ToArray();
    }
}
