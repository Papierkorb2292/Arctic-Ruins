using Game.Core.Serialization;
using Game.Core.Simulation;

namespace ArcticRuins.DataFragment;

[SyncableIdentifier("DataFragmentState")]
public class DataFragmentSimulationState : ISimulationState
{
    public SaveData.TechReference? Reward;
    public bool GeneratedReward = false;
    public bool UnlockedReward = false;
    
    public void Sync(ISerializationVisitor visitor)
    {
        if (visitor.Writing)
        {
            if (Reward == null)
            {
                visitor.WriteBool_1(false);
            }
            else
            {
                visitor.WriteBool_1(true);
                visitor.WriteInt_4(Reward.Value.Level);
                visitor.WriteInt_4(Reward.Value.Index);
            }
            visitor.WriteBool_1(GeneratedReward);
            visitor.WriteBool_1(UnlockedReward);
        }
        else
        {
            Reward = visitor.ReadBool_1() ? new SaveData.TechReference(visitor.ReadInt_4(), visitor.ReadInt_4()) : null;
            GeneratedReward = visitor.ReadBool_1();
            UnlockedReward = visitor.ReadBool_1();
        }
    }
}