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
                rawArgs = String.Join(" ", args);

                for (int idx = 0; idx < args.Length; ++idx)
                {
                    switch (args[idx])
                    {
                        case "-file":
                            ++idx;
                            if (idx >= args.Length)
                            {
                                throw new ArgumentException("-file must be followed by a match pattern");
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
                        case "-verbose":
                            verbose = true;
                            break;
                        default:
                            throw new ArgumentException("Unknown parameter " + args[idx]);
                    }
                }

                if (files.Count == 0)
                {
                    throw new ArgumentException("Must provide at least one -file parameter.");
                }
                if (matches.Count == 0)
                {
                    matches.Add(".*");
                }
            }
            public List<string> files = new List<string>();
            public List<string> matches = new List<string>();
            public bool verbose = false;
            public string rawArgs;
        }

        delegate void LogLineDelegate(string line);

        static void Main(string[] args)
        {
            try
            {
                ParsedArgs parsedArgs = new ParsedArgs(args);
                ConsoleWriteWinMDTypes(
                    parsedArgs,
                    delegate (string line) { Console.WriteLine(line); },
                    delegate (string line)
                    {
                        if (parsedArgs.verbose)
                        {
                            Console.WriteLine(line);
                        }
                    });
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                Console.Error.WriteLine(
                    "WinMDGraph (-file [WinMD path]|-match [regex]|-verbose)*\n"
                    + "\t-file [WinMD file path] - Add metadata from the specified WinMD file\n"
                    + "\t-match [WinMD file path] - Include types with matching full name\n"
                    + "\t-verbose - Include extra verbose info in the output\n"
                    );
            }
        }

        static string EdgeToNamespace(Graph<WinMDTypes.TypeInfo>.Edge edge)
        {
            string ns1 = edge.Start.Context.Namespace;
            string ns2 = edge.End.Context.Namespace;
            return ns1 == ns2 ? ns1 : "";
        }

        static void ConsoleWriteWinMDTypes(ParsedArgs parsedArgs, LogLineDelegate outputWriteLine, LogLineDelegate verboseWriteLine)
        {
            List<Regex> matches = parsedArgs.matches.Select<string, Regex>(matchRaw => new Regex(matchRaw)).ToList();
            WinMDTypes typeStore = new WinMDTypes(parsedArgs.files.ToArray());
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

            outputWriteLine("// Generated by https://github.com/david-risney/WinMDGraph " + parsedArgs.rawArgs);
            verboseWriteLine("// All in graph to start");
            foreach (var node in graph.NodeContextToNode.Values)
            {
                verboseWriteLine("// - " + node.Context);
            }

            verboseWriteLine("// Collapse types that don't match any of the regex but are parameterized types");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node =>
                    (node.Context.FullName == null || !matches.Any(match => match.IsMatch(node.Context.FullName))) &&
                    (node.Context.CorrespondingType != null && node.Context.CorrespondingType.GenericTypeArguments.Length > 0)
            ))
            {
                verboseWriteLine("// - " + node.Context);
                node.RemoveAndCollapseAllEdges();
            }

            verboseWriteLine("// Remove types that don't match any of the regex");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node =>
                    (node.Context.FullName == null || !matches.Any(match => match.IsMatch(node.Context.FullName))) &&
                    (node.Context.CorrespondingType == null || node.Context.CorrespondingType.GenericTypeArguments.Length == 0)
            ))
            {
                verboseWriteLine("// - " + node.Context);
                node.RemoveAndRemoveAllEdges();
            }

            verboseWriteLine("// Collapse non-classes");
            foreach (var node in graph.NodeContextToNode.Values.ToList().Where(
                node => (node.Context.Class == null || node.Context.Class.FullName == null) //&& node.Context.Interface == null
            ))
            {
                verboseWriteLine("// - " + node.Context);
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

            outputWriteLine("digraph {");
            outputWriteLine("\tnode [ fontname = \"Segoe UI\" ];");
            outputWriteLine("\tnode [ shape = \"rectangle\" ];");
            outputWriteLine("\tnode [ fillcolor = \"white\" ];");
            outputWriteLine("\tnode [ color = black ];");
            outputWriteLine("\tnode [ style = filled ];");
            int keyIdx = 0;

            foreach (var key in namespaceToEntries.Keys)
            {
                if (key != "")
                {
                    outputWriteLine("\tsubgraph cluster_" + keyIdx + " {");
                    outputWriteLine("\t\tlabel = \"" + key + "\";");
                    outputWriteLine("\t\tcolor = grey35;");
                    outputWriteLine("\t\tstyle = filled;");
                    outputWriteLine("\t\tfillcolor = grey90;");
                    outputWriteLine("\t\tfontname = \"Segoe UI\";");
                    outputWriteLine("\t\tfontsize = 18;");
                    ++keyIdx;
                }

                foreach (var entry in namespaceToEntries[key])
                {
                    outputWriteLine((key == "" ? "\t" : "\t\t") + entry);
                }

                if (key != "")
                {
                    outputWriteLine("\t}");
                }
            }
            outputWriteLine("}");
        }
    }
}
