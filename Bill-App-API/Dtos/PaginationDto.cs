namespace Bill_App_API.Dtos;

public record PaginatedResponse<T>
(
    List<T> Data,
    PaginationMeta Meta
);

public record PaginationMeta
(
    int Total,
    int Page,
    int Limit,
    int TotalPages
);
