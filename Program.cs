﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinMDGraph
{
    class Program
    {
        static string EncodeNameForDot(string raw)
        {
            string result = "";
            for (int idx = 0; idx < raw.Length; ++idx)
            {
                char c = raw[idx];
                if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_')
                {
                    result += c;
                }
                else 
                {
                    result += "_";
                }
            }
            return result;
        }

        private class ParsedArgs
        {
            public ParsedArgs(string[] args)
            {
                originalText = String.Join(" ", args);

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
            public string originalText;
        }

        static void Main(string[] args)
        {
            ParsedArgs parsedArgs = new ParsedArgs(args);
            ConsoleWriteWinMDTypes(parsedArgs.files, parsedArgs.matches);
        }

        static string EdgeToNamespace(Graph<WinMDTypes.TypeInfo>.Edge edge)
        {
            string ns1 = edge.Start.Context.Namespace;
            string ns2 = edge.End.Context.Namespace;
            return ns1 == ns2 ? ns1 : "";
        }

        static void ConsoleWriteWinMDTypes(List<string> files, List<string> matchesRaw)
        {
            List<Regex> matches = matchesRaw.Select<string, Regex>(matchRaw => new Regex(matchRaw)).ToList();
            WinMDTypes typeStore = new WinMDTypes(files.ToArray());
            var graph = typeStore.CreateGraph(typeInfo => {
                string fullName = typeInfo.FullName;
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
                Console.WriteLine("// - " + node.Context);
            }

            Console.WriteLine("// Collapse types that don't match any of the regex but are parameterized types");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node =>
                    (node.Context.FullName == null || !matches.Any(match => match.IsMatch(node.Context.FullName))) &&
                    (node.Context.CorrespondingType != null && node.Context.CorrespondingType.GenericTypeArguments.Length > 0)
            ))
            {
                Console.WriteLine("// - " + node.Context);
                node.RemoveAndCollapseAllEdges();
            }

            Console.WriteLine("// Remove types that don't match any of the regex");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node =>
                    (node.Context.FullName == null || !matches.Any(match => match.IsMatch(node.Context.FullName))) &&
                    (node.Context.CorrespondingType == null || node.Context.CorrespondingType.GenericTypeArguments.Length == 0)
            ))
            {
                Console.WriteLine("// - " + node.Context);
                node.RemoveAndRemoveAllEdges();
            }

            Console.WriteLine("// Collapse non-classes");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node => (node.Context.Class == null || node.Context.Class.FullName == null) //&& node.Context.Interface == null
            ))
            {
                Console.WriteLine("// - " + node.Context);
                node.RemoveAndCollapseAllEdges();
            }

            // Remove self referential edges
            foreach (var edge in graph.Edges.ToList().Where(
                edge => edge.Start.Context.FullName == edge.End.Context.FullName
            ))
            {
                edge.Remove();
            }

            Dictionary<string, List<string>> namespaceToEntries = new Dictionary<string, List<string>>();

            foreach (var node in graph.NodeContextToNode.Values)
            {
                string ns = node.Context.Namespace;
                List<string> entries = namespaceToEntries.ContainsKey(ns) ? namespaceToEntries[ns] : (namespaceToEntries[ns] = new List<string>());
                entries.Add(EncodeNameForDot(node.Context.Name) + ";");
            }

            foreach (var edge in graph.Edges)
            {
                string ns = EdgeToNamespace(edge);
                List<string> entries = namespaceToEntries.ContainsKey(ns) ? namespaceToEntries[ns] : (namespaceToEntries[ns] = new List<string>());
                entries.Add(EncodeNameForDot(edge.Start.Context.Name) + " -> " + EncodeNameForDot(edge.End.Context.Name) + ";");
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
