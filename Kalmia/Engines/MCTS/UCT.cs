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

    enum NodeType
    {
        NotTerminated,
        Terminated,
        NotDefinite
    }

    class Node
    {
        public const int MAX_CHILD_NODE_NUM = 60;
        public int ID;
        public NodeType Type;
        public Node[] ChildNodes = new Node[MAX_CHILD_NODE_NUM];
        public int ChildNodeNum = 0;
        public FastBoard Board = new FastBoard();
        public Move Move;
        public float UCBScore = 0.0f;
        public float ScoreSum = 0.0f;
        public float Value = 0.0f;
        public int SimulationCount = 0;
        public float FixedScore = 0.0f;
        public bool IsUsed = false;
    }

    class NodePool
    {
        Node[] nodes;
        int loc = 0;

        public NodePool(int size)
        {
            this.nodes = new Node[size];
            for (var i = 0; i < this.nodes.Length; i++)
            {
                this.nodes[i] = new Node();
                this.nodes[i].ID = i;
            }
        }

        public Node GetNode()
        {
            var start = this.loc;
            while (true)
            {
                if (!this.nodes[this.loc].IsUsed)
                {
                    var node = this.nodes[this.loc];
                    node.ChildNodeNum = 0;
                    node.ScoreSum = 0.0f;
                    node.SimulationCount = 0;
                    node.Type = NodeType.NotDefinite;
                    node.IsUsed = true;
                    return this.nodes[this.loc++];
                }

                if (++this.loc == this.nodes.Length)
                    this.loc = 0;

                if (this.loc == start)
                {
                    Console.Error.WriteLine("Node pool is full.");
                    var node = new Node();      // プールに空きがない場合は、ノードを生成(高コスト)
                    node.ID = -1;
                    node.IsUsed = true;
                    return node;
                }
            }
        }

        public void Clear()
        {
            this.loc = 0;
            for (var i = 0; i < this.nodes.Length; i++)
                if (this.nodes[i].IsUsed)
                    this.nodes[i].IsUsed = false;
        }
    }

    public class UCT
    {
        const float WIN_SCORE = 1.0f;
        const float DRAW_SCORE = 0.5f;
        const float LOSE_SCORE = 0.0f;

        public int ExpansionThreshold { get; set; }

        NodePool nodePool;
        Node rootNode;

        readonly Xorshift RAND = new Xorshift();
        readonly FastBoard BOARD = new FastBoard();
        readonly Move[] MOVES = new Move[FastBoard.MAX_MOVE_NUM];

        public UCT(int expansionThres = 40, int nodePoolSize = 1000000)
        {
            this.ExpansionThreshold = expansionThres;
            this.nodePool = new NodePool(nodePoolSize);
        }

        public void SetRoot(Board board)
        {
            this.nodePool.Clear();
            this.rootNode = this.nodePool.GetNode();
            this.rootNode.Board = board.ToFastBoard();
            this.rootNode.Move.Turn = board.Turn;
            this.rootNode.Move.Position = 0UL;
            Expand(this.rootNode); ;
        }

        public bool UpdateRoot(Move move)
        {
            if (this.rootNode == null)
                return false;

            this.rootNode.Board.CopyTo(this.BOARD);
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
            if (this.rootNode == null)
                return false;

            var idx = -1;
            for (var i = 0; i < this.rootNode.ChildNodeNum; i++)
                if (this.rootNode.ChildNodes[i].Board.EqualTo(board))
                    idx = i;
            if (idx == -1)
                return false;

            var newRoot = this.rootNode.ChildNodes[idx];
            for (var i = idx; i < this.rootNode.ChildNodeNum - 1; i++)
                this.rootNode.ChildNodes[i] = this.rootNode.ChildNodes[i + 1];
            this.rootNode.ChildNodeNum -= 1;

            DeleteNodes(this.rootNode);
            this.rootNode = newRoot;
            if (this.rootNode.ChildNodeNum == 0)
                Expand(this.rootNode);
            return true;
        }

        public SearchResult Search(int iterationCount, int maxSimulationCount, int timeLimit)
        {
            if (this.rootNode == null)
                throw new NullReferenceException("Root node must be initalized before searching.");

            var startTime = Environment.TickCount;
            var loopCount = 0;
            while (loopCount < iterationCount && this.rootNode.SimulationCount < maxSimulationCount && (Environment.TickCount - startTime) < timeLimit)
            {
                VisitNode(this.rootNode);
                loopCount++;
            }
            return CollectSearchResult();
        }

        SearchResult CollectSearchResult()
        {
            SearchResult result;
            result.RootNodeInfo = new NodeInfo(this.rootNode);
            result.ChildNodesInfo = new NodeInfo[this.rootNode.ChildNodeNum];
            for (var i = 0; i < result.ChildNodesInfo.Length; i++)
                result.ChildNodesInfo[i] = new NodeInfo(this.rootNode.ChildNodes[i]);
            return result;
        }

        void DeleteNodes(Node node)     // 再帰的にノードを破棄
        {
            if (node.ChildNodeNum != 0)
                for (var i = 0; i < node.ChildNodeNum; i++)
                    DeleteNodes(node.ChildNodes[i]);
            node.IsUsed = false;
        }

        float VisitNode(Node node)
        {
            node.SimulationCount++;

            if (node.Type == NodeType.NotDefinite)
            {
                GameResult result;
                if ((result = node.Board.GetResult(node.Board.Turn)) != GameResult.NotEnd)
                {
                    node.FixedScore = GetScore(result);
                    node.Type = NodeType.Terminated;
                    return WIN_SCORE - node.FixedScore;
                }
                else
                    node.Type = NodeType.NotTerminated;
            }

            if (node.Type == NodeType.Terminated)
            {
                UpdateNodeScore(node, node.FixedScore);
                return WIN_SCORE - node.FixedScore;
            }

            float score;
            if (node.ChildNodeNum != 0)
            {
                score = VisitNode(SelectChildNode(node));
                UpdateNodeScore(node, score);
                return WIN_SCORE - score;
            }

            if (node.SimulationCount > this.ExpansionThreshold)
            {
                Expand(node);
                score = VisitNode(SelectChildNode(node));
                UpdateNodeScore(node, score);
                return WIN_SCORE - score;
            }

            score = Rollout(node.Board);
            UpdateNodeScore(node, score);
            return WIN_SCORE - score;
        }

        void Expand(Node node)
        {
            var moveNum = node.Board.GetNextMoves(this.MOVES);
            node.ChildNodeNum = moveNum;
            for (var i = 0; i < moveNum; i++)
            {
                var childNode = this.nodePool.GetNode();
                childNode.Move = this.MOVES[i];
                node.Board.CopyTo(childNode.Board);
                childNode.Board.Update(childNode.Move);
                node.ChildNodes[i] = childNode;
            }
        }

        float Rollout(FastBoard board)
        {
            board.CopyTo(this.BOARD);
            GameResult result;
            while ((result = this.BOARD.GetResult(board.Turn)) == GameResult.NotEnd)
            {
                var moveNum = this.BOARD.GetNextMoves(this.MOVES);
                this.BOARD.Update(this.MOVES[this.RAND.Next((uint)moveNum)]);
            }
            return GetScore(result);
        }

        static Node SelectChildNode(Node parent)
        {
            var simCountSum = parent.SimulationCount - 1;
            var childNodes = parent.ChildNodes;
            var childNodeNum = parent.ChildNodeNum;
            if (childNodeNum == 1 || simCountSum == 0)
                return childNodes[0];

            var sqrt2lnSimCountSum = MathF.Sqrt(2.0f * FastMath.Log(simCountSum));
            var selectedNode = childNodes[0];
            var maxUCBScore = (WIN_SCORE - selectedNode.Value) + sqrt2lnSimCountSum * MathF.Sqrt(1.0f / selectedNode.SimulationCount);
            Node childNode;
            for(var i = 0; i < childNodeNum; i++)
            {
                childNode = childNodes[i];
                if (childNode.SimulationCount == 0)     // シミュレーション回数が0のノードは必ずシミュレーションする.
                    return childNode;

                var ucbScore = (WIN_SCORE - childNode.Value) + sqrt2lnSimCountSum * MathF.Sqrt(1.0f / childNode.SimulationCount);
                if(ucbScore > maxUCBScore)
                {
                    selectedNode = childNode;
                    maxUCBScore = ucbScore;
                }
            }
            return selectedNode;
        }

        static void UpdateNodeScore(Node node, float score)
        {
            node.ScoreSum += score;
            node.Value = node.ScoreSum / node.SimulationCount;
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