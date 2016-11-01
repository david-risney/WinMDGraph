using System;
using System.Collections.Generic;
using System.Linq;

namespace WinMDGraph
{
    public class Graph<NodeContext>
    {
        public class Edge : IEquatable<Edge>, IComparable<Edge>
        {
            public Edge(Graph<NodeContext> parent, Node startIn, Node endIn)
            {
                Parent = parent;
                _Start = startIn;
                _End = endIn;

                // Make sure this member data is all setup before sending out references to this.
                // If this isn't setup then ToString, Equals, etc won't work.
                _Start.OutEdges.Add(this);
                _End.InEdges.Add(this);
            }
            private Graph<NodeContext> Parent;
            private Node _Start;
            public Node Start
            {
                get
                {
                    return _Start;
                }
            }
            private Node _End;
            public Node End
            {
                get
                {
                    return _End;
                }
            }

            public void Remove()
            {
                _Start.OutEdges.Remove(this);
                _End.InEdges.Remove(this);
                Parent.Edges.Remove(this);
            }

            public bool Equals(Edge other)
            {
                return this.ToString() == other.ToString();
            }

            public override bool Equals(object obj)
            {
                return obj is Edge && this.Equals((Edge)obj);
            }
            public int CompareTo(Edge other)
            {
                return this.ToString().CompareTo(other.ToString());
            }

            public override string ToString()
            {
                return "Edge(" + this.Start.ToString() + "," + this.End.ToString() + ")";
            }

            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }
        }

        public class Node : IEquatable<Node>, IComparable<Node>
        {
            private Graph<NodeContext> Parent;
            public Node(Graph<NodeContext> parent)
            {
                Parent = parent;
            }
            public HashSet<Edge> OutEdges = new HashSet<Edge>();
            public HashSet<Edge> InEdges = new HashSet<Edge>();
            public NodeContext Context;
            public void RemoveAndCollapseAllEdges()
            {
                foreach (Edge outEdge in OutEdges.ToList())
                {
                    foreach (Edge inEdge in InEdges.ToList())
                    {
                        // outEdge this -> other
                        // inEdge other -> this
                        Parent.AddEdge(inEdge.Start, outEdge.End);
                    }
                }
                RemoveAndRemoveAllEdges();
            }

            public void RemoveAndRemoveAllEdges()
            {
                OutEdges.ToList().ForEach(edge => edge.Remove());
                InEdges.ToList().ForEach(edge => edge.Remove());
                Parent.NodeContextToNode.Remove(this.Context);
            }

            public bool Equals(Node other)
            {
                return this.ToString() == other.ToString();
            }

            public override bool Equals(object obj)
            {
                return obj is Node && this.Equals(((Node)obj));
            }

            public override string ToString()
            {
                return "Node(" + Context.ToString() + ")";
            }

            public int CompareTo(Node other)
            {
                return this.ToString().CompareTo(other.ToString());
            }

            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }
        }

        public Node GetOrAddNode(NodeContext context)
        {
            if (NodeContextToNode.ContainsKey(context))
            {
                return NodeContextToNode[context];
            }
            else
            {
                Node node = new Node(this);
                node.Context = context;
                NodeContextToNode[context] = node;
                return node;
            }
        }

        public Edge AddEdge(Node start, Node end)
        {
            Edge edge = new Edge(this, start, end);
            Edges.Add(edge);
            return edge;
        }

        public HashSet<Edge> Edges = new HashSet<Edge>();
        public Dictionary<NodeContext, Node> NodeContextToNode = new Dictionary<NodeContext, Node>();
    }
}
