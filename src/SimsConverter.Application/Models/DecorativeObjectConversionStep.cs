using System.Collections.Generic;
using SimsConverter.Domain.Enums;
using SimsConverter.Domain.Models;

namespace SimsConverter.Application.Models;

public record DecorativeObjectConversionStep(
    string StepId,
    string Title,
    DecorativeObjectConversionStepStatus Status,
    string Details,
    IReadOnlyList<ConversionIssue> Issues
);
