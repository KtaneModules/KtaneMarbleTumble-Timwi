using System.Collections.Generic;
using System.Linq;
using RT.Dijkstra;

namespace MarbleTumble
{
    sealed class DijNode : Node<int, int>
    {
        public int[] Rotations { get; private set; }
        public int[] Traps { get; private set; }
        public int[] ColorIxs { get; private set; }
        public int Marble { get; private set; }
        public int LastSec { get; private set; }

        public DijNode(int[] rotations, int[] traps, int[] colorIxs, int marble, int lastSec)
        {
            Rotations = rotations;
            Traps = traps;
            ColorIxs = colorIxs;
            Marble = marble;
            LastSec = lastSec;
        }

        private static readonly int[][] _rotationData = @"-1,1,-2,0,2;-2,1,2,-1,0;1,0,2,-2,-1;0,-1,-2,1,2;2,0,1,-1,-2;1,-2,-1,2,0;-2,2,0,1,-1;0,-1,1,2,-2;-1,2,0,-2,1;2,-2,-1,0,1".Split(';').Select(str => str.Split(',').Select(s => int.Parse(s)).ToArray()).ToArray();

        public override bool IsFinal { get { return Marble == 0; } }

        public static int m(int v) { return (v % 10 + 10) % 10; }

        public override IEnumerable<Edge<int, int>> Edges
        {
            get
            {
                if (Marble == 0 || m(Traps[Marble - 1] + Rotations[Marble - 1]) == (Marble == 5 ? 0 : m(Rotations[Marble])))
                    yield break;

                for (int sec = 0; sec < 10; sec++)
                {
                    var newRotations = new int[5];
                    for (var i = 0; i < 5; i++)
                        newRotations[i] = Rotations[i] + _rotationData[sec][ColorIxs[i]];
                    var newMarble = Marble;
                    while (newMarble > 0 && (newMarble == 5 ? 0 : m(newRotations[newMarble])) == m(newRotations[newMarble - 1]))
                        newMarble--;
                    var weight = 60 * ((LastSec == -1) ? 1 : (sec < LastSec) ? (LastSec - sec) : (LastSec + 10 - sec));
                    if (Marble != newMarble && Marble - newMarble <= weight)
                        weight /= (Marble - newMarble);
                    yield return new Edge<int, int>(weight, sec, new DijNode(newRotations, Traps, ColorIxs, newMarble, sec));
                }
            }
        }

        public override bool Equals(Node<int, int> other)
        {
            if (!(other is DijNode))
                return false;
            var oth = other as DijNode;
            for (int i = 0; i < 5; i++)
                if (m(Rotations[i]) != m(oth.Rotations[i]))
                    return false;
            return Marble == oth.Marble && LastSec == oth.LastSec;
        }

        public override int GetHashCode()
        {
            const int b = 378551;
            int a = 63689;
            int hash = 12;

            unchecked
            {
                for (int i = 0; i < 5; i++)
                {
                    hash = hash * a + m(Rotations[i]);
                    a *= b;
                }
                return hash + Marble + 17 * LastSec;
            }
        }

        public override string ToString()
        {
            return string.Format("Rotations: {0}; Marble: {1}", Rotations.JoinString(", "), Marble);
        }
    }
}
