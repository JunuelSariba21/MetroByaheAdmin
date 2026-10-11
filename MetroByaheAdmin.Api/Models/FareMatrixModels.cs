namespace MetroByaheAdmin.Api.Models;

public sealed class FareMatrixRequest
{
    public string? Id { get; set; }

    public string? Accommodation { get; set; }

    // Some existing documents use the original misspelled field name.
    // Accept it at the API boundary while returning the corrected DTO name.
    public string? Accomodation { get; set; }

    public List<FareEntryRequest>? Fares { get; set; }
}

public sealed class FareEntryRequest
{
    public string? Origin { get; set; }

    public string? Destination { get; set; }

    public decimal? Full { get; set; }

    public decimal? Pagibig { get; set; }

    public decimal? Sp { get; set; }
}

public sealed class FareMatrixResponse
{
    public string Id { get; set; } = "";

    public string Accommodation { get; set; } = "";

    public List<FareEntryResponse> Fares { get; set; } = new();
}

public sealed class FareEntryResponse
{
    public string Origin { get; set; } = "";

    public string Destination { get; set; } = "";

    public decimal Full { get; set; }

    public decimal Pagibig { get; set; }

    public decimal Sp { get; set; }
}
