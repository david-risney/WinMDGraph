namespace WinMDLog
{
    interface IAbiType
    {
        IAbiType DefaultInterface { get; }
        bool ImplicitParent { get; }
        string InspectableClassKind { get; }
        string ParentHelperClassName { get; }
        IAbiType Factory { get; }
        bool NoInstanceClass { get; }
        AbiEvent[] Events { get; }
        bool IsAgile { get; }
        IAbiMethod[] Methods { get; }
        string Namespace { get; }
        string NamespaceDefinitionBeginStatement { get; }
        string NamespaceDefinitionEndStatement { get; }
        AbiProperty[] ReadOnlyProperties { get; }
        AbiProperty[] ReadWriteProperties { get; }
        string RuntimeClassName { get; }
        string ShortNameNoTypeParameters { get; }

        string[] GetActivatableClassStatements(ReferenceCollector refs);
        string GetFullName(ReferenceCollector refs);
        IAbiType[] GetParentClasses(ReferenceCollector refs);
        string GetShortName(ReferenceCollector refs);
        string GetShortNameAsInParam(ReferenceCollector refs);
        string GetShortNameAsOutParam(ReferenceCollector refs);
        string GetParamPrefix(bool inParam);
        string GetParamSuffix(bool inParam);
        IAbiType[] GetFactoryAndStaticInterfaces(ReferenceCollector refs);
        string GetFullNameAsInParam(ReferenceCollector refs);
        string GetFullNameAsOutParam(ReferenceCollector refs);
    }
}