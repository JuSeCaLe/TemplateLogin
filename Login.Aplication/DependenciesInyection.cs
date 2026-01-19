namespace Login.Applicacion
{
    using System.Reflection;
    using MediatR;
    using Microsoft.Extensions.DependencyInjection;

    public static class DependenciesInyection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(Assembly.GetExecutingAssembly());
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            //services.AddFluentValidation(Assembly.GetExecutingAssembly());        
            //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(SolicitudValidacionComportamiento<,>));
            //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExcepcionSinManejoComportamiento<,>));

            return services;
        }
    }
}