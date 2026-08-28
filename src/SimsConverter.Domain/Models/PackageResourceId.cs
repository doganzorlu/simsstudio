namespace SimsConverter.Domain.Models;

public record PackageResourceId(
    uint TypeId,
    uint GroupId,
    ulong InstanceId
)
{
    public string FormattedKey => $"{TypeId:X8}:{GroupId:X8}:{InstanceId:X16}";
}
