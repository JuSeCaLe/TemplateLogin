namespace Login.Application.Business.LogIn
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoMapper;
    using Login.Application.Common.Interfaces;
    using MediatR;

    public class LogInRequest : IRequest<string>
    {
        public string UserName { get; set; }

        public string Password { get; set; }
    }

    public class StartSesion : IRequestHandler<LogInRequest, string>
    {
        private readonly IMapper mapperDto;

        private readonly IRepositoriesFactory repositoryFactory;

        public StartSesion(IMapper mapper, IRepositoriesFactory factory)
        {
            mapper = mapper;
        }

        public Task<string> Handle(LogInRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}