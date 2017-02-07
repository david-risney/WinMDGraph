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

        void Run()
        {
            var types = (new WinMDTypes(args.files.ToArray())).Types.Where(type =>
                type.IsInterface &&
                AbiTypeRuntimeClass.IsValidType(type) &&
                args.matches.Any(regex => regex.IsMatch(type.Namespace + "." + type.Name))
            ).SelectMany(rawType => {
                List<IAbiType> typeAndFactory = new List<IAbiType>();
                IAbiType type = new AbiTypeRuntimeClass(rawType);
                typeAndFactory.Add(type);
                if (type.Factory != null)
                {
                    typeAndFactory.Add(type.Factory);
                }
                return typeAndFactory;
            }).Where(abiType => !abiType.NoInstanceClass);
            
            foreach (IAbiType type in types)
            {
                ReferenceCollector refs = new ReferenceCollector(type.Namespace);

                const string rootTemplate =
@"$includeStatements
#include <wrl/implements.h>

using namespace Microsoft::WRL;
$usingNamespaceStatements

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

$interfaceImplementationDefinitions
$namespaceDefinitionEnd";

                string result = rootTemplate.
                    Replace("$namespaceDefinitionBegin", type.NamespaceDefinitionBeginStatement).
                    Replace("$namespaceDefinitionEnd", type.NamespaceDefinitionEndStatement).
                    Replace("$className", type.ShortNameNoTypeParameters).
                    Replace("$runtimeclassStringName", type.RuntimeClassName).
                    Replace("$parentHelperClass", type.ParentHelperClassName).
                    Replace("$inspectableClassKind", type.InspectableClassKind).
                    Replace("$activatableClassStatements", String.Join("\n", type.GetActivatableClassStatements(refs)));

                var parentClasses = type.GetParentClasses(refs).
                    Where(parent => !parent.ImplicitParent).
                    Select(parent => "    " + parent.GetShortName(refs));

                if (type.IsAgile)
                {
                    parentClasses = new string[] { "    FtmBase" }.Concat(parentClasses);
                }

                result = result.
                    Replace("$parentClasses", String.Join(",\n", parentClasses));

                result = result.Replace("$interfaceImplementationDeclarations",
                    String.Join("\n", type.GetParentClasses(refs).Select(tinterface =>
                    {
                        string[] header = new string[] { "    // " + tinterface.GetFullName(refs) };
                        string[] tail = new string[] { "" };

                        IEnumerable<string> methods = tinterface.Methods.SelectMany(method => new string[] {
                            "    IFACEMETHOD(" + method.Name +")(" + method.GetParameters(refs) + ");"
                        });

                        IEnumerable<string> events = tinterface.Events.SelectMany(tevent => new string[] {
                            "    IFACEMETHOD(add_" + tevent.Name + ")(" + tevent.EventHandlerType.GetShortNameAsInParam(refs) + " eventHandler, _Out_ EventRegistrationToken* token);",
                            "    IFACEMETHOD(remove_" + tevent.Name + ")(_In_ EventRegistrationToken token);",
                        });

                        IEnumerable<string> readOnlyProperties = tinterface.ReadOnlyProperties.SelectMany(property => new string[] {
                            "    IFACEMETHOD(get_" + property.Name + ")(" + property.PropertyType.GetShortNameAsOutParam(refs) + " value);"
                        });

                        IEnumerable<string> readWriteProperties = tinterface.ReadWriteProperties.SelectMany(property => new string[] {
                            "    IFACEMETHOD(get_" + property.Name + ")(" + property.PropertyType.GetShortNameAsOutParam(refs) + " value);",
                            "    IFACEMETHOD(put_" + property.Name + ")(" + property.PropertyType.GetShortNameAsInParam(refs) + " value);"
                        });

                        return String.Join("\n", header.Concat(methods).Concat(readOnlyProperties).Concat(readWriteProperties).Concat(events).Concat(tail));
                    })));

                result = result.Replace("$interfaceImplementationDefinitions",
                    String.Join("\n", type.GetParentClasses(refs).Select(tinterface =>
                    {
                        string header = "// " + tinterface.GetFullName(refs);

                        IEnumerable<string> methods = tinterface.Methods.SelectMany(method => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::" + method.Name + "(" + method.GetParameters(refs) + ")",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            ""
                        });

                        IEnumerable<string> events = tinterface.Events.SelectMany(tevent => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::add_" + tevent.Name + "(" + tevent.EventHandlerType.GetShortNameAsInParam(refs) + " eventHandler, _Out_ EventRegistrationToken* token)",
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
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::get_" + property.Name + "(" + property.PropertyType.GetShortNameAsOutParam(refs) + " value)",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            ""
                        });

                        IEnumerable<string> readWriteProperties = tinterface.ReadWriteProperties.SelectMany(property => new string[] {
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::get_" + property.Name + "(" + property.PropertyType.GetShortNameAsOutParam(refs) + " value)",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            "",
                            header,
                            "IFACEMETHODIMP " + type.ShortNameNoTypeParameters + "::put_" + property.Name + "(" + property.PropertyType.GetShortNameAsInParam(refs) + " value)",
                            "{",
                            "    return E_NOTIMPL;",
                            "}",
                            ""
                        });

                        return String.Join("\n", methods.Concat(events).Concat(readOnlyProperties).Concat(readWriteProperties));
                    })));

                result = result.Replace("$eventHelperDeclaration", 
                    String.Join("\n", type.GetParentClasses(refs).SelectMany(
                        tinterface => tinterface.Events
                    ).Select(
                        tevent => "    AgileEventSource<" + tevent.EventHandlerType.GetShortName(refs) + "> m_" + tevent.Name + "EventHandlers;"
                    )));

                // Do these last once refs has been fully populated.
                result = result.
                    Replace("$includeStatements", String.Join("\n", refs.IncludeStatements)).
                    Replace("$usingNamespaceStatements", String.Join("\n", refs.UsingNamespaceStatements));

                if (args.outPath == null)
                {
                    Console.WriteLine(result);
                }
                else
                {
                    StreamWriter streamWriter = File.CreateText(args.outPath + "\\" + type.ShortNameNoTypeParameters + ".cpp");
                    streamWriter.WriteLine(result);

                    streamWriter.Flush();
                    streamWriter.Close();
                }
            }
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
                    "WinMDLog (-file [WinMD path]|-match [regex]|-outPath [output directory])*\n"
                    + "\t-file [WinMD file path] - Add metadata from the specified WinMD file\n"
                    + "\t-match [WinMD file path] - Process runtime classes with matching full name\n"
                    + "\t-outPath [output directory] - Write runtime classes into individual files in that path\n"
                    );
            }

            (new Program(parsedArgs)).Run();
        }
    }
}
