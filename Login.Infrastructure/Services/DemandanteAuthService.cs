using Login.Infrastructure.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Login.Infrastructure.Services;

// Resuelve, en vivo contra la BD, qué roles-demandante (AppRole.IsDemandante) tiene
// asignado un usuario. Se usa tanto en el login/me (para armar el dropdown de
// creación de casos) como en CasesController (para autorizar cada request) — no se
// confía en los claims horneados en el JWT para que activar/desactivar un rol, o
// reasignarlo, tenga efecto inmediato sin esperar a un nuevo login.
public class DemandanteAuthService
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly UserManager<AppUser> _userManager;

    public DemandanteAuthService(RoleManager<AppRole> roleManager, UserManager<AppUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    // TODOS los roles-demandante (activos o no) que tiene asignado el usuario.
    // Incluye los inactivos a propósito: distinguir "el usuario no tiene NINGÚN
    // rol de demandante" (sin restricción, ej. r-user) de "tiene uno pero está
    // inactivo" (debe quedar SIN ver ningún caso) es responsabilidad del caller.
    //
    // Nota: se filtra en memoria (no con userRoles.Contains(r.Name) en el Where)
    // porque EF Core traduce ese patrón a OPENJSON(...) WITH (...), que este SQL
    // Server rechaza con "Incorrect syntax near the keyword 'WITH'".
    public async Task<List<AppRole>> ResolveAssignedDemandanteRolesAsync(AppUser user)
    {
        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Count == 0) return new List<AppRole>();

        var demandanteRoles = await _roleManager.Roles
            .Where(r => r.IsDemandante)
            .ToListAsync();

        return demandanteRoles
            .Where(r => r.Name is not null && userRoles.Contains(r.Name))
            .ToList();
    }

    // Subconjunto activo: demandantes que el usuario puede efectivamente usar hoy
    // (para el dropdown de creación de casos y la respuesta de login/me).
    public async Task<List<AppRole>> ResolveActiveDemandanteRolesAsync(AppUser user) =>
        (await ResolveAssignedDemandanteRolesAsync(user))
            .Where(r => r.Active)
            .ToList();
}
