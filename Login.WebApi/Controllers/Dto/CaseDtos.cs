namespace Login.WebApi.Controllers.Dto;

public record ProcessInfoDto(string Radicado, string ProcessType, string Court, string City);
public record PartyInfoDto(string Person, string ProcessRole);
public record FinancialInfoDto(decimal? Capital, string? Obligations, bool FngFag);
public record MeasuresInfoDto(bool Embargo, string? EmbargoDate, bool RemanentEmbargo, string? RemanentEntity);
public record StagesInfoDto(bool PaymentOrder, bool PersonalNotification, string? PersonalNotificationDate, bool FirstInstanceSentence, string? FirstInstanceDate, bool SecondInstance, string? SecondInstanceDate);
public record AuctionInfoDto(string? AppraisalStatus, string? AppraisalDate, decimal? AppraisalValue, string? AuctionStatus, string? AuctionDate, bool? Awarded, string? AwardDate);
public record ClosureInfoDto(string? TerminationDate, string? TerminationReason, string? TitlesStatus, bool? Delivery, string? DeliveryDate, string? FileReturnStatus, string? FileReturnDate);
public record ProcessStageDto(int Id, string CreatedAt, string StageName, string SubStageName, string? Observation);
public record ProceduralNoteDto(int Id, string CreatedAt, string Text);

public record CaseDto(
    int Id,
    string CreatedAt,
    ProcessInfoDto Process,
    List<PartyInfoDto> PartiesInfo,
    FinancialInfoDto? FinancialInfo,
    MeasuresInfoDto? Measures,
    StagesInfoDto? Stages,
    AuctionInfoDto? Auction,
    ClosureInfoDto? Closure,
    List<ProcessStageDto> ProcessStages,
    List<ProceduralNoteDto> ProceduralNotes
);

public record CreateCaseRequest(
    ProcessInfoDto Process,
    List<PartyInfoDto> PartiesInfo,
    FinancialInfoDto? FinancialInfo,
    MeasuresInfoDto? Measures,
    StagesInfoDto? Stages,
    AuctionInfoDto? Auction,
    ClosureInfoDto? Closure
);

public record UpdateCaseRequest(
    ProcessInfoDto Process,
    List<PartyInfoDto> PartiesInfo,
    FinancialInfoDto? FinancialInfo,
    MeasuresInfoDto? Measures,
    StagesInfoDto? Stages,
    AuctionInfoDto? Auction,
    ClosureInfoDto? Closure
);

public record AddProcessStageRequest(string? StageDate, string StageName, string? SubStageName, string? Observation);
public record AddProceduralNoteRequest(string Text);
