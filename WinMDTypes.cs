using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;

namespace WinMDGraph
{
    public class WinMDTypes
    {
        static WinMDTypes()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;
            WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve;
        }

        private static void WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve(object sender, NamespaceResolveEventArgs e)
        {
            string path =
                WindowsRuntimeMetadata.ResolveNamespace(e.NamespaceName, Enumerable.Empty<string>())
                    .FirstOrDefault();

            e.ResolvedAssemblies.Add(Assembly.ReflectionOnlyLoadFrom(path));
        }

        private static Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.ReflectionOnlyLoad(args.Name);
        }

        private List<Type> types;

        public WinMDTypes(string[] assemblyPaths)
        {
            this.types = LoadTypesFromAssemblies(assemblyPaths);
        }

        private Assembly LoadAssembly(string assemblyPath)
        {
            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                try
                {
                    Assembly.ReflectionOnlyLoad(reference.FullName);
                }
                catch (FileNotFoundException)
                {
                    var potentialFileName = String.Format("{0}\\{1}.dll", (new FileInfo(assemblyPath)).DirectoryName, reference.Name);
                    var potentialFileInfo = new FileInfo(potentialFileName);

                    if (potentialFileInfo.Exists)
                    {
                        Assembly.ReflectionOnlyLoadFrom(potentialFileInfo.FullName);
                    }
                }
                catch (FileLoadException)
                {
                }
            }
            return assembly;
        }

        private List<Type> LoadTypesFromAssemblies(string[] assemblyPaths)
        {
            return assemblyPaths.Select(assemblyPath => LoadAssembly(assemblyPath)).SelectMany(assembly => assembly.GetTypes()).ToList();
        }

        public List<Type> Types
        {
            get { return this.types; }
        }

        public class TypeInfo : IEquatable<TypeInfo>, IComparable<TypeInfo>
        {
            public TypeInfo(Type interfaceOrClass)
            {
                if (interfaceOrClass.IsInterface)
                {
                    this.Interface = interfaceOrClass;
                }
                else
                {
                    this.Class = interfaceOrClass;
                }
            }
            public TypeInfo(MethodInfo methodInfo)
            {
                this.Method = methodInfo;
            }
            public TypeInfo(PropertyInfo propertyInfo)
            {
                this.Property = propertyInfo;
            }
            public TypeInfo(EventInfo eventInfo)
            {
                this.Event = eventInfo;
            }
            public Type Interface;
            public Type Class;
            public MethodInfo Method;
            public PropertyInfo Property;
            public EventInfo Event;

            private static string TypeToName(Type type)
            {
                string name = type.Name;
                if (type.GenericTypeArguments.Length > 0)
                {
                    name += "<" + type.GenericTypeArguments.Select(genericType => TypeToName(genericType)) + ">";
                }
                return name;
            }

            public string Name
            {
                get
                {
                    string name = null;
                    if (Interface != null)
                    {
                        name = TypeToName(Interface);
                    }
                    else if (Class != null)
                    {
                        name = TypeToName(Class);
                    }
                    else if (Method != null)
                    {
                        name = Method.Name;
                    }
                    else if (Property != null)
                    {
                        name = Property.Name;
                    }
                    else if (Event != null)
                    {
                        name = Event.Name;
                    }
                    else
                    {
                        throw new Exception("Unexpected kind.");
                    }
                    return name;
                }
            }

            public Type CorrespondingType
            {
                get
                {
                    Type type = null;
                    if (Interface != null)
                    {
                        type = Interface;
                    }
                    else if (Class != null)
                    {
                        type = Class;
                    }
                    else if (Method != null)
                    {
                        type = Method.DeclaringType;
                    }
                    else if (Property != null)
                    {
                        type = Property.DeclaringType;
                    }
                    else if (Event != null)
                    {
                        type = Event.DeclaringType;
                    }
                    else
                    {
                        throw new Exception("Unexpected kind.");
                    }
                    return type;
                }
            }

            public string Namespace
            {
                get
                {
                    return this.CorrespondingType.Namespace;
                }
            }

            public string FullName
            {
                get
                {
                    string className = "";
                    if (Method != null)
                    {
                        className = TypeToName(Method.DeclaringType) + ".";
                    }
                    else if (Property != null)
                    {
                        className = TypeToName(Property.DeclaringType) + ".";
                    }
                    else if (Event != null)
                    {
                        className = TypeToName(Event.DeclaringType) + ".";
                    }
                    return Namespace + "." + className + Name;
                }
            }

            public string KindName
            {
                get
                {
                    if (this.Interface != null)
                    {
                        return "Interface";
                    }
                    else if (this.Class != null)
                    {
                        return "Class";
                    }
                    else if (this.Method != null)
                    {
                        return "Method";
                    }
                    else if (this.Property != null)
                    {
                        return "Property";
                    }
                    else if (this.Event != null)
                    {
                        return "Event";
                    }
                    throw new Exception("Error");
                }
            }

            public override string ToString()
            {
                return KindName + " " + FullName;
            }

            public bool Equals(TypeInfo other)
            {
                return this.ToString() == other.ToString();
            }

            public int CompareTo(TypeInfo other)
            {
                return this.ToString().CompareTo(other.ToString());
            }

            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj is TypeInfo && this.Equals(obj as TypeInfo);
            }

            public List<TypeInfo> GetOutEdges()
            {
                List<TypeInfo> outEdges = new List<TypeInfo>();
                Type type = this.Class;
                if (type == null)
                {
                    type = this.Interface;
                }
                if (type != null)
                {
                    foreach (Type parentInterface in type.GetInterfaces())
                    {
                        outEdges.Add(new TypeInfo(parentInterface));
                    }

                    CustomAttributeData exclusiveTo = type.CustomAttributes.FirstOrDefault<CustomAttributeData>(attribute => attribute.AttributeType.FullName == "Windows.Foundation.Metadata.ExclusiveToAttribute");
                    if (exclusiveTo != null)
                    {
                        Type exclusiveToType = (Type)exclusiveTo.ConstructorArguments[0].Value;
                        outEdges.Add(new TypeInfo(exclusiveToType));
                    }

                    foreach (Type genericType in type.GenericTypeArguments)
                    {
                        outEdges.Add(new TypeInfo(genericType));
                    }

                    foreach (PropertyInfo propertyInfo in type.GetRuntimeProperties())
                    {
                        outEdges.Add(new TypeInfo(propertyInfo));
                    }
                    foreach (MethodInfo methodInfo in type.GetRuntimeMethods())
                    {
                        if (!methodInfo.IsSpecialName)
                        {
                            outEdges.Add(new TypeInfo(methodInfo));
                        }
                    }

                    foreach (EventInfo eventInfo in type.GetRuntimeEvents())
                    {
                        outEdges.Add(new TypeInfo(eventInfo));
                    }

                }

                if (this.Property != null)
                {
                    outEdges.Add(new TypeInfo(this.Property.PropertyType));
                }

                if (this.Method != null)
                {
                    outEdges.Add(new TypeInfo(this.Method.ReturnType));

                    foreach (ParameterInfo parameterInfo in this.Method.GetParameters())
                    {
                        if (parameterInfo.IsOut)
                        {
                            outEdges.Add(new TypeInfo(parameterInfo.GetType()));
                        }
                    }
                }

                if (this.Event != null)
                {
                    foreach (var eventHandlerParamType in this.Event.EventHandlerType.GenericTypeArguments)
                    {
                        outEdges.Add(new TypeInfo(eventHandlerParamType));
                    }
                }
                return outEdges;
            }

            public List<TypeInfo> GetInEdges()
            {
                List<TypeInfo> inEdges = new List<TypeInfo>();
                if (this.Method != null)
                {
                    foreach (ParameterInfo parameterInfo in this.Method.GetParameters())
                    {
                        if (parameterInfo.IsIn)
                        {
                            inEdges.Add(new TypeInfo(parameterInfo.GetType()));
                        }
                    }
                }
                return inEdges;
            }

            public Type Type
            {
                get
                {
                    TypeInfo typeInfo = this;
                    if (typeInfo.Class != null)
                    {
                        return typeInfo.Class;
                    }
                    else if (typeInfo.Interface != null)
                    {
                        return typeInfo.Interface;
                    }
                    else if (typeInfo.Event != null)
                    {
                        return typeInfo.Event.DeclaringType;
                    }
                    else if (typeInfo.Method != null)
                    {
                        return typeInfo.Method.DeclaringType;
                    }
                    else if (typeInfo.Property != null)
                    {
                        return typeInfo.Property.DeclaringType;
                    }
                    throw new Exception("What?!");
                }
            }
        }

        public delegate bool TypeInfoFilter(TypeInfo typeInfo);

        public Graph<TypeInfo> CreateGraph(TypeInfoFilter filter)
        {
            Graph<TypeInfo> graph = new Graph<TypeInfo>();

            HashSet<TypeInfo> nextFrontier = new HashSet<TypeInfo>(Types.Select(type => new TypeInfo(type)));
            HashSet<TypeInfo> visited = new HashSet<TypeInfo>();
            HashSet<TypeInfo> frontier = new HashSet<TypeInfo>();

            while (nextFrontier.Count > 0)
            {
                frontier = new HashSet<TypeInfo>(nextFrontier.Where(
                    entry => !visited.Contains(entry)
                ).Where(
                    entry => filter(entry)));

                nextFrontier = new HashSet<TypeInfo>();

                foreach (TypeInfo typeInfo in frontier)
                {
                    visited.Add(typeInfo);
                    Graph<TypeInfo>.Node nodeForType = graph.GetOrAddNode(typeInfo);

                    foreach (TypeInfo outNode in typeInfo.GetOutEdges())
                    {
                        nextFrontier.Add(outNode);
                        Graph<TypeInfo>.Node outNodeInfo = graph.GetOrAddNode(outNode);
                        graph.AddEdge(nodeForType, outNodeInfo);
                    }

                    foreach (TypeInfo inNode in typeInfo.GetInEdges())
                    {
                        nextFrontier.Add(inNode);
                        Graph<TypeInfo>.Node inNodeInfo = graph.GetOrAddNode(inNode);
                        graph.AddEdge(inNodeInfo, nodeForType);
                    }
                }
            }

            return graph;
        }
    }
}
