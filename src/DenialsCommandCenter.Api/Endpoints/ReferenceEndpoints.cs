using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Api.Endpoints;

public sealed record PolicySectionResponse(string PolicyTitle, string Text);

public sealed record ReferenceResponse(
    IReadOnlyDictionary<string, string> ReasonCodes, IReadOnlyDictionary<string, string> RemarkCodes,
    IReadOnlyDictionary<string, PolicySectionResponse> PolicySections);

public static class ReferenceEndpoints
{
    public static void MapReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reference", (ReferenceData reference) => Results.Ok(new ReferenceResponse(
            reference.ReasonCodes.Carc,
            reference.ReasonCodes.Rarc,
            reference.Policies.Documents
                .SelectMany(document => document.Sections.Select(section => (document.Title, section)))
                .DistinctBy(entry => entry.section.Citation)
                .ToDictionary(entry => entry.section.Citation, entry => new PolicySectionResponse(entry.Title, entry.section.Text)))));
    }
}
