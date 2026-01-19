using Microsoft.Extensions.DependencyInjection;

namespace Login.Infrastructure
{
    public static class DependenciesConfiguration
    {
        public static IServiceCollection AddDependenciesInfrastructure(this IServiceCollection services)
        {
            //servicios.AddTransient<IFactoriaRepositorio, FactoriaRepositorios>();
            //servicios.AddTransient<IParametricaRepositorio, ParametricaRepositorio>();
            //servicios.AddTransient<IEmpresaRepositorio, EmpresaRepositorio>();
            //servicios.AddTransient<ICiudadRepositorio, CiudadRepositorio>();
            //servicios.AddTransient<IRutaRepositorio, RutaRepositorio>();
            //servicios.AddTransient<IConfiguracionKeyVault, ConfiguracionKeyVault>();
            //servicios.AddTransient<IRepositorioIdentityRoles, RepositorioIdentityRoles>();
            //servicios.AddTransient<IRepositorioIdentityUsuarios, RepositorioIdentityUsuarios>();
            //servicios.AddTransient<IClienteMicrosoftGraph, ClienteMicrosoftGraph>();
            //servicios.AddTransient<IManejadorEmail, ManejadorEmail>();
            //servicios.AddTransient<IGeneradorTokens, GeneradorTokens>();
            return services;
        }
    }
}