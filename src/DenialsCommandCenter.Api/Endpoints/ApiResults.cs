namespace DenialsCommandCenter.Api.Endpoints;

public static class ApiResults
{
    public static IResult BadRequest(string title) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: title);
}
