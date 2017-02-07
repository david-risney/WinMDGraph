namespace WinMDLog
{
    interface IAbiMethod
    {
        string Name { get; }

        string GetParameters(ReferenceCollector refs);
    }
}