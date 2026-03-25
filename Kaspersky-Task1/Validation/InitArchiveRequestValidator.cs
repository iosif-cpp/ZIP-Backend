using FluentValidation;
using Kaspersky_Task1.Configuration;
using Kaspersky_Task1.Contracts;
using Kaspersky_Task1.Services.Catalogs;

namespace Kaspersky_Task1.Validation;

public sealed class InitArchiveRequestValidator : AbstractValidator<InitArchiveRequest>
{
    public InitArchiveRequestValidator(ArchiveOptions opts, IFileCatalog catalog)
    {
        var opts1 = opts;
        var catalog1 = catalog;

        RuleFor(x => x.Files)
            .NotNull()
            .WithMessage("Files list is required.");

        RuleFor(x => x.Files)
            .Must(files => Normalize(files).Count > 0)
            .WithMessage("Files list is required.");

        RuleFor(x => x.Files)
            .Must(files => Normalize(files).Count <= opts1.MaxFilesPerArchive)
            .WithMessage($"Too many files. Max is {opts1.MaxFilesPerArchive}.");

        RuleFor(x => x)
            .Custom((request, context) =>
            {
                var normalized = Normalize(request.Files);
                if (normalized.Count == 0)
                    return;

                try
                {
                    catalog1.ResolveRequestedFilesAsync(normalized, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (ArgumentException ex)
                {
                    context.AddFailure(ex.Message);
                }
            });
    }

    private static List<string> Normalize(List<string>? files)
    {
        if (files is null)
            return new List<string>();

        return files
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

