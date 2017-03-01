namespace WinMDLog
{
    interface IAbiMethod
    {
        string Name { get; }

        string GetParameters(ReferenceCollector refs, bool commentOutParamNames = false, bool shortNames = true);
    }
}