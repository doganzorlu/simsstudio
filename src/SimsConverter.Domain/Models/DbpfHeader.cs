namespace SimsConverter.Domain.Models;

public record DbpfHeader(
    string Magic,
    int MajorVersion,
    int MinorVersion,
    int IndexEntryCount,
    long IndexOffset,
    int IndexSizeBytes
);
