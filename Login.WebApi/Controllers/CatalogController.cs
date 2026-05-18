using Login.Infrastructure.Model;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly DataContext _db;
    public CatalogController(DataContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CatalogProcessTypeDto>>> GetAll()
    {
        var data = await _db.CatalogProcessTypes
            .Include(pt => pt.Stages)
                .ThenInclude(s => s.SubStages)
            .OrderBy(pt => pt.Id)
            .ToListAsync();

        return Ok(data.Select(pt => new CatalogProcessTypeDto(
            pt.Id,
            pt.Name,
            pt.Stages.OrderBy(s => s.Id).Select(s => new CatalogStageDto(
                s.Id,
                s.Name,
                s.SubStages.OrderBy(ss => ss.Id).Select(ss => new CatalogSubStageDto(ss.Id, ss.Name)).ToList()
            )).ToList()
        )));
    }
}
