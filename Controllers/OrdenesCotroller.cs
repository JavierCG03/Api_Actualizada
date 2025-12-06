using CarSlineAPI.Data;
using CarSlineAPI.Models.DTOs;
using CarSlineAPI.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

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
        /// NUEVO: Obtener historial de servicios de un vehículo (últimos 6 meses)
        /// </summary>
        [HttpGet("historial-vehiculo/{vehiculoId}")]
        [ProducesResponseType(typeof(HistorialVehiculoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerHistorialVehiculo(int vehiculoId)
        {
            try
            {
                // Calcular fecha de hace 6 meses
                var fechaLimite = DateTime.Now.AddMonths(-6);

                var historial = await _db.Ordenes
                    .Include(o => o.TipoServicio)
                    .Include(o => o.ServiciosExtra)
                        .ThenInclude(se => se.ServicioExtra)
                    .Where(o => o.VehiculoId == vehiculoId
                             && o.Activo
                             && o.EstadoOrdenId == 4 // Solo órdenes entregadas
                             && o.FechaCreacion >= fechaLimite)
                    .OrderByDescending(o => o.FechaCreacion)
                    .Select(o => new HistorialServicioDto
                    {
                        NumeroOrden = o.NumeroOrden,
                        FechaServicio = o.FechaCreacion,
                        TipoServicio = o.TipoServicio != null ? o.TipoServicio.NombreServicio : "Servicio General",
                        KilometrajeRegistrado = o.KilometrajeActual,
                        CostoTotal = o.CostoTotal,
                        ServiciosExtra = o.ServiciosExtra.Select(se => new ServicioExtraHistorialDto
                        {
                            NombreServicio = se.ServicioExtra != null ? se.ServicioExtra.NombreServicio : "",
                            Precio = se.PrecioAplicado
                        }).ToList(),
                        ObservacionesAsesor = o.ObservacionesAsesor ?? ""
                    })
                    .ToListAsync();

                // Calcular estadísticas
                var totalServicios = historial.Count;
                var costoPromedio = historial.Any() ? historial.Average(h => h.CostoTotal) : 0;
                var ultimoServicio = historial.FirstOrDefault();

                return Ok(new HistorialVehiculoResponse
                {
                    Success = true,
                    Message = $"Se encontraron {totalServicios} servicio(s) en los últimos 6 meses",
                    Historial = historial,
                    TotalServicios = totalServicios,
                    CostoPromedio = costoPromedio,
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
                    Message = "Error al obtener historial del vehículo",
                    Historial = new List<HistorialServicioDto>()
                });
            }
        }

        [HttpPost("crear")]
        public async Task<IActionResult> CrearOrden([FromBody] CrearOrdenRequest request, [FromHeader(Name = "X-User-Id")] int asesorId)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Success = false, Message = "Datos inválidos" });

            var strategy = _db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    // prefijo
                    var prefijo = request.TipoOrdenId switch
                    {
                        1 => "SRV",
                        2 => "DIA",
                        3 => "REP",
                        4 => "GAR",
                        5 => "RTO",
                        _ => "ORD"
                    };

                    // obtener max correlativo
                    var maxNumero = await _db.Ordenes
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

                    // costo base
                    decimal costoTotal = 0;
                    if (request.TipoServicioId.HasValue)
                    {
                        var tipo = await _db.TiposServicio.FindAsync(request.TipoServicioId.Value);
                        if (tipo != null) costoTotal = tipo.PrecioBase;
                    }

                    // costo extras
                    if (request.ServiciosExtraIds != null && request.ServiciosExtraIds.Any())
                    {
                        var extras = await _db.ServiciosExtra
                            .Where(e => request.ServiciosExtraIds.Contains(e.Id))
                            .ToListAsync();

                        costoTotal += extras.Sum(e => e.Precio);
                    }

                    var orden = new Orden
                    {
                        NumeroOrden = numeroOrden,
                        TipoOrdenId = request.TipoOrdenId,
                        ClienteId = request.ClienteId,
                        VehiculoId = request.VehiculoId,
                        AsesorId = asesorId,
                        TipoServicioId = request.TipoServicioId,
                        KilometrajeActual = request.KilometrajeActual,
                        EstadoOrdenId = 1,
                        FechaHoraPromesaEntrega = request.FechaHoraPromesaEntrega,
                        ObservacionesAsesor = request.ObservacionesAsesor,
                        CostoTotal = costoTotal,
                        FechaCreacion = DateTime.Now,
                        Activo = true
                    };
                    _db.Ordenes.Add(orden);
                    await _db.SaveChangesAsync();

                    var orden2 = new OrdenGeneral
                    {
                        NumeroOrden = numeroOrden,
                        TipoOrdenId = request.TipoOrdenId,
                        ClienteId = request.ClienteId,
                        VehiculoId = request.VehiculoId,
                        AsesorId = asesorId,
                        TipoServicioId = request.TipoServicioId,
                        KilometrajeActual = request.KilometrajeActual,
                        EstadoOrdenId = 1,
                        FechaHoraPromesaEntrega = request.FechaHoraPromesaEntrega,
                        CostoTotal = costoTotal,
                        FechaCreacion = DateTime.Now,
                        Activo = true
                    };

                    _db.OrdenesGenerales.Add(orden2);
                    await _db.SaveChangesAsync();

                    // insertar servicios extra aplicados
                    if (request.ServiciosExtraIds != null && request.ServiciosExtraIds.Any())
                    {
                        var extras = await _db.ServiciosExtra
                            .Where(e => request.ServiciosExtraIds.Contains(e.Id))
                            .ToListAsync();

                        foreach (var se in extras)
                        {
                            var oe = new OrdenServicioExtra
                            {
                                OrdenId = orden.Id,
                                ServicioExtraId = se.Id,
                                PrecioAplicado = se.Precio
                            };
                            _db.OrdenesServiciosExtra.Add(oe);
                        }

                        await _db.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    return Ok(new { Success = true, NumeroOrden = numeroOrden, OrdenId = orden.Id, CostoTotal = costoTotal });
                }

                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error al crear orden");
                    return StatusCode(500, new { Success = false, Message = "Error al crear orden" });
                }
            });
        }

        [HttpGet("asesor/{tipoOrdenId}")]
        public async Task<IActionResult> ObtenerOrdenesPorTipo(int tipoOrdenId, [FromHeader(Name = "X-User-Id")] int asesorId)
        {
            var ordenes = await _db.Ordenes
                .Include(o => o.Cliente)
                .Include(o => o.Vehiculo)
                .Include(o => o.TipoServicio)
                .Where(o => o.TipoOrdenId == tipoOrdenId
                         && o.AsesorId == asesorId
                         && o.Activo
                         && new[] { 1, 2, 3 }.Contains(o.EstadoOrdenId))
                .OrderBy(o => o.FechaHoraPromesaEntrega)
                .Select(o => new
                {
                    o.Id,
                    o.NumeroOrden,
                    VehiculoCompleto = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Anio}",
                    ClienteNombre = o.Cliente.NombreCompleto,
                    ClienteTelefono = o.Cliente.TelefonoMovil,
                    HoraPromesa = o.FechaHoraPromesaEntrega.ToString("HH:mm"),
                    HoraInicio = o.FechaInicioProceso.HasValue ? o.FechaInicioProceso.Value.ToString("HH:mm") : "-",
                    HoraFin = o.FechaFinalizacion.HasValue ? o.FechaFinalizacion.Value.ToString("HH:mm") : "-",
                    TipoServicio = o.TipoServicio != null ? o.TipoServicio.NombreServicio : "Sin servicio",
                    o.CostoTotal,
                    EstadoId = o.EstadoOrdenId
                })
                .ToListAsync();

            return Ok(ordenes);
        }

        [HttpPut("cancelar/{ordenId}")]
        public async Task<IActionResult> CancelarOrden(int ordenId)
        {
            var orden = await _db.Ordenes.FindAsync(ordenId);
            if (orden == null) return NotFound(new { Success = false, Message = "Orden no encontrada" });

            orden.EstadoOrdenId = 5;
            orden.Activo = false;
            await _db.SaveChangesAsync();

            return Ok(new { Success = true, Message = "Orden cancelada" });
        }

        [HttpPut("entregar/{ordenId}")]
        public async Task<IActionResult> EntregarOrden(int ordenId)
        {
            var orden = await _db.Ordenes.FindAsync(ordenId);
            if (orden == null) return NotFound(new { Success = false, Message = "Orden no encontrada" });

            orden.EstadoOrdenId = 4;
            orden.FechaEntrega = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok(new { Success = true, Message = "Orden Entregada" });
        }
    }
}