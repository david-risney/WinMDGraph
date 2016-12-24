using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WinMDLog
{
    class ReferenceCollector
    {
        public ReferenceCollector(string namespaceToIgnore)
        {
            this.namespaceToIgnore = namespaceToIgnore;
        }

        private string namespaceToIgnore = "";
        private List<Type> types = new List<Type>();
        private List<string> includeHeaders = new List<string>();

        public void AddReference(Type type)
        {
            types.Add(type);
        }

        public void AddIncludeHeader(string headerFileName)
        {
            includeHeaders.Add(headerFileName);
        }

        public string[] IncludeStatements
        {
            get
            {
                return types.Where(type => type.Namespace != this.namespaceToIgnore && type.Namespace.StartsWith("Windows")).Select(type =>
                {
                    string[] namespaceParts = type.Namespace.Split('.');
                    return String.Join(".", namespaceParts.Take(2)) + ".h";
                }).Concat(includeHeaders).Distinct().Select(namespaceParts =>
                {
                    return "#include <" + namespaceParts + ">";
                }).ToArray<string>();
            }
        }

        public string[] UsingNamespaceStatements
        {
            get
            {
                return types.Where(type => type.Namespace.Replace(".", "::") != this.namespaceToIgnore && type.Namespace.StartsWith("Windows")).Select(
                    type => "using namespace " + type.Namespace.Replace(".", "::") + ";"
                ).Distinct().ToArray();
            }
        }
    }

    class AbiMethod
    {
        public AbiMethod(MethodInfo methodInfo)
        {
            this.methodInfo = methodInfo;
        }

        public string Name { get { return this.methodInfo.Name; } }

        public string GetParameters(ReferenceCollector refs)
        {
            List<string> parameters = this.methodInfo.GetParameters().Select(parameter =>
            {
                AbiType paramType = new AbiType(parameter.ParameterType);
                return (parameter.IsIn ? paramType.GetShortNameAsInParam(refs) : paramType.GetShortNameAsOutParam(refs)) + " " + parameter.Name;
            }).ToList();

            if (methodInfo.ReturnType != null && methodInfo.ReturnType.Name != "Void")
            {
                parameters.Add((new AbiType(methodInfo.ReturnType)).GetShortNameAsOutParam(refs) + " value");
            }

            return String.Join(", ", parameters);
        }

        private MethodInfo methodInfo;
    }

    class AbiEvent
    {
        public AbiEvent(EventInfo eventInfo)
        {
            this.eventInfo = eventInfo;
        }

        private EventInfo eventInfo;

        public string Name { get { return eventInfo.Name; } }

        public AbiType EventHandlerType { get { return new AbiType(eventInfo.EventHandlerType); } }
    }

    class AbiProperty
    {
        public AbiProperty(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
        }

        public string Name { get { return this.propertyInfo.Name; } }

        public AbiType PropertyType { get { return new AbiType(this.propertyInfo.PropertyType); } }

        private PropertyInfo propertyInfo;
    }

    class AbiType
    {
        public AbiType(Type raAbiType)
        {
            this.raAbiType = raAbiType;
            this.unprojectedType = UnprojectType(raAbiType);
            this.defaultInterface = GetDefaultInterface(this.unprojectedType);
        }

        private Type raAbiType;
        private Type unprojectedType;
        private Type defaultInterface;

        private static Type UnprojectType(Type raAbiType)
        {
            Type type = raAbiType;
            string fullname = type.Namespace + "." + type.Name;

            switch (fullname)
            {
                case "System.Uri":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.Uri, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.IDisposable":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.IClosable, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
            }

            return type;
        }

        public string[] GetActivatableClassStatements(ReferenceCollector refs)
        {
            try
            {
                var attrs = unprojectedType.GetCustomAttributesData();
                return attrs.Where(
                    attr => attr.AttributeType.FullName == "Windows.Foundation.Metadata.ActivatableAttribute"
                ).Select(
                    attr =>
                    {
                        if (attr.ConstructorArguments.Count() == 2)
                        {
                            return "ActivatableClass(" + ShortNameNoTypeParameters + ");";
                        }
                        else if (attr.ConstructorArguments.Count() == 3)
                        {
                            return "ActivatableClassWithFactory(" + ShortNameNoTypeParameters + ", " + (new AbiType((Type)attr.ConstructorArguments.First().Value)).GetShortName(refs) + ");";
                        }
                        throw new Exception("Unknown Activatable attribute type.");
                    }).ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine("/* Error: " + e.Message + " " + e.StackTrace + " */");
                return new string[] { };
            }
        }

        private static Type GetDefaultInterface(Type raAbiType)
        {
            Type defaultInterface = raAbiType;
            if (defaultInterface.IsClass && defaultInterface.Namespace.StartsWith("Windows."))
            {
                defaultInterface = defaultInterface.GetInterfaces()[0];
            }
            return defaultInterface;
        }

        private static string GetAbiTypeNameStrict(string rawName)
        {
            string name = null;
            switch (rawName)
            {
                case "System.Boolean":
                    name = "boolean";
                    break;
                case "System.String":
                    name = "HSTRING";
                    break;
                case "System.Int32":
                    name = "INT32";
                    break;
                case "System.UInt32":
                    name = "UINT32";
                    break;
                case "System.Byte":
                    name = "BYTE";
                    break;
            }
            return name;
        }

        private static string GetAbiTypeShortName(Type raAbiType)
        {
            string name = GetAbiTypeNameStrict(raAbiType.Namespace + "." + raAbiType.Name);

            if (name == null)
            {
                name = RemoveGenericTypeArtifact(raAbiType.Name);
            }

            return name;
        }

        private static string GetAbiTypeFullName(Type raAbiType)
        {
            string name = GetAbiTypeNameStrict(raAbiType.Namespace + "." + raAbiType.Name);

            if (name == null)
            {
                name = raAbiType.Namespace.Replace(".", "::") + "::" + RemoveGenericTypeArtifact(raAbiType.Name);
            }

            return name;
        }

        public AbiMethod[] Methods
        {
            get
            {
                return this.unprojectedType.GetMethods().Where(
                    method => !method.IsSpecialName
                ).Select(
                    method => new AbiMethod(method)
                ).ToArray();
            }
        }

        public AbiEvent[] Events
        {
            get
            {
                return this.unprojectedType.GetEvents().Select(
                    eventInfo => new AbiEvent(eventInfo)
                ).ToArray();
            }
        }

        public AbiProperty[] ReadOnlyProperties
        {
            get
            {
                return this.unprojectedType.GetProperties().Where(
                    property => !property.CanWrite
                ).Select(
                    property => new AbiProperty(property)
                ).ToArray();
            }
        }

        public AbiProperty[] ReadWriteProperties
        {
            get
            {
                return this.unprojectedType.GetProperties().Where(
                    property => property.CanWrite
                ).Select(
                    property => new AbiProperty(property)
                ).ToArray();
            }
        }

        public string ShortNameNoTypeParameters { get { return GetAbiTypeShortName(unprojectedType); } }

        private static string RemoveGenericTypeArtifact(string name)
        {
            return name.Split('`')[0];
        }

        public string GetShortName(ReferenceCollector refs)
        {
            Type type = unprojectedType;
            refs.AddReference(type);
            string name = GetAbiTypeShortName(type);

            if (type.GenericTypeArguments.Count() > 0)
            {
                name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                {
                    return (new AbiType(typeArgument)).GetShortName(refs);
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

        public AbiType[] GetParentClasses(ReferenceCollector refs)
        {
            return this.unprojectedType.GetInterfaces().Select(tinterface => new AbiType(tinterface)).ToArray();
        }

        public string RuntimeClassName
        {
            get
            {
                return "RuntimeClass_" + (this.unprojectedType.Namespace + "." + this.unprojectedType.Name).Replace(".", "_");
            }
        }

        public string Namespace { get { return this.unprojectedType.Namespace.Replace(".", "::"); } }

        public string NamespaceDefinitionBeginStatement
        {
            get
            {
                return String.Join("\n", this.unprojectedType.Namespace.Split('.').Select(tnamespace => "namespace " + tnamespace + "\n{"));
            }
        }

        public string NamespaceDefinitionEndStatement
        {
            get
            {
                return String.Join("\n", this.unprojectedType.Namespace.Split('.').Select(tnamespace => "}"));
            }
        }

        public AbiType DefaultInterface { get { return new AbiType(this.defaultInterface); } }

        public string GetFullName(ReferenceCollector refs)
        {
            Type type = unprojectedType;
            string name = GetAbiTypeFullName(type);
            refs.AddReference(type);

            if (type.GenericTypeArguments.Count() > 0)
            {
                name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                {
                    return (new AbiType(typeArgument)).GetFullName(refs);
                })) + ">";
            }

            return name;
        }
    }
}
