﻿using System;
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

    class AbiTypeRuntimeClassFactory : IAbiType
    {
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

        public AbiTypeRuntimeClass DefaultInterface
        {
            get
            {
                throw new NotImplementedException();
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

        public AbiMethod[] Methods
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
            return this.innerClass.GetFactoryAndStaticInterfaces(refs);
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
        public AbiTypeRuntimeClass(Type rawType)
        {
            this.rawType = rawType;
            this.unprojectedType = UnprojectType(rawType);
            this.defaultInterface = GetDefaultInterface(this.unprojectedType);
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
            catch (Exception e)
            {
                Console.WriteLine("/* Error: " + e.Message + " " + e.StackTrace + " */");
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
        private Type defaultInterface;

        private static Type UnprojectType(Type rawType)
        {
            Type type = rawType;
            string fullname = type.Namespace + "." + type.Name;

            switch (fullname)
            {
                case "System.Uri":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.Uri, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.IDisposable":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.IClosable, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.DateTimeOffset":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.DateTime, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System.TimeSpan":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.TimeSpan, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
                    break;
                case "System::Collections::Generic::IList":
                    type = Type.ReflectionOnlyGetType("Windows.Foundation.Collections.IVector, Windows.Foundation, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime", true, false);
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
                            return "ActivatableClassWithFactory(" + ShortNameNoTypeParameters + ", " + (new AbiTypeRuntimeClass((Type)attr.ConstructorArguments.First().Value)).GetShortName(refs) + ");";
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

        private static Type GetDefaultInterface(Type rawType)
        {
            Type defaultInterface = rawType;
            if (defaultInterface.IsClass && defaultInterface.Namespace.StartsWith("Windows."))
            {
                try
                {
                    defaultInterface = defaultInterface.GetInterfaces()[0];
                }
                catch (Exception e)
                {
                    Console.WriteLine("// GetDefaultInterface failed for " + rawType.FullName);
                    throw e;
                }
            }
            return defaultInterface;
        }

        private static Dictionary<string, string> rawTypeNameToAbiTypeName = new Dictionary<string, string>(){
            { "System.Boolean", "boolean" },
            { "System.Byte", "BYTE" },
            { "System.Char", "WCHAR" },
            { "System.Double", "DOUBLE" },
            { "System.Guid", "GUID" },
            { "System.Int16", "INT16" },
            { "System.Int32", "INT32" },
            { "System.Int64", "INT64" },
            { "System.Single", "FLOAT" },
            { "System.String", "HSTRING" },
            { "System.UInt16", "UINT16" },
            { "System.UInt32", "UINT32" },
            { "System.UInt64", "UINT64" },
        };

        private static string GetAbiTypeNameStrict(string rawName)
        {
            string name = null;
            if (rawTypeNameToAbiTypeName.ContainsKey(rawName))
            {
                name = rawTypeNameToAbiTypeName[rawName];
            }
            return name;
        }

        private static string GetAbiTypeShortName(Type rawType)
        {
            string name = GetAbiTypeNameStrict(rawType.Namespace + "." + rawType.Name);

            if (name == null)
            {
                name = RemoveGenericTypeArtifact(rawType.Name);
                if (!rawType.Namespace.StartsWith("Windows"))
                {
                    name = rawType.Namespace.Replace(".", "::") + "::" + name;
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
                catch (Exception e)
                {
                    Console.WriteLine("/* Error: " + e.Message + " " + e.StackTrace + " */");
                }
                return agile;
            }
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
                name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                {
                    return (new AbiTypeRuntimeClass(typeArgument)).GetShortName(refs);
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

        public IAbiType[] GetParentClasses(ReferenceCollector refs)
        {
            return this.unprojectedType.GetInterfaces().Select(tinterface => new AbiTypeRuntimeClass(tinterface)).ToArray();
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

        public AbiTypeRuntimeClass DefaultInterface
        {
            get
            {
                return new AbiTypeRuntimeClass(this.defaultInterface);
            }
        }

        public string GetFullName(ReferenceCollector refs)
        {
            Type type = unprojectedType;
            string name = GetAbiTypeFullName(type);
            refs.AddReference(type);

            if (type.GenericTypeArguments.Count() > 0)
            {
                name += "<" + String.Join(", ", type.GenericTypeArguments.Select(typeArgument =>
                {
                    return (new AbiTypeRuntimeClass(typeArgument)).GetFullName(refs);
                })) + ">";
            }

            return name;
        }
    }
}
