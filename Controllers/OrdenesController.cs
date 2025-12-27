// ============================================
// Controllers/OrdenesController.cs - ACTUALIZADO
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
    public class OrdenesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<OrdenesController> _logger;

        public OrdenesController(ApplicationDbContext db, ILogger<OrdenesController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// ✅ NUEVO: Crear orden con lista de trabajos
        /// POST api/Ordenes/crear-con-trabajos
        /// </summary>
        
        [HttpPost("crear-con-trabajos")]
        public async Task<IActionResult> CrearOrdenConTrabajos(
            [FromBody] CrearOrdenConTrabajosRequest request,
            [FromHeader(Name = "X-User-Id")] int asesorId)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Success = false, Message = "Datos inválidos" });

            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    // 1. Generar número de orden
                    var prefijo = request.TipoOrdenId switch
                    {
                        1 => "SRV",
                        2 => "DIA",
                        3 => "REP",
                        4 => "GAR",
                        5 => "RTO",
                        _ => "ORD"
                    };

                    var maxNumero = await _db.OrdenesGenerales
                        .Where(o => o.NumeroOrden.StartsWith(prefijo + "-"))
                        .Select(o => o.NumeroOrden)
                        .ToListAsync();

                    int siguiente = 1;
                    if (maxNumero.Any())
                    {
                        var maxInt = maxNumero
                            .Select(s =>
                            {
                                var parts = s.Split('-', 2);
                                if (parts.Length < 2) return 0;
                                return int.TryParse(parts[1], out var n) ? n : 0;
                            })
                            .DefaultIfEmpty(0)
                            .Max();
                        siguiente = maxInt + 1;
                    }

                    var numeroOrden = $"{prefijo}-{siguiente:D6}";

                    // 2. Crear orden general
                    var ordenGeneral = new OrdenGeneral
                    {
                        NumeroOrden = numeroOrden,
                        TipoOrdenId = request.TipoOrdenId,
                        ClienteId = request.ClienteId,
                        VehiculoId = request.VehiculoId,
                        TipoServicioId = request.TipoServicioId,
                        AsesorId = asesorId,
                        KilometrajeActual = request.KilometrajeActual,
                        EstadoOrdenId = 1, // Pendiente
                        FechaHoraPromesaEntrega = request.FechaHoraPromesaEntrega,
                        ObservacionesAsesor = request.ObservacionesAsesor,
                        CostoTotal = 0, // Se calculará después
                        FechaCreacion = DateTime.Now,
                        Activo = true,
                        TotalTrabajos = request.Trabajos.Count,
                        TrabajosCompletados = 0,
                        ProgresoGeneral = 0
                    };

                    _db.OrdenesGenerales.Add(ordenGeneral);
                    await _db.SaveChangesAsync();

                    // 3. Crear trabajos asociados
                    foreach (var t in request.Trabajos)
                    {
                        var trabajo = new TrabajoPorOrden
                        {
                            OrdenGeneralId = ordenGeneral.Id,
                            Trabajo = t.Trabajo,
                            IndicacionesTrabajo = string.IsNullOrWhiteSpace(t.Indicaciones) ? null : t.Indicaciones,
                            EstadoTrabajo = 1,
                            Activo = true,
                            FechaCreacion = DateTime.Now
                        };

                        _db.TrabajosPorOrden.Add(trabajo);
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Orden {numeroOrden} creada con {request.Trabajos.Count} trabajos");

                    return Ok(new
                    {
                        Success = true,
                        NumeroOrden = numeroOrden,
                        OrdenId = ordenGeneral.Id,
                        TotalTrabajos = request.Trabajos.Count,
                        Message = "Orden creada exitosamente"
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error al crear orden con trabajos");
                    return StatusCode(500, new { Success = false, Message = "Error al crear orden" });
                }
            });
        }

        /// <summary>
        /// Obtener órdenes por tipo (para asesor)
        /// GET api/Ordenes/asesor/{tipoOrdenId}
        /// </summary>
        [HttpGet("asesor/{tipoOrdenId}")]
        public async Task<IActionResult> ObtenerOrdenesPorTipo(
        int tipoOrdenId,
        [FromHeader(Name = "X-User-Id")] int asesorId)
        {
            try
            {
                var ordenes = await _db.OrdenesGenerales
                    .Include(o => o.Cliente)
                    .Include(o => o.Vehiculo)
                    .Include(o => o.Trabajos.Where(t => t.Activo))
                        .ThenInclude(t => t.TecnicoAsignado)
                    .Where(o => o.TipoOrdenId == tipoOrdenId
                             && o.AsesorId == asesorId
                             && o.Activo
                             && new[] { 1, 2, 3 }.Contains(o.EstadoOrdenId))
                    .OrderBy(o => o.FechaHoraPromesaEntrega)
                    .Select(o => new OrdenConTrabajosDto
                    {
                        Id = o.Id,
                        NumeroOrden = o.NumeroOrden,
                        TipoOrdenId = o.TipoOrdenId,
                        ClienteNombre = o.Cliente.NombreCompleto,
                        ClienteTelefono = o.Cliente.TelefonoMovil,
                        TipoServicio=o.TipoServicio.NombreServicio,
                        VehiculoCompleto = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Color} / {o.Vehiculo.Anio}",
                        VIN = o.Vehiculo.VIN,
                        Placas = o.Vehiculo.Placas ?? "",
                        FechaHoraPromesaEntrega = o.FechaHoraPromesaEntrega,
                        EstadoOrdenId = o.EstadoOrdenId,
                        TotalTrabajos = o.TotalTrabajos,
                        TrabajosCompletados = o.TrabajosCompletados,
                        ProgresoGeneral = o.ProgresoGeneral,
                        CostoTotal = o.CostoTotal,
                        Trabajos = o.Trabajos
                            .Where(t => t.Activo)
                            .Select(t => new TrabajoDto
                            {
                                Id = t.Id,
                                Trabajo = t.Trabajo,
                                TecnicoNombre = t.TecnicoAsignado != null ? t.TecnicoAsignado.NombreCompleto : null,
                                EstadoTrabajo = t.EstadoTrabajo,
                                FechaHoraInicio = t.FechaHoraInicio,
                                FechaHoraTermino = t.FechaHoraTermino
                            }).ToList()
                    })
                    .ToListAsync();

                return Ok(ordenes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener órdenes");
                return StatusCode(500, new { Message = "Error al obtener órdenes" });
            }
        }

        [HttpGet("Jefe-Taller/{tipoOrdenId}")]// Para obtener todas las ordenes generales 
        public async Task<IActionResult> ObtenerOrdenesPorTipo_Jefe(int tipoOrdenId)
        {
            try
            {
                var ordenes = await _db.OrdenesGenerales
                    .Include(o => o.Cliente)
                    .Include(o => o.Vehiculo)
                    .Include(o => o.Trabajos.Where(t => t.Activo))
                        .ThenInclude(t => t.TecnicoAsignado)
                    .Where(o => o.TipoOrdenId == tipoOrdenId
                             && o.Activo
                             && new[] { 1, 2, 3 }.Contains(o.EstadoOrdenId))
                    .OrderBy(o => o.FechaHoraPromesaEntrega)
                    .Select(o => new OrdenConTrabajosDto
                    {
                        Id = o.Id,
                        NumeroOrden = o.NumeroOrden,
                        TipoOrdenId = o.TipoOrdenId,
                        ClienteNombre = o.Cliente.NombreCompleto,
                        ClienteTelefono = o.Cliente.TelefonoMovil,
                        TipoServicio = o.TipoServicio.NombreServicio,
                        VehiculoCompleto = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Color} / {o.Vehiculo.Anio}",
                        VIN = o.Vehiculo.VIN,
                        Placas = o.Vehiculo.Placas ?? "",
                        FechaHoraPromesaEntrega = o.FechaHoraPromesaEntrega,
                        EstadoOrdenId = o.EstadoOrdenId,
                        TotalTrabajos = o.TotalTrabajos,
                        TrabajosCompletados = o.TrabajosCompletados,
                        ProgresoGeneral = o.ProgresoGeneral,
                        CostoTotal = o.CostoTotal,
                        Trabajos = o.Trabajos
                            .Where(t => t.Activo)
                            .Select(t => new TrabajoDto
                            {
                                Id = t.Id,
                                Trabajo = t.Trabajo,
                                TecnicoNombre = t.TecnicoAsignado != null ? t.TecnicoAsignado.NombreCompleto : null,
                                EstadoTrabajo = t.EstadoTrabajo,
                                FechaHoraInicio = t.FechaHoraInicio,
                                FechaHoraTermino = t.FechaHoraTermino
                            }).ToList()
                    })
                    .ToListAsync();

                return Ok(ordenes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener órdenes");
                return StatusCode(500, new { Message = "Error al obtener órdenes" });
            }
        }

        /// <summary>
        /// Obtener orden detallada con todos sus trabajos
        /// GET api/Ordenes/detalle/{ordenId}
        /// </summary>
        [HttpGet("detalle/{ordenId}")]
        public async Task<IActionResult> ObtenerDetalleOrden(int ordenId)
        {
            try
            {
                var orden = await _db.OrdenesGenerales
                    .Include(o => o.Cliente)
                    .Include(o => o.Vehiculo)
                    .Include(o => o.Asesor)
                    .Include(o => o.Trabajos.Where(t => t.Activo))
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
                        VehiculoCompleto = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Color} / {o.Vehiculo.Anio}",
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
                                Trabajo = t.Trabajo,
                                TecnicoAsignadoId = t.TecnicoAsignadoId,
                                TecnicoNombre = t.TecnicoAsignado != null ? t.TecnicoAsignado.NombreCompleto : null,
                                FechaHoraAsignacionTecnico = t.FechaHoraAsignacionTecnico,
                                FechaHoraInicio = t.FechaHoraInicio,
                                FechaHoraTermino = t.FechaHoraTermino,
                                IndicacionesTrabajo = t.IndicacionesTrabajo,
                                ComentariosTecnico = t.ComentariosTecnico,
                                ComentariosJefeTaller = t.ComentariosJefeTaller,
                                EstadoTrabajo = t.EstadoTrabajo,
                                EstadoTrabajoNombre = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.NombreEstado : null,
                                ColorEstado = t.EstadoTrabajoNavegacion != null ? t.EstadoTrabajoNavegacion.Color : null
                            }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (orden == null)
                    return NotFound(new { Message = "Orden no encontrada" });

                return Ok(orden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener detalle de orden {ordenId}");
                return StatusCode(500, new { Message = "Error al obtener detalle de orden" });
            }
        }

        /// <summary>
        /// Cancelar orden
        /// PUT api/Ordenes/cancelar/{ordenId}
        /// </summary>
        [HttpPut("cancelar/{ordenId}")]
        public async Task<IActionResult> CancelarOrden(int ordenId)
        {
            try
            {
                var orden = await _db.OrdenesGenerales.FindAsync(ordenId);
                if (orden == null)
                    return NotFound(new { Success = false, Message = "Orden no encontrada" });

                orden.EstadoOrdenId = 5; // Cancelada
                orden.Activo = false;

                // Cancelar todos los trabajos pendientes
                var trabajos = await _db.TrabajosPorOrden
                    .Where(t => t.OrdenGeneralId == ordenId && t.Activo && t.EstadoTrabajo == 1)
                    .ToListAsync();

                foreach (var trabajo in trabajos)
                {
                    trabajo.EstadoTrabajo = 5; // Cancelado
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Orden {ordenId} cancelada");

                return Ok(new { Success = true, Message = "Orden cancelada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cancelar orden {ordenId}");
                return StatusCode(500, new { Success = false, Message = "Error al cancelar orden" });
            }
        }

        /// <summary>
        /// Entregar orden (solo si todos los trabajos están completados)
        /// PUT api/Ordenes/entregar/{ordenId}
        /// </summary>
        
        [HttpPut("entregar/{ordenId}")]
        public async Task<IActionResult> EntregarOrden(int ordenId)
        {
            try
            {
                var orden = await _db.OrdenesGenerales
                    .Include(o => o.Trabajos)
                    .FirstOrDefaultAsync(o => o.Id == ordenId);

                if (orden == null)
                    return NotFound(new { Success = false, Message = "Orden no encontrada" });

                // Verificar que todos los trabajos estén completados
                var trabajosPendientes = orden.Trabajos.Count(t => t.Activo && t.EstadoTrabajo != 4);
                if (trabajosPendientes > 0)
                    return BadRequest(new
                    {
                        Success = false,
                        Message = $"No se puede entregar. Hay {trabajosPendientes} trabajo(s) sin completar"
                    });

                orden.EstadoOrdenId = 4; // Entregada
                orden.FechaEntrega = DateTime.Now;

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Orden {ordenId} entregada");

                return Ok(new { Success = true, Message = "Orden entregada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al entregar orden {ordenId}");
                return StatusCode(500, new { Success = false, Message = "Error al entregar orden" });
            }
        }

        [HttpGet("historial-servicio/{vehiculoId}")]
        [ProducesResponseType(typeof(HistorialVehiculoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerHistorialServicio(int vehiculoId)
        {
            try
            {
                var fechaLimite = DateTime.Now.AddMonths(-12);

                var ordenes = await _db.OrdenesGenerales
                    .Include(o => o.TipoServicio) // Necesario para o.TipoServicio.NombreServicio
                    .Where(o => o.VehiculoId == vehiculoId
                             && o.Activo
                             && o.TipoOrdenId == 1   // Solo órdenes de servicio
                             && o.EstadoOrdenId == 4 // Solo órdenes ENTREGADAS
                             && o.FechaCreacion >= fechaLimite)
                    .OrderByDescending(o => o.FechaCreacion)
                    .ToListAsync();

                var historial = ordenes.Select(o => new HistorialServicioDto
                {
                    NumeroOrden = o.NumeroOrden,
                    FechaServicio = o.FechaCreacion,
                    TipoServicio = o.TipoServicio.NombreServicio ?? "",
                    KilometrajeRegistrado = o.KilometrajeActual,
                    ObservacionesAsesor = o.ObservacionesAsesor ?? ""
                }).ToList();

                var ultimoServicio = historial.FirstOrDefault();

                return Ok(new HistorialVehiculoResponse
                {
                    Success = true,
                    Message = historial.Any() ? "Historial encontrado" : "Sin servicios recientes",
                    Historial = historial,
                    UltimoServicio = ultimoServicio?.TipoServicio ?? "",
                    UltimoKilometraje = ultimoServicio?.KilometrajeRegistrado ?? 0,
                    UltimaFechaServicio = ultimoServicio?.FechaServicio
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener historial del vehículo {vehiculoId}");

                return StatusCode(500, new HistorialVehiculoResponse
                {
                    Success = false,
                    Message = "Error al obtener historial de servicios",
                    Historial = new List<HistorialServicioDto>()
                });
            }
        }

    }
}