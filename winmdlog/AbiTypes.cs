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

    class PredefinedMethod : IAbiMethod
    {
        private string name;
        private string parameters;

        public PredefinedMethod(string name, string parameters)
        {
            this.name = name;
            this.parameters = parameters;
        }

        public string Name
        {
            get
            {
                return this.name;
            }
        }

        public string GetParameters(ReferenceCollector refs)
        {
            return this.parameters;
        }
    }

    class AbiMethod : IAbiMethod
    {
        public AbiMethod(MethodInfo methodInfo)
        {
            this.methodInfo = methodInfo;
        }

        public string Name { get { return this.methodInfo.Name; } }

        public string GetParameters(ReferenceCollector refs)
        {
            List<string> parameters = this.methodInfo.GetParameters().Where(
                parameter => AbiTypeRuntimeClass.IsValidType(parameter.ParameterType)
            ).Select(parameter => {
                AbiTypeRuntimeClass paramType = new AbiTypeRuntimeClass(parameter.ParameterType);
                return (parameter.IsIn ? paramType.GetShortNameAsInParam(refs) : paramType.GetShortNameAsOutParam(refs)) + " " + parameter.Name;
            }).ToList();

            if (methodInfo.ReturnType != null && methodInfo.ReturnType.Name != "Void")
            {
                parameters.Add((new AbiTypeRuntimeClass(methodInfo.ReturnType)).GetShortNameAsOutParam(refs) + " value");
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

        public AbiTypeRuntimeClass EventHandlerType { get { return new AbiTypeRuntimeClass(eventInfo.EventHandlerType); } }
    }

    class AbiProperty
    {
        public AbiProperty(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
        }

        public string Name { get { return this.propertyInfo.Name; } }

        public AbiTypeRuntimeClass PropertyType { get { return new AbiTypeRuntimeClass(this.propertyInfo.PropertyType); } }

        private PropertyInfo propertyInfo;
    }

    class ActivationFactoryAbiInterface : IAbiType
    {
        public bool ImplicitParent {  get { return true; } }

        public AbiEvent[] Events
        {
            get
            {
                return new AbiEvent[] { };
            }
        }

        public IAbiType Factory
        {
            get
            {
                return null;
            }
        }

        public string InspectableClassKind
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsAgile
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IAbiMethod[] Methods
        {
            get
            {
                return new IAbiMethod[] { new PredefinedMethod("ActivateInstance", "_Outptr_ IInspectable** inspectable") };
            }
        }

        public string Namespace
        {
            get
            {
                return "";
            }
        }

        public string NamespaceDefinitionBeginStatement
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string NamespaceDefinitionEndStatement
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool NoInstanceClass
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string ParentHelperClassName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public AbiProperty[] ReadOnlyProperties
        {
            get
            {
                return new AbiProperty[] { };
            }
        }
        
        public AbiProperty[] ReadWriteProperties
        {
            get
            {
                return new AbiProperty[] { };
            }
        }

        public string RuntimeClassName
        {
            get
            {
                return "IActivationFactory";
            }
        }

        public string ShortNameNoTypeParameters
        {
            get
            {
                return "IActivationFactory";
            }
        }

        public string[] GetActivatableClassStatements(ReferenceCollector refs)
        {
            throw new NotImplementedException();
        }

        public IAbiType[] GetFactoryAndStaticInterfaces(ReferenceCollector refs)
        {
            return new IAbiType[] { };
        }

        public string GetFullName(ReferenceCollector refs)
        {
            return "IActivationFactory";
        }

        public IAbiType[] GetParentClasses(ReferenceCollector refs)
        {
            return new IAbiType[] { };
        }

        public string GetShortName(ReferenceCollector refs)
        {
            return "IActivationFactory";
        }

        public string GetShortNameAsInParam(ReferenceCollector refs)
        {
            throw new NotImplementedException();
        }

        public string GetShortNameAsOutParam(ReferenceCollector refs)
        {
            throw new NotImplementedException();
        }
    }

    class AbiTypeRuntimeClassFactory : IAbiType
    {
        public bool ImplicitParent { get { return false; } }

        public string InspectableClassKind
        {
            get
            {
                return this.innerClass.NoInstanceClass ? 
                    "InspectableClassStatic" : 
                    "InspectableClass";
            }
        }
        public IAbiType[] GetFactoryAndStaticInterfaces(ReferenceCollector refs)
        {
            throw new NotImplementedException();
        }

        private AbiTypeRuntimeClass innerClass;

        private string ClassNameSuffix = "Factory";

        public string ParentHelperClassName
        {
            get
            {
                return this.innerClass.IsAgile ? "AgileActivationFactory" : "ActivationFactory";
            }
        }
        public AbiTypeRuntimeClassFactory(AbiTypeRuntimeClass innerClass)
        {
            this.innerClass = innerClass;
        }

        public IAbiType Factory
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool NoInstanceClass
        {
            get
            {
                return false;
            }
        }

        public AbiEvent[] Events
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsAgile
        {
            get
            {
                return false;
            }
        }

        public IAbiMethod[] Methods
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Namespace
        {
            get
            {
                return this.innerClass.Namespace;
            }
        }

        public string NamespaceDefinitionBeginStatement
        {
            get
            {
                return this.innerClass.NamespaceDefinitionBeginStatement;
            }
        }

        public string NamespaceDefinitionEndStatement
        {
            get
            {
                return this.innerClass.NamespaceDefinitionEndStatement;
            }
        }

        public AbiProperty[] ReadOnlyProperties
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public AbiProperty[] ReadWriteProperties
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string RuntimeClassName
        {
            get
            {
                return this.innerClass.RuntimeClassName + ClassNameSuffix;
            }
        }

        public string ShortNameNoTypeParameters
        {
            get
            {
                return this.innerClass.ShortNameNoTypeParameters + ClassNameSuffix;
            }
        }

        public string[] GetActivatableClassStatements(ReferenceCollector refs)
        {
            return new string[] { };
        }

        public string GetFullName(ReferenceCollector refs)
        {
            return this.innerClass.GetFullName(refs) + ClassNameSuffix;
        }

        public IAbiType[] GetParentClasses(ReferenceCollector refs)
        {
            // innerClass's static and activatable interfaces
            var parentInterfaces = this.innerClass.GetFactoryAndStaticInterfaces(refs);
            return parentInterfaces.Concat(new List<IAbiType>() { new ActivationFactoryAbiInterface() }).ToArray();
        }

        public string GetShortName(ReferenceCollector refs)
        {
            return this.innerClass.GetShortName(refs) + ClassNameSuffix;
        }

        public string GetShortNameAsInParam(ReferenceCollector refs)
        {
            return this.innerClass.GetShortNameAsInParam(refs) + ClassNameSuffix;
        }

        public string GetShortNameAsOutParam(ReferenceCollector refs)
        {
            return this.innerClass.GetShortNameAsOutParam(refs) + ClassNameSuffix;
        }
    }

    class AbiTypeRuntimeClass : IAbiType
    {
        public bool ImplicitParent { get { return false; } }

        public string InspectableClassKind
        {
            get
            {
                return "InspectableClass";
            }
        }

        static public bool IsValidType(Type type)
        {
            return UnprojectType(type) != null;
        }

        static public bool IsValidRuntimeClass(Type type)
        {
            return UnprojectType(type) != null &&
                !IsDelegate(type) &&
                !type.IsEnum &&
                !type.IsInterface;
        }

        private bool noInstanceClass;
        public bool NoInstanceClass
        {
            get
            {
                return this.noInstanceClass;
            }
        }

        public AbiTypeRuntimeClass(Type rawType)
        {
            if (rawType.Name.EndsWith("&"))
            {
                rawType = Type.ReflectionOnlyGetType(rawType.AssemblyQualifiedName.Replace("&", ""), true, false);
            }
            this.rawType = rawType;
            this.unprojectedType = UnprojectType(rawType);
            if (this.unprojectedType == null)
            {
                throw new InvalidOperationException();
            }
            this.noInstanceClass = this.unprojectedType.GetInterfaces().Count() == 0;
        }

        public IAbiType[] GetFactoryAndStaticInterfaces(ReferenceCollector refs)
        {
            List<IAbiType> types = new List<IAbiType>();

            try
            {
                var attrs = this.unprojectedType.GetCustomAttributesData();

                types.AddRange(attrs.Where(attr => 
                    attr.AttributeType.FullName == "Windows.Foundation.Metadata.ActivatableAttribute" &&
                    attr.ConstructorArguments.Count() == 3
                ).Select(attr =>
                    new AbiTypeRuntimeClass((Type)attr.ConstructorArguments.First().Value)
                ));

                types.AddRange(attrs.Where(attr =>
                    attr.AttributeType.FullName == "Windows.Foundation.Metadata.StaticAttribute" &&
                    attr.ConstructorArguments.Count() > 0
                ).Select(attr =>
                    new AbiTypeRuntimeClass((Type)attr.ConstructorArguments.First().Value)
                ));
            }
            catch (TypeLoadException e)
            {
                Console.WriteLine("/* Not sure how to fix this... Error: " + e.Message + " " + e.StackTrace + " */");
            }

            return types.ToArray();
        }

        public string ParentHelperClassName
        {
            get
            {
                return "RuntimeClass";
            }
        }

        public IAbiType Factory
        {
            get
            {
                if (GetFactoryAndStaticInterfaces(null).Count() > 0)
                {
                    return new AbiTypeRuntimeClassFactory(this);
                }
                return null;
            }
        }

        private Type rawType;
        private Type unprojectedType;

        private static Type UnprojectType(Type rawType)
        {
            Type type = rawType;
            if (rawType.IsArray)
            {
                type = rawType.GetElementType();
            }
            string fullname = type.Namespace + "." + type.Name;

            switch (fullname)
            {
                case "System.Uri":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.Uri, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.IDisposable":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.IClosable, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Nullable`1":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.IReference`1[[" +
                        type.GenericTypeArguments[0].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.DateTimeOffset":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.DateTime, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.TimeSpan":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.TimeSpan, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Collections.Generic.ICollection`1":
                    type = null;
                    break;
                case "System.Collections.Generic.IEnumerable`1":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.Collections.IIterable`1[[" + 
                        type.GenericTypeArguments[0].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Collections.Generic.KeyValuePair`2":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.Collections.IKeyValuePair`2[[" +
                        type.GenericTypeArguments[0].AssemblyQualifiedName + "],[" +
                        type.GenericTypeArguments[1].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Collections.Generic.IDictionary`2":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.Collections.IMap`2[[" +
                        type.GenericTypeArguments[0].AssemblyQualifiedName + "],[" +
                        type.GenericTypeArguments[1].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Collections.Generic.IReadOnlyDictionary`2":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.Collections.IMapView`2[[" +
                        type.GenericTypeArguments[0].AssemblyQualifiedName + "],[" +
                        type.GenericTypeArguments[1].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Collections.IEnumerable":
                    type = null;
                    break;
                case "System.Collections.Generic.IReadOnlyList`1":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.Collections.IVectorView`1[[" +
                        type.GenericTypeArguments[0].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.Collections.Generic.IReadOnlyCollection`1":
                    type = null;
                    break;
                case "System.Collections.Generic.IList`1":
                    type = Type.ReflectionOnlyGetType(
                        "Windows.Foundation.Collections.IVector`1[[" +
                        type.GenericTypeArguments[0].AssemblyQualifiedName +
                        "]], Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
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
                            return "ActivatableClassWithFactory(" + ShortNameNoTypeParameters + ", " + Factory.GetShortName(refs) + ");";
                        }
                        throw new Exception("Unknown Activatable attribute type.");
                    }).ToArray();
            }
            catch (TypeLoadException e)
            {
                Console.WriteLine("/* Not sure how to fix this... Error: " + e.Message + " " + e.StackTrace + " */");
                return new string[] { };
            }
        }

        private static Dictionary<string, string> rawTypeNameToAbiTypeName = new Dictionary<string, string>()
        {
            { "System.Boolean", "boolean" },
            { "System.Byte", "BYTE" },
            { "System.Char", "WCHAR" },
            { "System.Double", "DOUBLE" },
            { "System.Exception", "HRESULT" },
            { "System.Guid", "GUID" },
            { "System.Int16", "INT16" },
            { "System.Int32", "INT32" },
            { "System.Int64", "INT64" },
            { "System.Object", "IInspectable" },
            { "System.Single", "FLOAT" },
            { "System.String", "HSTRING" },
            { "System.UInt16", "UINT16" },
            { "System.UInt32", "UINT32" },
            { "System.UInt64", "UINT64" },
        };

        private static bool IsDelegate(Type type)
        {
            return type.FullName == "System.Delegate" || 
                (type.BaseType != null && type.FullName != "System.Object" && IsDelegate(type.BaseType));
        }

        private static string GetAbiTypeNameStrict(string rawName)
        {
            string name = null;
            if (rawTypeNameToAbiTypeName.ContainsKey(rawName))
            {
                name = rawTypeNameToAbiTypeName[rawName];
            }
            else if (rawName == "System.EventHandler`1")
            {
                name = "IEventHandler";
            }
            return name;
        }

        private static string GetAbiTypeShortName(Type rawType)
        {
            Type type = rawType;
            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            string rawName = type.Namespace + "." + type.Name;
            string name = GetAbiTypeNameStrict(rawName);

            if (name == null)
            {
                name = RemoveGenericTypeArtifact(type.Name);
                if (!type.Namespace.StartsWith("Windows"))
                {
                    name = type.Namespace.Replace(".", "::") + "::" + name;
                }
            }

            return name;
        }

        private static string GetAbiTypeFullName(Type rawType)
        {
            string name = GetAbiTypeNameStrict(rawType.Namespace + "." + rawType.Name);

            if (name == null)
            {
                name = rawType.Namespace.Replace(".", "::") + "::" + RemoveGenericTypeArtifact(rawType.Name);
            }

            return name;
        }

        public bool IsAgile
        {
            get
            {
                bool agile = false;
                try
                {
                    var attrs = this.unprojectedType.GetCustomAttributesData();
                    return attrs.Where(attr =>
                        attr.AttributeType.FullName == "Windows.Foundation.Metadata.MarshalingBehaviorAttribute"
                    ).Where(attr =>
                        attr.ConstructorArguments.Count > 0 &&
                        attr.ConstructorArguments[0].Value.Equals(2)
                    ).Count() > 0;
                }
                catch (TypeLoadException e)
                {
                    Console.WriteLine("/* Not sure how to fix this... Error: " + e.Message + " " + e.StackTrace + " */");
                }
                return agile;
            }
        }

        public IAbiMethod[] Methods
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

        public string ShortNameNoTypeParameters
        {
            get
            {
                return GetAbiTypeShortName(unprojectedType);
            }
        }

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
                name += "<" + String.Join(", ", type.GenericTypeArguments.Where(
                    typeArg => AbiTypeRuntimeClass.IsValidType(typeArg)
                ).Select(typeArgument => 
                    (new AbiTypeRuntimeClass(typeArgument)).GetShortName(refs)
                )) + ">";
            }

            return name;
        }

        private static bool IgnoreIndirection(Type type)
        {
            string name = type.Namespace + "." + type.Name;
            switch (name)
            {
                case "System.String":
                case "System.Exception":
                    return true;

                default:
                    return false;
            }
        }

        public string GetShortNameAsOutParam(ReferenceCollector refs)
        {
            Type type = this.unprojectedType;
            bool doublePointer = !IgnoreIndirection(type) && (type.IsClass || type.IsInterface || type.IsByRef || type.IsPointer);
            return (doublePointer ? "_Outptr_" : "_Out_") + " " + this.GetShortName(refs) + (doublePointer ? "**" : "*");
        }

        public string GetShortNameAsInParam(ReferenceCollector refs)
        {
            Type type = this.unprojectedType;
            bool pointer = !IgnoreIndirection(type) && (type.IsClass || type.IsInterface || type.IsByRef || type.IsPointer);
            return "_In_ " + this.GetShortName(refs) + (pointer ? "*" : "");
        }

        public IAbiType[] GetParentClasses(ReferenceCollector refs)
        {
            return this.unprojectedType.GetInterfaces().Where(
                tinterface => AbiTypeRuntimeClass.IsValidType(tinterface)
            ).Select(
                tinterface => new AbiTypeRuntimeClass(tinterface)
            ).ToArray();
        }

        public string RuntimeClassName
        {
            get
            {
                return "RuntimeClass_" + (this.unprojectedType.Namespace + "." + this.unprojectedType.Name).Replace(".", "_");
            }
        }

        public string Namespace
        {
            get
            {
                return this.unprojectedType.Namespace.Replace(".", "::");
            }
        }

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

        public string GetFullName(ReferenceCollector refs)
        {
            Type type = unprojectedType;
            string name = GetAbiTypeFullName(type);
            refs.AddReference(type);

            if (type.GenericTypeArguments.Count() > 0)
            {
                name += "<" + String.Join(", ", type.GenericTypeArguments.Where(
                    typeArgument => AbiTypeRuntimeClass.IsValidType(typeArgument)
                ).Select(
                    typeArgument => (new AbiTypeRuntimeClass(typeArgument)).GetFullName(refs)
                )) + ">";
            }

            return name;
        }
    }
}
