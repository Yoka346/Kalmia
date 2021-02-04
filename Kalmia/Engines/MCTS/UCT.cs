using System;

namespace Kalmia.Engines.MCTS
{
    public struct NodeInfo
    {
        public Move Move;
        public float ScoreSum;
        public int SimulationCount;
        public float Value;

        internal NodeInfo(Node node)
        {
            this.Move = node.Move;
            this.ScoreSum = node.ScoreSum;
            this.SimulationCount = node.SimulationCount;
            this.Value = node.Value;
        }
    }

    public struct SearchResult
    {
        public NodeInfo RootNodeInfo;
        public NodeInfo[] ChildNodesInfo;
    }

    class Node
    {
        public const int MAX_CHILD_NODE_NUM = 60;
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
        int Loc = -1;

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
                    var node = this.Nodes[this.Loc];
                    node.ChildNodeNum = 0;
                    node.ScoreSum = 0.0f;
                    node.SimulationCount = 0;
                    node.IsTerminated = false;
                    node.IsUsed = true;
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

        public void Clear()
        {
            if (this.Loc == -1)
                return;

            this.Loc = -1;
            for (var i = 0; i < this.Nodes.Length; i++)
                if (this.Nodes[i].IsUsed)
                    this.Nodes[i].IsUsed = false;
        }
    }

    public class UCT
    {
        const float WIN_SCORE = 1.0f;
        const float DRAW_SCORE = 0.5f;
        const float LOSE_SCORE = 0.0f;

        public int ExpansionThreshold { get; set; }

        NodePool NodePool;
        Node RootNode;

        readonly Xorshift RAND = new Xorshift();
        readonly FastBoard BOARD = new FastBoard();
        readonly Move[] MOVES = new Move[FastBoard.MAX_MOVE_NUM];

        public UCT(int expansionThres = 40, int nodePoolSize = 1000000) 
        {
            this.ExpansionThreshold = expansionThres;
            this.NodePool = new NodePool(nodePoolSize);
        }

        public void SetRoot(Board board)
        {
            this.NodePool.Clear();
            this.RootNode = this.NodePool.GetNode();
            this.RootNode.Board = board.ToFastBoard();
            this.RootNode.Move.Turn = board.Turn;
            this.RootNode.Move.Position = 0UL;
            Expand(this.RootNode); ;
        }

        public bool UpdateRoot(Move move)
        {
            if (this.RootNode == null)
                return false;

            this.RootNode.Board.CopyTo(this.BOARD);
            if (!this.BOARD.IsLegalMove(move))
                return false;
            this.BOARD.Update(move);
            return UpdateRoot(this.BOARD);
        }

        public bool UpdateRoot(Board board)     
        {
            return UpdateRoot(board.ToFastBoard());
        }

        public bool UpdateRoot(FastBoard board)     // boardを持つ子ノードが存在すれば、それをルートノードに設定し、不要なサブツリーは破棄する.
        {
            if (this.RootNode == null)
                return false;

            var idx = -1;
            for (var i = 0; i < this.RootNode.ChildNodeNum; i++)
                if (this.RootNode.ChildNodes[i].Board.EqualTo(board))
                    idx = i;
            if (idx == -1)
                return false;

            var newRoot = this.RootNode.ChildNodes[idx];
            for (var i = idx; i < this.RootNode.ChildNodeNum - 1; i++)
                this.RootNode.ChildNodes[i] = this.RootNode.ChildNodes[i + 1];
            this.RootNode.ChildNodeNum -= 1;

            DeleteNodes(this.RootNode);
            this.RootNode = newRoot;
            if (this.RootNode.ChildNodeNum == 0)
                Expand(this.RootNode);
            return true;
        }

        public SearchResult Search(int maxSimCount, int timeLimit)
        {
            if (this.RootNode == null)
                throw new NullReferenceException("Root node must be initalized before searching.");

            var startTime = Environment.TickCount;
            var simCount = 0;
            while (simCount < maxSimCount && (Environment.TickCount - startTime) < timeLimit)
                VisitNode(this.RootNode);
            return CollectSearchResult();
        }

        SearchResult CollectSearchResult()
        {
            SearchResult result;
            result.RootNodeInfo = new NodeInfo(this.RootNode);
            result.ChildNodesInfo = new NodeInfo[this.RootNode.ChildNodeNum];
            for (var i = 0; i < result.ChildNodesInfo.Length; i++)
                result.ChildNodesInfo[i] = new NodeInfo(this.RootNode.ChildNodes[i]);
            return result;
        }

        void DeleteNodes(Node node)     // 再帰的にノードを破棄
        {
            if (node.ChildNodeNum != 0)
                for (var i = 0; i < node.ChildNodeNum; i++)
                    DeleteNodes(node.ChildNodes[i]);
            this.NodePool.ReturnNodeToPool(node);
        }

        float VisitNode(Node node)
        {
            node.SimulationCount++;

            if (node.IsTerminated)
            {
                UpdateNodeScore(node, node.FixedScore);
                return WIN_SCORE - node.FixedScore;
            }

            float score;
            if (node.ChildNodeNum != 0)
            {
                score = VisitNode(SelectNode(node.ChildNodes, node.ChildNodeNum));
                UpdateNodeScore(node, score);
                return WIN_SCORE - score;
            }

            if (node.SimulationCount > this.ExpansionThreshold) 
            {
                if (node.IsTerminated)
                    return node.FixedScore;
                Expand(node);
                score = VisitNode(SelectNode(node.ChildNodes, node.ChildNodeNum));
                UpdateNodeScore(node, score);
                return WIN_SCORE - score;
            }

            GameResult result;
            if ((result = node.Board.GetResult(node.Board.Turn)) != GameResult.NotEnd)
            {
                node.FixedScore = GetScore(result);
                node.IsTerminated = true;
                return WIN_SCORE - node.FixedScore;
            }

            score = Rollout(node.Board);
            UpdateNodeScore(node, score);
            return WIN_SCORE - score;
        }

        void Expand(Node node)
        {
            var moveNum = node.Board.GetNextMoves(this.MOVES);
            node.ChildNodeNum = moveNum;
            for(var i = 0; i < moveNum; i++)
            {
                var childNode = this.NodePool.GetNode();
                childNode.Move = this.MOVES[i];
                node.Board.CopyTo(this.BOARD);
                this.BOARD.Update(childNode.Move);
                this.BOARD.CopyTo(childNode.Board);
                node.ChildNodes[i] = childNode;
            }
        }

        float Rollout(FastBoard board)
        {
            board.CopyTo(this.BOARD);
            GameResult result;
            while((result = this.BOARD.GetResult(board.Turn)) == GameResult.NotEnd)
            {
                var moveNum = this.BOARD.GetNextMoves(this.MOVES);
                this.BOARD.Update(this.MOVES[this.RAND.Next((uint)moveNum)]);
            }
            return GetScore(result);
        }

        static Node SelectNode(Node[] nodes, int nodeNum)
        {
            if (nodeNum == 1)
                return nodes[0];

            SetUCBScore(nodes, nodeNum);
            var selectedNode = nodes[0];
            for (var i = 1; i < nodeNum; i++)
                if (nodes[i].UCBScore > selectedNode.UCBScore)
                    selectedNode = nodes[i];
            return selectedNode;
        }

        static void UpdateNodeScore(Node node, float score)
        {
            node.ScoreSum += score;
            node.Value = node.ScoreSum / node.SimulationCount;
        }

        static void SetUCBScore(Node[] nodes, int nodeNum)
        {
            var simCountSum = 0;
            for (var i = 0; i < nodeNum; i++)
                simCountSum += nodes[i].SimulationCount;

            for(var i = 0; i < nodeNum; i++)
            {
                var node = nodes[i];
                node.UCBScore = (WIN_SCORE - node.Value) + UCB(node.SimulationCount, simCountSum);
            }
        }

        static float UCB(int simCount, int simCountSum)
        {
            if (simCount == 0)
                return float.PositiveInfinity;
            return MathF.Sqrt(2.0f * MathF.Log(simCountSum) / simCount);
        }

        static float GetScore(GameResult result)
        {
            switch (result)
            {
                case GameResult.Win:
                    return WIN_SCORE;

                case GameResult.Lose:
                    return LOSE_SCORE;

                case GameResult.Draw:
                    return DRAW_SCORE;

                default:
                    return 0.0f;
            }
        }
    }
}