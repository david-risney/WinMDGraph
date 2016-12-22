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
                                throw new ArgumentException("-outPath must be followed by a match pattern");
                            }
                            outPath = args[idx];
                            break;
                        case "-nonAbi":
                            nonAbi = true;
                            break;
                        default:
                            throw new ArgumentException("Unknown parameter " + args[idx]);
                    }
                }

                if (matches.Count == 0)
                {
                    matches.Add(new Regex(".*"));
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
            public bool nonAbi = false;
            public string rawArgs;
        }

        ParsedArgs args;
        Program(ParsedArgs args)
        {
            this.args = args;
        }

        /*
#include header names
using namespaces
class name for class decl and ctor
implemented C++ interface list no namespace with FtmBase depending on marshaling type of class
runtimeclass name for inspectableclass
event helper class type
activatable statements
namespace start statements
namespace end statements

for each implemented interface (C++ interface - default interface of runtime class if runtime class)
interface name with namespace 
for each method
method decls - name, paramerter list
for each event
event decls - name, event handler type
for each read only property
get property decls - name, type as out param
for each read/write property
get/set property decls - name, type as out param, type as in param


$Define(Root,Context=Class)
$includeStatements

$usingStatements

$namespaceStartStatements

class $className WrlFinal :
    public RuntimeClass<
        $parentTypes
    >
{
        InspectableClass($runtimeClassName, BaseTrust);
    public:
        $className();
        HRESULT RuntimeClassInitialize();

        $InterfacePublicDeclarations

    private:
        $InterfacePrivateDeclarations
};

$InterfaceImplementation

$namespaceEndStatements


$Define(InterfacePublicDeclarations,Context=ParentInterface)
// $interfaceFullName
$MethodDeclarations
$EventDeclarations
$ReadOnlyPropertyDeclarations
$ReadWritePropertyDeclarations

$Define(MethodDeclarations,Context=Method)
IFACEMETHOD($methodName)($methodParameters);

$Define(EventDeclarations,Context=Event)
IFACEMETHOD(add_$eventName)(_In_ $eventHandlerType, _Out_ EventHandlerToken* token);
IFACEMETHOD(remove_$eventName)(_In_ EventHandlerToken token);

$Define(ReadOnlyPropertyDeclarations,Context=ReadOnlyProperty)
IFACEMETHOD(get_$propertyName)($propertyTypeAsOutParam value);

$Define(ReadWritePropertyDeclarations,Context=ReadOnlyProperty)
IFACEMETHOD(get_$propertyName)($propertyTypeAsOutParam value);
IFACEMETHOD(put_$propertyName)($propertyTypeAsInParam value);

$Define(InterfacePrivateDeclarations,Context=ParentInterface)
$EventPrivateDeclarations

$Define(EventPrivateDeclarations,Context=Event)
$eventHandlerType $(eventName)Handlers;

$Define(InterfaceImplementation,Context=ParentInterface)
$MethodDefinitions
$EventDefinitions
$ReadOnlyPropertyDefinitions
$ReadWritePropertyDefinitions

$Define(MethodDefitions,Context=Method)
// $interfaceFullName
IFACEMETHODIMP $className::$methodName($methodParameters)
{
    return E_NOTIMPL;
}

$Define(EventDefinitions,Context=Event)
// $interfaceFullName
IFACEMETHODIMP add_$eventName(_In_ $eventHandlerType* handler, _Out_ EventHandlerToken* token)
{
    return $(eventName)Handlers.Add(handler, token);
}

// $interfaceFullName
IFACEMETHODIMP remove_$eventName(_In_ EventHandlerToken token)
{
    return $(eventName)Handlers.Remove(token);
}


 *          */

        class ReferenceCollector
        {
            public ReferenceCollector(string namespaceToIgnore)
            {
                this.namespaceToIgnore = namespaceToIgnore;
            }

            private string namespaceToIgnore = "";
            private List<Type> types = new List<Type>();
            public void AddReference(Type type)
            {
                types.Add(type);
            }

            public string[] IncludeStatements
            {
                get
                {
                    return types.Where(type => type.Namespace != this.namespaceToIgnore).Select(type =>
                    {
                        string[] namespaceParts = type.Namespace.Split('.');
                        return String.Join(".", namespaceParts.Take(2));
                    }).Distinct().Select(namespaceParts =>
                    {
                        return "#include <" + namespaceParts + ".h>";
                    }).ToArray<string>();
                }
            }

            public string[] UsingNamespaceStatements
            {
                get
                {
                    return types.Where(type => type.Namespace != this.namespaceToIgnore).Select(
                        type => "using namespace " + type.Namespace.Replace(".", "::") + ";"
                    ).Distinct().ToArray();
                }
            }
        }

        class WMethod
        {
            public WMethod(MethodInfo methodInfo)
            {
                this.methodInfo = methodInfo;
            }

            public string Name
            {
                get
                {
                    return this.methodInfo.Name;
                }
            }

            public string GetParameters(ReferenceCollector refs)
            {
                List<string> parameters = this.methodInfo.GetParameters().Select(parameter =>
                {
                    WType paramType = new WType(parameter.ParameterType);
                    return (parameter.IsIn ? paramType.GetShortNameAsInParam(refs) : paramType.GetShortNameAsOutParam(refs)) + " " + parameter.Name;
                }).ToList();

                if (methodInfo.ReturnType != null)
                {
                    parameters.Add((new WType(methodInfo.ReturnType)).GetShortNameAsOutParam(refs) + " value");
                }

                return String.Join(", ", parameters);
            }

            private MethodInfo methodInfo;
        }

        class WEvent
        {
            public WEvent(EventInfo eventInfo)
            {
                this.eventInfo = eventInfo;
            }

            private EventInfo eventInfo;

            public string Name
            {
                get
                {
                    return eventInfo.Name;
                }
            }

            public WType EventHandlerType
            {
                get
                {
                    return new WType(eventInfo.EventHandlerType);
                }
            }
        }

        class WProperty
        {
            public WProperty(PropertyInfo propertyInfo)
            {
                this.propertyInfo = propertyInfo;
            }

            public string Name
            {
                get
                {
                    return this.propertyInfo.Name;
                }
            }

            public WType PropertyType
            {
                get
                {
                    return new WType(this.propertyInfo.PropertyType);
                }
            }

            private PropertyInfo propertyInfo;
        }

        class WType
        {
            public WType(Type rawType)
            {
                this.rawType = rawType;
                this.unprojectedType = UnprojectType(rawType);
                this.defaultInterface = GetDefaultInterface(this.unprojectedType);
            }

            private Type rawType;
            private Type unprojectedType;
            private Type defaultInterface;

            private static Type UnprojectType(Type rawType)
            {
                Type type = rawType;
                if (type.Namespace + "." + type.Name == "System.Uri")
                {
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.Uri, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                }

                return type;
            }

            private static Type GetDefaultInterface(Type rawType)
            {
                Type defaultInterface = rawType;
                if (defaultInterface.IsClass && defaultInterface.Namespace.StartsWith("Windows."))
                {
                    defaultInterface = defaultInterface.GetInterfaces()[0];
                }
                return defaultInterface;
            }

            private static string RemapTypeName(string rawTypeName)
            {
                string name = rawTypeName;

                switch (name)
                {
                    case "System::Boolean":
                        name = "boolean";
                        break;
                    case "System::String":
                        name = "HSTRING";
                        break;
                    case "System::Int32":
                        name = "INT32";
                        break;
                }

                return name;
            }

            public WMethod[] Methods
            {
                get
                {
                    return this.unprojectedType.GetMethods().Where(
                        method => !method.IsSpecialName
                    ).Select(
                        method => new WMethod(method)
                    ).ToArray();
                }
            }

            public WEvent[] Events
            {
                get
                {
                    return this.unprojectedType.GetEvents().Select(
                        eventInfo => new WEvent(eventInfo)
                    ).ToArray();
                }
            }

            public WProperty[] ReadOnlyProperties
            {
                get
                {
                    return this.unprojectedType.GetProperties().Where(
                        property => !property.CanWrite
                    ).Select(
                        property => new WProperty(property)
                    ).ToArray();
                }
            }

            public WProperty[] ReadWriteProperties
            {
                get
                {
                    return this.unprojectedType.GetProperties().Where(
                        property => property.CanWrite
                    ).Select(
                        property => new WProperty(property)
                    ).ToArray();
                }
            }

            public string GetShortName(ReferenceCollector refs)
            {
                Type type = unprojectedType;
                string name = type.Name;
                refs.AddReference(type);

                name = RemapTypeName(name);

                if (type.GenericTypeArguments.Count() > 0)
                {
                    name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                    {
                        return (new WType(typeArgument)).GetShortName(refs);
                    })) + ">";
                }

                return name;
            }

            public string GetShortNameAsOutParam(ReferenceCollector refs)
            {
                Type type = this.unprojectedType;
                bool doublePointer = (type.Namespace + "." + type.Name != "System.String") && (type.IsClass || type.IsInterface || type.IsByRef || type.IsPointer);
                return (doublePointer ? "_Outptr_" : "_Out_") + " " + this.GetShortName(refs) + (doublePointer ? "**" : "*");
            }
            public string GetShortNameAsInParam(ReferenceCollector refs)
            {
                Type type = this.unprojectedType;
                bool pointer = (type.Namespace + "." + type.Name != "System.String") && (type.IsClass || type.IsInterface || type.IsByRef || type.IsPointer);
                return "_In_ " + this.GetShortName(refs) + (pointer ? "*" : "");
            }

            public string Namespace
            {
                get
                {
                    return this.unprojectedType.Namespace.Replace(".", "::");
                }
            }

            public WType DefaultInterface
            {
                get
                {
                    return new WType(this.defaultInterface);
                }
            }

            public string GetFullName(ReferenceCollector refs)
            {
                Type type = unprojectedType;
                string name =  this.Namespace + "::" + type.Name;
                refs.AddReference(type);

                name = RemapTypeName(name);

                if (type.GenericTypeArguments.Count() > 0)
                {
                    name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                    {
                        return (new WType(typeArgument)).GetFullName(refs);
                    })) + ">";
                }

                return name;
            }

        }

        static string MethodToParameterList(MethodInfo methodInfo, List<string> namespaceReferences)
        {
            List<string> parameters = methodInfo.GetParameters().Select(parameter => 
                (parameter.IsIn ? TypeToInParamSal(parameter.ParameterType) : TypeToOutParamSal(parameter.ParameterType)) + " " +
                TypeToName(parameter.ParameterType, namespaceReferences, parameter.IsIn ? TypeToNameContext.InParam : TypeToNameContext.OutParam) + " " +
                parameter.Name).ToList();

            if (methodInfo.ReturnType != null)
            {
                parameters.Add(TypeToOutParamSal(methodInfo.ReturnType) + " " + TypeToName(methodInfo.ReturnType, namespaceReferences, TypeToNameContext.OutParam) + " value");
            }

            return String.Join(", ", parameters);
        }


        enum TypeToNameContext
        {
            Type,
            InParam,
            OutParam
        }
        static string TypeToName(Type rawType, List<string> namespaceReferences = null, TypeToNameContext context = TypeToNameContext.Type)
        {
            Type type = RemapType(rawType);
            string name = type.Name;

            if (type.Namespace.StartsWith("Windows.") && namespaceReferences != null)
            {
                namespaceReferences.Add(type.Namespace);
            }
            else
            {
                name = type.Namespace.Replace(".", "::") + "::" + name;
            }

            name = RemapTypeName(name);

            if (type.GenericTypeArguments.Count() > 0)
            {
                name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                    {
                        return TypeToName(typeArgument, namespaceReferences);
                    })) + ">";
            }

            if (context == TypeToNameContext.InParam ||
                context == TypeToNameContext.OutParam)
            {
                if (name != "HSTRING" && (type.IsClass || type.IsInterface || type.IsByRef || type.IsPointer))
                {
                    name += "*";
                }
            }
            if (context == TypeToNameContext.OutParam)
            {
                name += "*";
            }

            return name;
        }

        private List<string> Unique(List<string> original)
        {
            return original.Distinct().ToList();
        }

        static string MethodToDeclaration(MethodInfo method, List<string> references)
        {
            return "        IFACEMETHOD(" + method.Name + ")(" + MethodToParameterList(method, references) + ");";
        }

        void Run()
        {
            List<Type> types = (new WinMDTypes(args.files.ToArray())).Types;
            
            foreach (Type type in types.Where(type => 
                !type.IsInterface && 
                !type.IsEnum &&
                args.matches.Any(regex => regex.IsMatch(type.Namespace + "." + type.Name))
            ))
            {
                List<Type> externalTypes = new List<Type>();
                List<string> sourceHeaderContent = new List<string>();
                List<string> sourceClassContent = new List<string>();
                List<string> references = new List<string>();

                string modifiedNamespace = type.Namespace;
                sourceClassContent.AddRange(modifiedNamespace.Split('.').SelectMany(name => new string[] { "namespace " + name, "{" }));
                sourceClassContent.Add("// " + type.FullName);
                sourceClassContent.Add("class " + type.Name + " WrlFinal :");
                sourceClassContent.Add("    public RuntimeClass<");
                sourceClassContent.AddRange(type.GetInterfaces().Select(tinterface => "        " + TypeToName(tinterface, references) +","));
                sourceClassContent.Add("    >");
                sourceClassContent.Add("{");
                sourceClassContent.Add("        InspectableClass(RuntimeClass_" + (type.Namespace + "." + type.Name).Replace(".", "_") + ", BaseTrust);");
                sourceClassContent.Add("    public:");
                sourceClassContent.Add("        " + type.Name + "();");
                sourceClassContent.Add("        HRESULT RuntimeClassInitialize();");
                sourceClassContent.Add("");
                sourceClassContent.AddRange(type.GetInterfaces().SelectMany(tinterface =>
                    (new string[] {
                                       "        // " + TypeToName(tinterface)
                    }).Concat(tinterface.GetMethods().Where(
                        method => !method.IsSpecialName
                    ).Select(
                        method => MethodToDeclaration(method, references)
                    ).Concat(
                        tinterface.GetProperties().Where(
                            property => property.CanWrite
                        ).SelectMany(
                            property =>
                                new string[] {
                                    MethodToDeclaration(property.GetMethod, references),
                                    MethodToDeclaration(property.SetMethod, references)
                                }
                        )
                    ).Concat(
                        tinterface.GetProperties().Where(
                            property => !property.CanWrite && property.CanRead
                        ).Select(
                            property => MethodToDeclaration(property.GetMethod, references)
                        )
                    ).Concat(
                        tinterface.GetEvents().SelectMany(
                            tevent => new string[] {
                                MethodToDeclaration(tevent.AddMethod, references),
                                MethodToDeclaration(tevent.RemoveMethod, references)
                            }
                        )
                    ).Concat(new string[] { "" })
                )));
                sourceClassContent.Add("    private:");
                sourceClassContent.AddRange(type.GetInterfaces().SelectMany(tinterface =>
                    tinterface.GetEvents().Select(tevent => "        AgileEventSource<" + TypeToName(tevent.EventHandlerType, references) + "> m_" + tevent.Name + "EventHandlers;")
                ));
                sourceClassContent.Add("}");
                sourceClassContent.AddRange(modifiedNamespace.Split('.').Select(name => "}"));

                sourceHeaderContent.AddRange(references.Select(tnamespace => 
                {
                    string[] namespaceParts = tnamespace.Split('.');
                    return String.Join(".", namespaceParts.Take(2));
                }).Distinct().Select(namespaceParts =>
                {
                    return "#include <" + namespaceParts + ".h>";
                }));
                sourceHeaderContent.Add("");
                sourceHeaderContent.AddRange(references.Distinct().Where(
                    tnamespace => tnamespace != type.Namespace
                ).Select(
                    tnamespace => "using namespace " + tnamespace.Replace(".", "::"))
                );
                sourceHeaderContent.Add("");

                if (args.outPath == null)
                {
                    sourceHeaderContent.ForEach(line => Console.WriteLine(line));
                    sourceClassContent.ForEach(line => Console.WriteLine(line));
                }
                else
                {
                    StreamWriter streamWriter = File.CreateText(args.outPath + "\\" + type.Name + ".cpp");
                    
                    sourceHeaderContent.ForEach(line => streamWriter.WriteLine(line));
                    sourceClassContent.ForEach(line => streamWriter.WriteLine(line));

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
                    "WinMDLog (-file [WinMD path]|-match [regex])*\n"
                    + "\t-file [WinMD file path] - Add metadata from the specified WinMD file\n"
                    + "\t-match [WinMD file path] - Include types with matching full name\n"
                    );
            }

            (new Program(parsedArgs)).Run();
        }
    }
}
