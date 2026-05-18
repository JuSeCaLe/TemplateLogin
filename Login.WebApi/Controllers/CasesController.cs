using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Cases;
using Login.WebApi.Controllers.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Login.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly DataContext _db;
    public CasesController(DataContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CaseDto>>> GetAll()
    {
        var cases = await QueryWithIncludes().OrderByDescending(c => c.CreatedAt).ToListAsync();
        return Ok(cases.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CaseDto>> GetById(int id)
    {
        var c = await FindCase(id);
        return c is null ? NotFound() : Ok(ToDto(c));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<CaseDto>>> Search([FromQuery] string radicado)
    {
        var term = radicado.Trim();
        var cases = await QueryWithIncludes()
            .Where(c => c.Process.Radicado.Contains(term))
            .ToListAsync();
        return Ok(cases.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CreateCaseRequest req)
    {
        var entity = BuildEntity(req);
        _db.Cases.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(ToDto(await FindCase(entity.Id) ?? entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CaseDto>> Update(int id, UpdateCaseRequest req)
    {
        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        ApplyUpdate(entity, req);
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    [HttpPost("{id:int}/stages")]
    public async Task<ActionResult<CaseDto>> AddStage(int id, AddProcessStageRequest req)
    {
        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        entity.ProcessStages.Add(new CaseProcessStage
        {
            CaseId = id,
            CreatedAt = req.StageDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            StageName = req.StageName.Trim(),
            SubStageName = req.SubStageName?.Trim() ?? "",
            Observation = req.Observation?.Trim()
        });
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    [HttpPost("{id:int}/notes")]
    public async Task<ActionResult<CaseDto>> AddNote(int id, AddProceduralNoteRequest req)
    {
        var entity = await FindCase(id);
        if (entity is null) return NotFound();

        entity.ProceduralNotes.Add(new CaseProceduralNote
        {
            CaseId = id,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Text = req.Text.Trim()
        });
        await _db.SaveChangesAsync();
        return Ok(ToDto(entity));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private IQueryable<Case> QueryWithIncludes() =>
        _db.Cases
            .Include(c => c.Parties)
            .Include(c => c.ProcessStages)
            .Include(c => c.ProceduralNotes);

    private async Task<Case?> FindCase(int id) =>
        await QueryWithIncludes().FirstOrDefaultAsync(c => c.Id == id);

    private static Case BuildEntity(CreateCaseRequest req) => new()
    {
        Process = MapProcess(req.Process),
        FinancialInfo = MapFinancial(req.FinancialInfo),
        Measures = MapMeasures(req.Measures),
        Stages = MapStages(req.Stages),
        Auction = MapAuction(req.Auction),
        Closure = MapClosure(req.Closure),
        Parties = req.PartiesInfo.Select(MapParty).ToList()
    };

    private static void ApplyUpdate(Case entity, UpdateCaseRequest req)
    {
        entity.Process.Radicado = req.Process.Radicado.Trim();
        entity.Process.ProcessType = req.Process.ProcessType.Trim();
        entity.Process.Court = req.Process.Court.Trim();
        entity.Process.City = req.Process.City.Trim();

        entity.FinancialInfo = MapFinancial(req.FinancialInfo);
        entity.Measures = MapMeasures(req.Measures);
        entity.Stages = MapStages(req.Stages);
        entity.Auction = MapAuction(req.Auction);
        entity.Closure = MapClosure(req.Closure);

        entity.Parties.Clear();
        foreach (var p in req.PartiesInfo)
            entity.Parties.Add(MapParty(p));
    }

    private static CaseProcess MapProcess(ProcessInfoDto d) => new()
    {
        Radicado = d.Radicado.Trim(),
        ProcessType = d.ProcessType.Trim(),
        Court = d.Court.Trim(),
        City = d.City.Trim()
    };

    private static CaseFinancial? MapFinancial(FinancialInfoDto? d) =>
        d is null ? null : new() { Capital = d.Capital, Obligations = d.Obligations?.Trim(), FngFag = d.FngFag };

    private static CaseMeasures? MapMeasures(MeasuresInfoDto? d) =>
        d is null ? null : new() { Embargo = d.Embargo, EmbargoDate = d.EmbargoDate, RemanentEmbargo = d.RemanentEmbargo, RemanentEntity = d.RemanentEntity?.Trim() };

    private static CaseStages? MapStages(StagesInfoDto? d) =>
        d is null ? null : new() { PaymentOrder = d.PaymentOrder, PersonalNotification = d.PersonalNotification, PersonalNotificationDate = d.PersonalNotificationDate, FirstInstanceSentence = d.FirstInstanceSentence, FirstInstanceDate = d.FirstInstanceDate, SecondInstance = d.SecondInstance, SecondInstanceDate = d.SecondInstanceDate };

    private static CaseAuction? MapAuction(AuctionInfoDto? d) =>
        d is null ? null : new() { AppraisalStatus = d.AppraisalStatus, AppraisalDate = d.AppraisalDate, AppraisalValue = d.AppraisalValue, AuctionStatus = d.AuctionStatus, AuctionDate = d.AuctionDate, Awarded = d.Awarded, AwardDate = d.AwardDate };

    private static CaseClosure? MapClosure(ClosureInfoDto? d) =>
        d is null ? null : new() { TerminationDate = d.TerminationDate, TerminationReason = d.TerminationReason?.Trim(), TitlesStatus = d.TitlesStatus?.Trim(), Delivery = d.Delivery, DeliveryDate = d.DeliveryDate, FileReturnStatus = d.FileReturnStatus?.Trim(), FileReturnDate = d.FileReturnDate };

    private static CaseParty MapParty(PartyInfoDto d) => new() { Person = d.Person.Trim(), ProcessRole = d.ProcessRole.Trim() };

    private static CaseDto ToDto(Case c) => new(
        c.Id,
        c.CreatedAt.ToString("yyyy-MM-dd"),
        new ProcessInfoDto(c.Process.Radicado, c.Process.ProcessType, c.Process.Court, c.Process.City),
        c.Parties.Select(p => new PartyInfoDto(p.Person, p.ProcessRole)).ToList(),
        c.FinancialInfo is null ? null : new FinancialInfoDto(c.FinancialInfo.Capital, c.FinancialInfo.Obligations, c.FinancialInfo.FngFag),
        c.Measures is null ? null : new MeasuresInfoDto(c.Measures.Embargo, c.Measures.EmbargoDate, c.Measures.RemanentEmbargo, c.Measures.RemanentEntity),
        c.Stages is null ? null : new StagesInfoDto(c.Stages.PaymentOrder, c.Stages.PersonalNotification, c.Stages.PersonalNotificationDate, c.Stages.FirstInstanceSentence, c.Stages.FirstInstanceDate, c.Stages.SecondInstance, c.Stages.SecondInstanceDate),
        c.Auction is null ? null : new AuctionInfoDto(c.Auction.AppraisalStatus, c.Auction.AppraisalDate, c.Auction.AppraisalValue, c.Auction.AuctionStatus, c.Auction.AuctionDate, c.Auction.Awarded, c.Auction.AwardDate),
        c.Closure is null ? null : new ClosureInfoDto(c.Closure.TerminationDate, c.Closure.TerminationReason, c.Closure.TitlesStatus, c.Closure.Delivery, c.Closure.DeliveryDate, c.Closure.FileReturnStatus, c.Closure.FileReturnDate),
        c.ProcessStages.OrderBy(s => s.Id).Select(s => new ProcessStageDto(s.Id, s.CreatedAt, s.StageName, s.SubStageName, s.Observation)).ToList(),
        c.ProceduralNotes.OrderBy(n => n.Id).Select(n => new ProceduralNoteDto(n.Id, n.CreatedAt, n.Text)).ToList()
    );
}
