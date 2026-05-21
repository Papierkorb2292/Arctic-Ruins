using System;
using System.Linq;
using Core.Collections.Scoped;
using Game.Core.Coordinates;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Toolbar;
using UnityEngine;

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
        
        public static IToolbarEntryInsertLocation Replace(this IToolbarElementLocator elementLocator)
        {
            return new ToolbarEntryReplaceLocation(elementLocator);
        }

        private class ToolbarEntryReplaceLocation : IToolbarEntryInsertLocation
        {
            public readonly IToolbarElementLocator ElementLocator;
            
            public ToolbarEntryReplaceLocation(IToolbarElementLocator elementLocator)
            {
                ElementLocator = elementLocator;
            }
            
            void IToolbarEntryInsertLocation.AddEntry(
                ToolbarData toolbarData,
                IToolbarElementData elementData)
            {
                IParentToolbarElementData elementParent = ElementLocator.FindElementParent(toolbarData);
                Index index1 = ElementLocator.LeafIndex();
                int index2 = (index1.IsFromEnd ? elementParent.Children.Count() - index1.Value : index1.Value) + 1;
                Debug.Log("Replacing");

                using ScopedList<IToolbarElementData> scopedList = ScopedList<IToolbarElementData>.Get(elementParent.Children);
                scopedList[index2] = elementData;
                IToolbarElementData[] array = scopedList.ToArray();
                switch (elementParent)
                {
                    case RootToolbarElementData toolbarElementData1:
                        toolbarElementData1.Children = array;
                        break;
                    case ParentToolbarElementData toolbarElementData2:
                        toolbarElementData2.Children = array;
                        break;
                }
            }

            public override string ToString() => $"Replace \n{ElementLocator}";
        }
    }
}