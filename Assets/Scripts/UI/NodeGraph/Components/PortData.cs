using KexEdit.Legacy;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.UI.NodeGraph {
    public class PortData {
        public Entity Entity;
        public Port Port;
        public Entity Node;
        public int Index;
        public PointData Value;
        public PortState InteractionState;
        public UnitsType Units;
        public bool IsConnected;

        public bool Hovered => InteractionState.HasFlag(PortState.Hovered);
        public bool Dragging => InteractionState.HasFlag(PortState.Dragging);
        public bool Connected => InteractionState.HasFlag(PortState.Connected);

        public static PortData Create(Entity entity, Port port, Entity node, int index, UnitsType units) {
            return new PortData {
                Entity = entity,
                Port = port,
                Node = node,
                Index = index,
                Units = units,
            };
        }

        public static PortData WithValue(PortData portData, PointData value) {
            portData.Value = value;
            return portData;
        }

        public static PortData WithValue(PortData portData, float3 value) {
            portData.Value.Roll = value.x;
            portData.Value.Velocity = value.y;
            portData.Value.Energy = value.z;
            return portData;
        }

        public static PortData WithValue(PortData portData, float value) {
            portData.Value.Roll = value;
            return portData;
        }

        public void SetValue(PointData value) {
            Value = value;
        }

        public void SetValue(float3 value) {
            Value.Roll = value.x;
            Value.Velocity = value.y;
            Value.Energy = value.z;
        }

        public void SetValue(float value) {
            Value.Roll = value;
        }

        public void GetValue(out PointData value) {
            value = Value;
        }

        public void GetValue(out float3 value) {
            value = new(Value.Roll, Value.Velocity, Value.Energy);
        }

        public void GetValue(out float value) {
            value = Value.Roll;
        }

        public void Update(bool isConnected) {
            if (isConnected) InteractionState |= PortState.Connected;
            else InteractionState &= ~PortState.Connected;
        }
    }
}
