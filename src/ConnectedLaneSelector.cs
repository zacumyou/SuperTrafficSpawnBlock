using System.Collections.Generic;

namespace SuperTrafficSpawnBlock
{
    internal struct LaneExit<TLane, TNode>
    {
        internal TLane Lane;
        internal TNode End;
        internal LaneExit(TLane lane, TNode end) { Lane = lane; End = end; }
    }

    internal sealed class ConnectedLaneSelector<TKey, TLane, TNode>
    {
        private readonly Dictionary<TKey, uint> cursors = new Dictionary<TKey, uint>();
        private readonly List<TLane> connected = new List<TLane>();

        internal bool TryChoose(TKey key, IList<LaneExit<TLane, TNode>> lanes, ISet<TNode> nextStarts, out TLane selected)
        {
            connected.Clear();
            foreach (var lane in lanes)
                if (nextStarts.Contains(lane.End)) connected.Add(lane.Lane);
            selected = default(TLane);
            // Do not pin an existing path unless it actually offers a distribution choice.
            if (connected.Count < 2) return false;
            cursors.TryGetValue(key, out uint cursor);
            selected = connected[(int)(cursor % (uint)connected.Count)];
            cursors[key] = unchecked(cursor + 1);
            return true;
        }

        internal void Clear() { cursors.Clear(); connected.Clear(); }
    }
}
