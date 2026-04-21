using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface IShadowSlotStore
{
    void SetSlots(string peerId, IReadOnlyList<BusySlot> slots);
    IReadOnlyList<BusySlot> GetSlots(string peerId);
}
