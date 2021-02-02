using System;

namespace Kalmia.Engines.MCTSEngine
{
    public struct NodeInfo
    {
        public Move Move;
        public float ScoreSum;
        public int SimulationCount;
        public float Value;
    }

    public struct SearchResult
    {
        public NodeInfo RootNodeInfo;
        public NodeInfo[] ChildNodesInfo;
    }

    class Node
    {
        const int MAX_CHILD_NODE_NUM = 60;
        public int ID;
        public Node[] ChildNodes = new Node[MAX_CHILD_NODE_NUM];
        public int ChildNodeNum = 0;
        public FastBoard Board = new FastBoard();
        public Move Move;
        public float UCBScore = 0.0f;
        public float ScoreSum = 0.0f;
        public float Value = 0.0f;
        public int SimulationCount = 0;
        public bool IsTerminated = false;
        public float FixedScore = 0.0f;
        public bool IsUsed = false;
    }

    class NodePool
    {
        Node[] Nodes;
        int Loc = 0;

        public NodePool(int size)
        {
            this.Nodes = new Node[size];
            for(var i = 0; i < this.Nodes.Length; i++)
            {
                this.Nodes[i] = new Node();
                this.Nodes[i].ID = i;
            }
        }

        public Node GetNode()
        {
            var start = ++this.Loc;
            while (true)
            {
                if (!this.Nodes[this.Loc].IsUsed)
                {
                    this.Nodes[this.Loc].IsUsed = true;
                    return this.Nodes[this.Loc];
                }

                this.Loc++;
                if (this.Loc == this.Nodes.Length)
                    this.Loc = 0;

                if(this.Loc == start)
                {
                    Console.Error.WriteLine("Node pool is full.");      
                    var node = new Node();      // プールに空きがない場合は、ノードを生成(高コスト)
                    node.ID = -1;
                    node.IsUsed = true;
                    return node;
                }
            }
        }

        public void ReturnNodeToPool(Node node)
        {
            if (node.ID == -1)
                return;
            node.IsUsed = false;
            this.Nodes[node.ID] = node;
        }
    }

    public class UCT
    {

    }
}