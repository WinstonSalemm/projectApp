using ProjectApp.Api.Dtos;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Services;

public interface ISaleCalculator
{
    Task<Sale> BuildAndCalculateAsync(SaleCreateDto dto, CancellationToken ct = default);
}
