namespace BusTerminal.Api.Authorization;

public enum OperationClass
{
    Read,
    MutateDomain,
    OperatePlatform,
    Administer,
    DeveloperTooling,
}

public static class OperationClassPolicies
{
    public const string CanRead = "CanRead";
    public const string CanMutateDomain = "CanMutateDomain";
    public const string CanOperatePlatform = "CanOperatePlatform";
    public const string CanAdminister = "CanAdminister";
    public const string CanUseDeveloperTooling = "CanUseDeveloperTooling";

    public static string PolicyName(OperationClass operationClass) => operationClass switch
    {
        OperationClass.Read => CanRead,
        OperationClass.MutateDomain => CanMutateDomain,
        OperationClass.OperatePlatform => CanOperatePlatform,
        OperationClass.Administer => CanAdminister,
        OperationClass.DeveloperTooling => CanUseDeveloperTooling,
        _ => throw new ArgumentOutOfRangeException(nameof(operationClass), operationClass, "Unknown OperationClass."),
    };
}
