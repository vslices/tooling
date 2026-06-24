using LanguageExt;

using VSlices.DocumentGeneration.Application;
using VSlices.Monads;

namespace VSlices;

public sealed record GenerateFromTemplateCommand();

public sealed class GenerateFromTemplate
    : Feature<GenerateFromTemplate, DocGenRuntime, GenerateFromTemplateCommand>
{
    public static string Name => nameof(GenerateFromTemplate);

    public static Flow<DocGenRuntime, GenerateFromTemplateCommand, Unit> Get() =>
        Flow<DocGenRuntime, GenerateFromTemplateCommand>.Unit;
}