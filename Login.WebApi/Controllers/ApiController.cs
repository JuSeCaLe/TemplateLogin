namespace Login.WebApi.Controllers
{
    using MediatR;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Controladora base con la definición del mediador
    /// </summary>
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        /// <summary>
        /// Mediador
        /// </summary>
        private IMediator? _mediator;

        /// <summary>
        /// Mediador
        /// </summary>
        protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetService<IMediator>();
    }
}