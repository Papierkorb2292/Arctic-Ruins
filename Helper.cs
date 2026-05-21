using Game.Core.Coordinates;
using ShapezShifter.Flow.Atomic;

namespace DiagonalCutter
{
    public static class Helper
    {
        public static BuildingItemInput ToInput(this ShapeConnectorConfig shapeConnectorConfig, TileVector? pos = null)
        {
            return new BuildingItemInput
            {
                Position_L = pos ?? TileVector.Zero,
                Direction_L = shapeConnectorConfig.Direction.Value,
                StandType = shapeConnectorConfig.StandType,
                IOType = shapeConnectorConfig.CapsType,
                Seperators = shapeConnectorConfig.Separators
            };
        }
        
        public static BuildingItemOutput ToOutput(this ShapeConnectorConfig shapeConnectorConfig, TileVector? pos = null)
        {
            return new BuildingItemOutput
            {
                Position_L = pos ?? TileVector.Zero,
                Direction_L = shapeConnectorConfig.Direction.Value,
                StandType = shapeConnectorConfig.StandType,
                IOType = shapeConnectorConfig.CapsType,
                Seperators = shapeConnectorConfig.Separators
            };
        }
    }
}