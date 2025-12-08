// ============================================
// Controllers/TrabajosController.cs
// ============================================
using CarSlineAPI.Data;
using CarSlineAPI.Models.DTOs;
using CarSlineAPI.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarSlineAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrabajosController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<TrabajosController> _logger;

        public TrabajosController(ApplicationDbContext db, ILogger<TrabajosController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los trabajos de una orden
        /// GET api/Trabajos/orden/{ordenId}
        /// </summary>
        [HttpGet("orden/{ordenId}")]
        [ProducesResponseType(typeof(List<TrabajoDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerTrabajosPorOrden(int ordenId)
        {
            try
            {
                var trabajos = await _db.Set<TrabajoPorOrden>()
                    .Include(t => t.TecnicoAsignado)
                    .Include(t => t.EstadoTrabajoNavegacion)
                    .Where(t => t.OrdenGeneralId == ordenId && t.Activo)
                    .OrderBy(t => t.FechaCreacion)
                    .Select(t => new TrabajoDto
                    {
                        Id = t.Id,
                        OrdenGeneralId = t.OrdenGeneralId,
                        Trabajo = t.Trabajo,
                        TecnicoAsignadoId = t.TecnicoAsignadoId,
                        TecnicoNombre = t.TecnicoAsignado != null ? t.TecnicoAsignado.NombreCompleto : null,
                        FechaHoraAsignacionTecnico = t.FechaHoraAsignacionTecnico,
                        FechaHoraInicio = t.FechaHoraInicio,
                        FechaHoraTermino = t.FechaHoraTermino,
                        IncidenciasServicio = t.IncidenciasServicio,
                        ComentariosTecnico = t.ComentariosTecnico,
                        ComentariosJefeTaller = t.ComentariosJefeTaller,
                        EstadoTrabajo = t.EstadoTrabajo,
                        EstadoTrabajoNombre = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.NombreEstado : null,
                        ColorEstado = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.Color : null,
                        FechaCreacion = t.FechaCreacion
                    })
                    .ToListAsync();

                return Ok(trabajos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener trabajos de orden {ordenId}");
                return StatusCode(500, new { Message = "Error al obtener trabajos" });
            }
        }

        /// <summary>
        /// Obtener orden completa con todos sus trabajos
        /// GET api/Trabajos/orden-completa/{ordenId}
        /// </summary>
        [HttpGet("orden-completa/{ordenId}")]
        [ProducesResponseType(typeof(OrdenConTrabajosDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerOrdenCompleta(int ordenId)
        {
            try
            {
                var orden = await _db.OrdenesGenerales
                    .Include(o => o.Cliente)
                    .Include(o => o.Vehiculo)
                    .Include(o => o.Asesor)
                    .Include(o => o.Trabajos)
                        .ThenInclude(t => t.TecnicoAsignado)
                    .Include(o => o.Trabajos)
                        .ThenInclude(t => t.EstadoTrabajoNavegacion)
                    .Where(o => o.Id == ordenId && o.Activo)
                    .Select(o => new OrdenConTrabajosDto
                    {
                        Id = o.Id,
                        NumeroOrden = o.NumeroOrden,
                        TipoOrdenId = o.TipoOrdenId,
                        ClienteNombre = o.Cliente.NombreCompleto,
                        ClienteTelefono = o.Cliente.TelefonoMovil,
                        VehiculoCompleto = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Anio}",
                        VIN = o.Vehiculo.VIN,
                        Placas = o.Vehiculo.Placas ?? "",
                        AsesorNombre = o.Asesor.NombreCompleto,
                        KilometrajeActual = o.KilometrajeActual,
                        FechaCreacion = o.FechaCreacion,
                        FechaHoraPromesaEntrega = o.FechaHoraPromesaEntrega,
                        EstadoOrdenId = o.EstadoOrdenId,
                        CostoTotal = o.CostoTotal,
                        TotalTrabajos = o.TotalTrabajos,
                        TrabajosCompletados = o.TrabajosCompletados,
                        ProgresoGeneral = o.ProgresoGeneral,
                        ObservacionesAsesor = o.ObservacionesAsesor,
                        Trabajos = o.Trabajos
                            .Where(t => t.Activo)
                            .Select(t => new TrabajoDto
                            {
                                Id = t.Id,
                                OrdenGeneralId = t.OrdenGeneralId,
                                Trabajo = t.Trabajo,
                                TecnicoAsignadoId = t.TecnicoAsignadoId,
                                TecnicoNombre = t.TecnicoAsignado != null ? t.TecnicoAsignado.NombreCompleto : null,
                                FechaHoraAsignacionTecnico = t.FechaHoraAsignacionTecnico,
                                FechaHoraInicio = t.FechaHoraInicio,
                                FechaHoraTermino = t.FechaHoraTermino,
                                IncidenciasServicio = t.IncidenciasServicio,
                                ComentariosTecnico = t.ComentariosTecnico,
                                ComentariosJefeTaller = t.ComentariosJefeTaller,
                                EstadoTrabajo = t.EstadoTrabajo,
                                EstadoTrabajoNombre = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.NombreEstado : null,
                                ColorEstado = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.Color : null,
                                FechaCreacion = t.FechaCreacion
                            }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (orden == null)
                    return NotFound(new { Message = "Orden no encontrada" });

                return Ok(orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener orden completa {ordenId}");
                return StatusCode(500, new { Message = "Error al obtener orden" });
            }
        }

        /// <summary>
        /// Agregar trabajo a una orden existente
        /// POST api/Trabajos/agregar
        /// </summary>
        [HttpPost("agregar/{ordenId}")]
        [ProducesResponseType(typeof(TrabajoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> AgregarTrabajo(int ordenId, [FromBody] CrearTrabajoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new TrabajoResponse
                {
                    Success = false,
                    Message = "Datos inválidos"
                });

            try
            {
                var orden = await _db.OrdenesGenerales.FindAsync(ordenId);
                if (orden == null || !orden.Activo)
                    return NotFound(new TrabajoResponse
                    {
                        Success = false,
                        Message = "Orden no encontrada"
                    });

                var trabajo = new TrabajoPorOrden
                {
                    OrdenGeneralId = ordenId,
                    Trabajo = request.Trabajo,
                    TecnicoAsignadoId = request.TecnicoAsignadoId,
                    FechaHoraAsignacionTecnico = request.TecnicoAsignadoId.HasValue ? DateTime.Now : null,
                    EstadoTrabajo = 1, // Pendiente
                    Activo = true,
                    FechaCreacion = DateTime.Now
                };

                _db.Set<TrabajoPorOrden>().Add(trabajo);
                await _db.SaveChangesAsync();

                // Recargar con relaciones
                await _db.Entry(trabajo)
                    .Reference(t => t.TecnicoAsignado)
                    .LoadAsync();
                await _db.Entry(trabajo)
                    .Reference(t => t.EstadoTrabajoNavegacion)
                    .LoadAsync();

                _logger.LogInformation($"Trabajo agregado a orden {ordenId}");

                return Ok(new TrabajoResponse
                {
                    Success = true,
                    Message = "Trabajo agregado exitosamente",
                    Trabajo = new TrabajoDto
                    {
                        Id = trabajo.Id,
                        OrdenGeneralId = trabajo.OrdenGeneralId,
                        Trabajo = trabajo.Trabajo,
                        TecnicoAsignadoId = trabajo.TecnicoAsignadoId,
                        TecnicoNombre = trabajo.TecnicoAsignado?.NombreCompleto,
                        EstadoTrabajo = trabajo.EstadoTrabajo,
                        EstadoTrabajoNombre = trabajo.EstadoTrabajoNavegacion?.NombreEstado,
                        ColorEstado = trabajo.EstadoTrabajoNavegacion?.Color,
                        FechaCreacion = trabajo.FechaCreacion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar trabajo");
                return StatusCode(500, new TrabajoResponse
                {
                    Success = false,
                    Message = "Error al agregar trabajo"
                });
            }
        }

        /// <summary>
        /// Asignar técnico a un trabajo
        /// PUT api/Trabajos/asignar-tecnico
        /// </summary>
        [HttpPut("asignar-tecnico")]
        [ProducesResponseType(typeof(TrabajoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> AsignarTecnico([FromBody] AsignarTecnicoTrabajoRequest request)
        {
            try
            {
                var trabajo = await _db.Set<TrabajoPorOrden>().FindAsync(request.TrabajoId);

                if (trabajo == null || !trabajo.Activo)
                    return NotFound(new TrabajoResponse
                    {
                        Success = false,
                        Message = "Trabajo no encontrado"
                    });

                var tecnico = await _db.Usuarios.FindAsync(request.TecnicoId);
                if (tecnico == null || !tecnico.Activo || tecnico.RolId != 5)
                    return BadRequest(new TrabajoResponse
                    {
                        Success = false,
                        Message = "Técnico no válido"
                    });

                trabajo.TecnicoAsignadoId = request.TecnicoId;
                trabajo.FechaHoraAsignacionTecnico = DateTime.Now;

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Técnico {request.TecnicoId} asignado a trabajo {request.TrabajoId}");

                return Ok(new TrabajoResponse
                {
                    Success = true,
                    Message = "Técnico asignado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar técnico");
                return StatusCode(500, new TrabajoResponse
                {
                    Success = false,
                    Message = "Error al asignar técnico"
                });
            }
        }

        /// <summary>
        /// Iniciar trabajo (técnico comienza a trabajar)
        /// PUT api/Trabajos/iniciar/{trabajoId}
        /// </summary>
        [HttpPut("iniciar/{trabajoId}")]
        [ProducesResponseType(typeof(TrabajoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> IniciarTrabajo(
            int trabajoId,
            [FromHeader(Name = "X-User-Id")] int tecnicoId)
        {
            try
            {
                var trabajo = await _db.Set<TrabajoPorOrden>().FindAsync(trabajoId);

                if (trabajo == null || !trabajo.Activo)
                    return NotFound(new TrabajoResponse
                    {
                        Success = false,
                        Message = "Trabajo no encontrado"
                    });

                if (trabajo.TecnicoAsignadoId != tecnicoId)
                    return Unauthorized(new TrabajoResponse
                    {
                        Success = false,
                        Message = "No estás asignado a este trabajo"
                    });

                if (trabajo.EstadoTrabajo != 1)
                    return BadRequest(new TrabajoResponse
                    {
                        Success = false,
                        Message = "El trabajo ya fue iniciado"
                    });

                trabajo.EstadoTrabajo = 2; // En Proceso
                trabajo.FechaHoraInicio = DateTime.Now;

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Trabajo {trabajoId} iniciado por técnico {tecnicoId}");

                return Ok(new TrabajoResponse
                {
                    Success = true,
                    Message = "Trabajo iniciado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar trabajo");
                return StatusCode(500, new TrabajoResponse
                {
                    Success = false,
                    Message = "Error al iniciar trabajo"
                });
            }
        }

        /// <summary>
        /// Completar trabajo
        /// PUT api/Trabajos/completar/{trabajoId}
        /// </summary>
        [HttpPut("completar/{trabajoId}")]
        [ProducesResponseType(typeof(TrabajoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CompletarTrabajo(
            int trabajoId,
            [FromHeader(Name = "X-User-Id")] int tecnicoId,
            [FromBody] string? comentarios = null)
        {
            try
            {
                var trabajo = await _db.Set<TrabajoPorOrden>().FindAsync(trabajoId);

                if (trabajo == null || !trabajo.Activo)
                    return NotFound(new TrabajoResponse
                    {
                        Success = false,
                        Message = "Trabajo no encontrado"
                    });

                if (trabajo.TecnicoAsignadoId != tecnicoId)
                    return Unauthorized(new TrabajoResponse
                    {
                        Success = false,
                        Message = "No estás asignado a este trabajo"
                    });

                if (trabajo.EstadoTrabajo != 2)
                    return BadRequest(new TrabajoResponse
                    {
                        Success = false,
                        Message = "El trabajo no está en proceso"
                    });

                trabajo.EstadoTrabajo = 3; // Completado
                trabajo.FechaHoraTermino = DateTime.Now;
                trabajo.ComentariosTecnico = comentarios;

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Trabajo {trabajoId} completado por técnico {tecnicoId}");

                return Ok(new TrabajoResponse
                {
                    Success = true,
                    Message = "Trabajo completado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al completar trabajo");
                return StatusCode(500, new TrabajoResponse
                {
                    Success = false,
                    Message = "Error al completar trabajo"
                });
            }
        }

        /// <summary>
        /// Obtener trabajos asignados a un técnico
        /// GET api/Trabajos/mis-trabajos
        /// </summary>
        [HttpGet("mis-trabajos")]
        [ProducesResponseType(typeof(List<TrabajoDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerMisTrabajos(
            [FromHeader(Name = "X-User-Id")] int tecnicoId,
            [FromQuery] int? estadoFiltro = null)
        {
            try
            {
                var query = _db.Set<TrabajoPorOrden>()
                    .Include(t => t.OrdenGeneral)
                        .ThenInclude(o => o.Cliente)
                    .Include(t => t.OrdenGeneral)
                        .ThenInclude(o => o.Vehiculo)
                    .Include(t => t.EstadoTrabajoNavegacion)
                    .Where(t => t.TecnicoAsignadoId == tecnicoId && t.Activo);

                if (estadoFiltro.HasValue)
                    query = query.Where(t => t.EstadoTrabajo == estadoFiltro.Value);

                var trabajos = await query
                    .OrderBy(t => t.EstadoTrabajo)
                    .ThenBy(t => t.FechaCreacion)
                    .Select(t => new TrabajoDto
                    {
                        Id = t.Id,
                        OrdenGeneralId = t.OrdenGeneralId,
                        Trabajo = t.Trabajo,
                        TecnicoAsignadoId = t.TecnicoAsignadoId,
                        FechaHoraAsignacionTecnico = t.FechaHoraAsignacionTecnico,
                        FechaHoraInicio = t.FechaHoraInicio,
                        FechaHoraTermino = t.FechaHoraTermino,
                        IncidenciasServicio = t.IncidenciasServicio,
                        ComentariosTecnico = t.ComentariosTecnico,
                        EstadoTrabajo = t.EstadoTrabajo,
                        EstadoTrabajoNombre = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.NombreEstado : null,
                        ColorEstado = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.Color : null,
                        FechaCreacion = t.FechaCreacion
                    })
                    .ToListAsync();

                return Ok(trabajos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener trabajos del técnico {tecnicoId}");
                return StatusCode(500, new { Message = "Error al obtener trabajos" });
            }
        }
    }
}