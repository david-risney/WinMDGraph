using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinMDGraph
{
    class Program
    {
        static string EncodeNameForDot(string raw)
        {
            return raw.Replace('`', '_').Replace('.', '_');
        }

        private class ParsedArgs
        {
            public ParsedArgs(string[] args)
            {
                for (int idx = 0; idx < args.Length; ++idx)
                {
                    switch (args[idx])
                    {
                        case "-file":
                            ++idx;
                            if (idx >= args.Length)
                            {
                                throw new ArgumentException("-match must be followed by a match pattern");
                            }
                            files.Add(args[idx]);
                            break;
                        case "-match":
                            ++idx;
                            if (idx >= args.Length)
                            {
                                throw new ArgumentException("-match must be followed by a match pattern");
                            }
                            matches.Add(args[idx]);
                            break;
                        default:
                            throw new ArgumentException("Unknown parameter " + args[idx]);
                    }
                }
            }
            public List<string> files = new List<string>();
            public List<string> matches = new List<string>();
        }

        static void Main(string[] args)
        {
            ParsedArgs parsedArgs = new ParsedArgs(args);
            ConsoleWriteClassFactory(parsedArgs.files, parsedArgs.matches);
        }

        static Type NodeToType(Graph<WinMDTypes.TypeInfo>.Node node)
        {
            return node.Context.Type;
        }

        static string TypeInfoToFullName(WinMDTypes.TypeInfo typeInfo)
        {
            return typeInfo.Type.FullName;
        }

        static string NodeToNamespace(Graph<WinMDTypes.TypeInfo>.Node node)
        {
            return NodeToType(node).Namespace;
        }

        static string NodeToName(Graph<WinMDTypes.TypeInfo>.Node node)
        {
            return NodeToType(node).Name;
        }

        static string NodeToFullname(Graph<WinMDTypes.TypeInfo>.Node node)
        {
            return NodeToType(node).FullName;
        }

        static string NodeToDebugDescrption(Graph<WinMDTypes.TypeInfo>.Node node)
        {
            return node.Context.ToString();
        }

        static string EdgeToNamespace(Graph<WinMDTypes.TypeInfo>.Edge edge)
        {
            string ns1 = NodeToNamespace(edge.Start);
            string ns2 = NodeToNamespace(edge.End);
            return ns1 == ns2 ? ns1 : "";
        }

        static void ConsoleWriteClassFactory(List<string> files, List<string> matchesRaw)
        {
            List<Regex> matches = matchesRaw.Select<string, Regex>(matchRaw => new Regex(matchRaw)).ToList();
            WinMDTypes typeStore = new WinMDTypes(files.ToArray());
            var graph = typeStore.CreateGraph(typeInfo => {
                string fullName = TypeInfoToFullName(typeInfo);
                Type type = typeInfo.Type;
                bool genericArgs = false;
                if (type != null)
                {
                    genericArgs = type.GenericTypeArguments.Length > 0;
                }

                bool nameMatch = (fullName != null && matches.Any(match => match.IsMatch(fullName)));
                return nameMatch || genericArgs;
            });

            Console.WriteLine("// All in graph to start");
            foreach (var node in graph.NodeContextToNode.Values)
            {
                Console.WriteLine("// - " + NodeToDebugDescrption(node));
            }

            Console.WriteLine("// Collapse types that don't match any of the regex but are parameterized types");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node =>
                    (NodeToFullname(node) == null || !matches.Any(match => match.IsMatch(NodeToFullname(node)))) &&
                    (NodeToType(node) != null && NodeToType(node).GenericTypeArguments.Length > 0)
            ))
            {
                Console.WriteLine("// - " + NodeToDebugDescrption(node));
                node.RemoveAndCollapseAllEdges();
            }

            Console.WriteLine("// Remove types that don't match any of the regex");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node =>
                    (NodeToFullname(node) == null || !matches.Any(match => match.IsMatch(NodeToFullname(node)))) &&
                    (NodeToType(node) == null || NodeToType(node).GenericTypeArguments.Length == 0)
            ))
            {
                Console.WriteLine("// - " + NodeToDebugDescrption(node));
                node.RemoveAndRemoveAllEdges();
            }

            Console.WriteLine("// Collapse non-classes");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node => (node.Context.Class == null || node.Context.Class.FullName == null) //&& node.Context.Interface == null
            ))
            {
                Console.WriteLine("// - " + NodeToDebugDescrption(node));
                node.RemoveAndCollapseAllEdges();
            }

            // Remove self referential edges
            foreach (var edge in graph.Edges.ToList().Where(
                edge => NodeToFullname(edge.Start) == NodeToFullname(edge.End)
            ))
            {
                edge.Remove();
            }

            Dictionary<string, List<string>> namespaceToEntries = new Dictionary<string, List<string>>();

            foreach (var node in graph.NodeContextToNode.Values)
            {
                string ns = NodeToNamespace(node);
                List<string> entries = namespaceToEntries.ContainsKey(ns) ? namespaceToEntries[ns] : (namespaceToEntries[ns] = new List<string>());
                entries.Add(EncodeNameForDot(NodeToName(node)) + ";");
            }

            foreach (var edge in graph.Edges)
            {
                string ns = EdgeToNamespace(edge);
                List<string> entries = namespaceToEntries.ContainsKey(ns) ? namespaceToEntries[ns] : (namespaceToEntries[ns] = new List<string>());
                entries.Add(EncodeNameForDot(NodeToName(edge.Start)) + " -> " + EncodeNameForDot(NodeToName(edge.End)) + ";");
            }

            Console.WriteLine("digraph {");
            Console.WriteLine("\tnode [ fontname = \"Segoe UI\" ];");
            Console.WriteLine("\tnode [ shape = \"rectangle\" ];");
            int keyIdx = 0;

            foreach (var key in namespaceToEntries.Keys)
            {
                if (key != "")
                {
                    Console.WriteLine("\tsubgraph cluster_" + keyIdx + " {");
                    ++keyIdx;
                }

                foreach (var entry in namespaceToEntries[key])
                {
                    Console.WriteLine((key == "" ? "\t" : "\t\t") + entry);
                }

                if (key != "")
                {
                    Console.WriteLine("\t\tlabel = \"" + key + "\";");
                    Console.WriteLine("\t\tcolor = lightgrey;");
                    Console.WriteLine("\t\tfontname = \"Segoe UI\";");
                    Console.WriteLine("\t}");
                }
            }
            Console.WriteLine("}");
        }
    }
}
