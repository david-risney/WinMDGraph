using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinMD;

namespace WinMDLog
{
    class Program
    {
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
                            matches.Add(new Regex(args[idx]));
                            break;
                        case "-outPath":
                            ++idx;
                            if (idx >= args.Length)
                            {
                                throw new ArgumentException("-outPath must be followed by a path");
                            }
                            outPath = args[idx];
                            break;
                        default:
                            throw new ArgumentException("Unknown parameter " + args[idx]);
                    }
                }

                if (matches.Count == 0)
                {
                    throw new ArgumentException("Must specify at least one -match parameter.");
                }

                if (outPath != null)
                {
                    if (!Directory.Exists(outPath))
                    {
                        Directory.CreateDirectory(outPath);
                    }
                }
            }
            public List<string> files = new List<string>();
            public List<Regex> matches = new List<Regex>();
            public string outPath = null;
            public string rawArgs;
        }

        ParsedArgs args;
        Program(ParsedArgs args)
        {
            this.args = args;
        }

        struct FileOutput
        {
            public FileOutput(string name, string content)
            {
                this.Name = name;
                this.Content = content;
            }
            public string Name;
            public string Content;
        }

        void Run()
        {
            string[] filesExpanded = args.files.SelectMany<string, string>(path =>
            {
                if (Directory.Exists(path))
                {
                    return Directory.EnumerateFiles(path);
                }
                else
                {
                    return new string[] { path };
                }
            }).ToArray();
            List<Type> allTypes = (new WinMDTypes(filesExpanded)).Types;

            var types = allTypes.Where(type =>
                AbiTypeRuntimeClass.IsValidRuntimeClass(type) &&
                args.matches.Any(regex => regex.IsMatch(type.Namespace + "." + type.Name))
            ).Select(rawType =>
                new AbiTypeRuntimeClass(rawType)
            ).Where(abiType => !abiType.NoInstanceClass);

            var typeFactories = allTypes.Where(type =>
                AbiTypeRuntimeClass.IsValidRuntimeClass(type) &&
                args.matches.Any(regex => regex.IsMatch(type.Namespace + "." + type.Name))
            ).Select(
                rawType => new AbiTypeRuntimeClass(rawType)
            ).Where(
                abiType => abiType.Factory != null
            ).Select(
                abiType => abiType.Factory
            );

            var results = ProcessTypes(types.ToList<IAbiType>()).Concat(
                ProcessTypes(typeFactories.ToList<IAbiType>())).
                ToList<FileOutput>();
            
            results.Sort(delegate(FileOutput left, FileOutput right)
            {
                return left.Name.CompareTo(right.Name);
            });

            foreach (var result in results)
            {
                if (args.outPath == null)
                {
                    Console.WriteLine("// " + result.Name);
                    Console.WriteLine(result.Content);
                    Console.WriteLine();
                }
                else
                {
                    StreamWriter streamWriter = File.CreateText(args.outPath + "\\" + result.Name);
                    streamWriter.WriteLine(result.Content);

                    streamWriter.Flush();
                    streamWriter.Close();
                }
            }
        }

        List<FileOutput> ProcessTypes(List<IAbiType> types)
        {
            List<FileOutput> results = new List<FileOutput>();
            foreach (IAbiType type in types)
            {
                const string headerTemplate =
@"$headerIncludeStatements
#include <wrl/implements.h>

$namespaceDefinitionBegin
class $className WrlFinal : $parentHelperClass<
$parentClasses
    >
{
    $inspectableClassKind($runtimeclassStringName, BaseTrust);

public:
    $className();
    HRESULT RuntimeClassInitialize();

$interfaceImplementationDeclarations
private:
$eventHelperDeclaration
};
$activatableClassStatements

$namespaceDefinitionEnd";

                results = results.Concat(ProcessType(headerTemplate, type, false).Select(fileOutput =>
                {
                    fileOutput.Name += ".h";
                    return fileOutput;
                })).ToList<FileOutput>();

                const string sourceTemplate =
@"$sourceIncludeStatements

using namespace Microsoft::WRL;
$usingNamespaceStatements

$namespaceDefinitionBegin
$interfaceImplementationDefinitions
$namespaceDefinitionEnd";

                results = results.Concat(ProcessType(sourceTemplate, type, true).Select(fileOutput => 
                {
                    fileOutput.Name += ".cpp";
                    return fileOutput;
                })).ToList<FileOutput>();
            }

            return results;
        }

        List<FileOutput> ProcessType(string rootTemplate, IAbiType type, bool shortNames)
        {
            List<FileOutput> results = new List<FileOutput>();
            ReferenceCollector refs = new ReferenceCollector(type.Namespace);
            string result = rootTemplate.
                Replace("$namespaceDefinitionBegin", type.NamespaceDefinitionBeginStatement).
                Replace("$namespaceDefinitionEnd", type.NamespaceDefinitionEndStatement).
                Replace("$className", type.ShortNameNoTypeParameters).
                Replace("$runtimeclassStringName", type.RuntimeClassName).
                Replace("$parentHelperClass", type.ParentHelperClassName).
                Replace("$inspectableClassKind", type.InspectableClassKind).
                Replace("$activatableClassStatements", String.Join(Environment.NewLine, type.GetActivatableClassStatements(refs)));

            var parentClasses = type.GetParentClasses(refs).
                Where(parent => !parent.ImplicitParent).
                Select(parent => "    " + (shortNames ? parent.GetShortName(refs) : parent.GetFullName(refs)));

            if (type.IsAgile)
            {
                parentClasses = new string[] { "    FtmBase" }.Concat(parentClasses);
            }

            result = result.
                Replace("$parentClasses", String.Join("," + Environment.NewLine, parentClasses));

            result = result.Replace("$interfaceImplementationDeclarations",
                String.Join(Environment.NewLine, type.GetParentClasses(refs).Select(tinterface =>
                {
                    string[] header = new string[] { "    // " + tinterface.GetFullName(refs) };
                    string[] tail = new string[] { "" };

                    IEnumerable<string> methods = tinterface.Methods.SelectMany(method => new string[] {
                            "    IFACEMETHOD(" + method.Name +")(" + method.GetParameters(refs, false, shortNames) + ");"
                    });

                    IEnumerable<string> events = tinterface.Events.SelectMany(tevent => new string[] {
                            "    IFACEMETHOD(add_" + tevent.Name + ")(" + (shortNames ? tevent.EventHandlerType.GetShortNameAsInParam(refs) : tevent.EventHandlerType.GetFullNameAsInParam(refs)) + " eventHandler, _Out_ EventRegistrationToken* token);",
                            "    IFACEMETHOD(remove_" + tevent.Name + ")(_In_ EventRegistrationToken token);",
                    });

                    IEnumerable<string> readOnlyProperties = tinterface.ReadOnlyProperties.SelectMany(property => new string[] {
                            "    IFACEMETHOD(get_" + property.Name + ")(" + (shortNames ? property.PropertyType.GetShortNameAsOutParam(refs) : property.PropertyType.GetFullNameAsOutParam(refs)) + " value);"
                    });

                    IEnumerable<string> readWriteProperties = tinterface.ReadWriteProperties.SelectMany(property => new string[] {
                            "    IFACEMETHOD(get_" + property.Name + ")(" + (shortNames ? property.PropertyType.GetShortNameAsOutParam(refs) : property.PropertyType.GetFullNameAsOutParam(refs)) + " value);",
                            "    IFACEMETHOD(put_" + property.Name + ")(" + (shortNames ? property.PropertyType.GetShortNameAsInParam(refs) : property.PropertyType.GetFullNameAsInParam(refs)) + " value);"
                    });

                    return String.Join(Environment.NewLine, header.Concat(methods).Concat(readOnlyProperties).Concat(readWriteProperties).Concat(events).Concat(tail));
                })));

            result = result.Replace("$interfaceImplementationDefinitions",
                String.Join(Environment.NewLine, type.GetParentClasses(refs).Select(tinterface =>
                {
                    string header = "// " + tinterface.GetFullName(refs);

                    IEnumerable<string> methods = tinterface.Methods.SelectMany(method => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::" + method.Name + "(" + method.GetParameters(refs, false, shortNames) + ")",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            ""
                    });

                    IEnumerable<string> events = tinterface.Events.SelectMany(tevent => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::add_" + tevent.Name + "(" + (shortNames ? tevent.EventHandlerType.GetShortNameAsInParam(refs) : tevent.EventHandlerType.GetFullNameAsInParam(refs)) + " eventHandler, _Out_ EventRegistrationToken* token)",
                            "{",
                            "    return m_" + tevent.Name + "EventHandlers.Add(eventHandler, token);",
                            "}",
                            "",
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::remove_" + tevent.Name + "(_In_ EventRegistrationToken token)",
                            "{",
                            "    return m_" + tevent.Name + "EventHandlers.Remove(token);",
                            "}",
                            ""
                    });

                    IEnumerable<string> readOnlyProperties = tinterface.ReadOnlyProperties.SelectMany(property => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::get_" + property.Name + "(" + (shortNames ? property.PropertyType.GetShortNameAsOutParam(refs) : property.PropertyType.GetFullNameAsOutParam(refs)) + " /* value */)",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            ""
                    });

                    IEnumerable<string> readWriteProperties = tinterface.ReadWriteProperties.SelectMany(property => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::get_" + property.Name + "(" + (shortNames ? property.PropertyType.GetShortNameAsOutParam(refs) : property.PropertyType.GetFullNameAsOutParam(refs)) + " /* value */)",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            "",
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::put_" + property.Name + "(" + (shortNames ? property.PropertyType.GetShortNameAsInParam(refs) : property.PropertyType.GetFullNameAsInParam(refs)) + " /* value */)",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            ""
                    });

                    return String.Join(Environment.NewLine, methods.Concat(events).Concat(readOnlyProperties).Concat(readWriteProperties));
                })));

            result = result.Replace("$eventHelperDeclaration",
                String.Join(Environment.NewLine, type.GetParentClasses(refs).SelectMany(
                    tinterface => tinterface.Events
                ).Select(
                    tevent => "    AgileEventSource<" + (shortNames ? tevent.EventHandlerType.GetShortName(refs) : tevent.EventHandlerType.GetFullName(refs)) + "> m_" + tevent.Name + "EventHandlers;"
                )));

            // Do these last once refs has been fully populated.

            var headerIncludeStatements = refs.IncludeStatements;
            if (type.Factory != null)
            {
                headerIncludeStatements = headerIncludeStatements.Concat(new string[] { "#include \"" + type.Factory.ShortNameNoTypeParameters + ".h\"" }).ToArray();
            }

            result = result.
                Replace("$headerIncludeStatements", String.Join(Environment.NewLine, headerIncludeStatements)).
                Replace("$sourceIncludeStatements", "#include \"" + type.ShortNameNoTypeParameters + ".h\"").
                Replace("$usingNamespaceStatements", String.Join(Environment.NewLine, refs.UsingNamespaceStatements));

            results.Add(new FileOutput(type.ShortNameNoTypeParameters, result));

            return results;
        }

        static void Main(string[] args)
        {
            ParsedArgs parsedArgs = null;

            try
            {
                parsedArgs = new ParsedArgs(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
                Console.Error.WriteLine(
                    "WinMDLog (-file [WinMD path]|-match [regex]|-outPath [output directory])*" + Environment.NewLine
                    + "\t-file [WinMD file path] - Add metadata from the specified WinMD file" + Environment.NewLine
                    + "\t-match [WinMD file path] - Process runtime classes with matching full name" + Environment.NewLine
                    + "\t-outPath [output directory] - Write runtime classes into individual files in that path" + Environment.NewLine
                    );
            }

            if (parsedArgs != null)
            {
                (new Program(parsedArgs)).Run();
            }
        }
    }
}
