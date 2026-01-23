using CarSlineAPI.Data;
using CarSlineAPI.Models.DTOs;
using CarSlineAPI.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarSlineAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RefaccionesTrabajoController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RefaccionesTrabajoController> _logger;

        public RefaccionesTrabajoController(ApplicationDbContext db, ILogger<RefaccionesTrabajoController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Agregar múltiples refacciones a un trabajo
        /// POST api/RefaccionesTrabajo/agregar
        /// </summary>
        [HttpPost("agregar")]
        [ProducesResponseType(typeof(AgregarRefaccionesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AgregarRefacciones([FromBody] AgregarRefaccionesTrabajoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AgregarRefaccionesResponse
                {
                    Success = false,
                    Message = "Datos inválidos"
                });
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // Verificar que el trabajo existe
                var trabajo = await _db.TrabajosPorOrden
                    .Include(t => t.OrdenGeneral)
                    .FirstOrDefaultAsync(t => t.Id == request.TrabajoId && t.Activo);

                if (trabajo == null)
                {
                    return NotFound(new AgregarRefaccionesResponse
                    {
                        Success = false,
                        Message = "Trabajo no encontrado"
                    });
                }

                // Validar que el trabajo no esté completado o cancelado
                if (trabajo.EstadoTrabajo == 4 || trabajo.EstadoTrabajo == 6)
                {
                    return BadRequest(new AgregarRefaccionesResponse
                    {
                        Success = false,
                        Message = "No se pueden agregar refacciones a un trabajo completado o cancelado"
                    });
                }

                var refaccionesAgregadas = new List<RefaccionTrabajoDto>();
                decimal totalRefacciones = 0;

                // Procesar cada refacción
                foreach (var refaccionDto in request.Refacciones)
                {
                    var refaccionTrabajo = new Refacciontrabajo
                    {
                        TrabajoId = request.TrabajoId,
                        OrdenGeneralId = trabajo.OrdenGeneralId,
                        Refaccion = refaccionDto.Refaccion,
                        Cantidad = refaccionDto.Cantidad,
                        PrecioUnitario = refaccionDto.PrecioUnitario
                    };

                    _db.Set<Refacciontrabajo>().Add(refaccionTrabajo);
                    await _db.SaveChangesAsync(); // Para obtener el ID y el Total calculado

                    // Recargar para obtener el Total calculado por la base de datos
                    await _db.Entry(refaccionTrabajo).ReloadAsync();

                    var total = refaccionDto.Cantidad * refaccionDto.PrecioUnitario;
                    totalRefacciones += total;

                    refaccionesAgregadas.Add(new RefaccionTrabajoDto
                    {
                        Id = refaccionTrabajo.Id,
                        TrabajoId = refaccionTrabajo.TrabajoId,
                        OrdenGeneralId = refaccionTrabajo.OrdenGeneralId,
                        Refaccion = refaccionTrabajo.Refaccion,
                        Cantidad = refaccionTrabajo.Cantidad,
                        PrecioUnitario = refaccionTrabajo.PrecioUnitario,
                        Total = total
                    });
                }

                // Actualizar el total de refacciones en el trabajo
                trabajo.RefaccionesTotal = totalRefacciones;
                await _db.SaveChangesAsync();

                // Actualizar el progreso general de la orden
                await ActualizarProgresoOrden(trabajo.OrdenGeneralId);

                await transaction.CommitAsync();

                _logger.LogInformation(
                    $"Se agregaron {refaccionesAgregadas.Count} refacciones al trabajo {request.TrabajoId}. Total: ${totalRefacciones:F2}");

                return Ok(new AgregarRefaccionesResponse
                {
                    Success = true,
                    Message = $"Se agregaron {refaccionesAgregadas.Count} refacción(es) exitosamente",
                    RefaccionesAgregadas = refaccionesAgregadas,
                    TotalRefacciones = totalRefacciones,
                    CantidadRefacciones = refaccionesAgregadas.Count
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error al agregar refacciones al trabajo {request.TrabajoId}");
                return StatusCode(500, new AgregarRefaccionesResponse
                {
                    Success = false,
                    Message = "Error al agregar refacciones"
                });
            }
        }

        /// <summary>
        /// Obtener todas las refacciones de un trabajo
        /// GET api/RefaccionesTrabajo/trabajo/{trabajoId}
        /// </summary>
        [HttpGet("trabajo/{trabajoId}")]
        [ProducesResponseType(typeof(ObtenerRefaccionesTrabajoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerRefaccionesPorTrabajo(int trabajoId)
        {
            try
            {
                var trabajo = await _db.TrabajosPorOrden
                    .Include(t => t.OrdenGeneral)
                    .FirstOrDefaultAsync(t => t.Id == trabajoId);

                if (trabajo == null)
                {
                    return NotFound(new ObtenerRefaccionesTrabajoResponse
                    {
                        Success = false,
                        Message = "Trabajo no encontrado"
                    });
                }

                var refacciones = await _db.Set<Refacciontrabajo>()
                    .Where(r => r.TrabajoId == trabajoId)
                    .Select(r => new RefaccionTrabajoDto
                    {
                        Id = r.Id,
                        TrabajoId = r.TrabajoId,
                        OrdenGeneralId = r.OrdenGeneralId,
                        Refaccion = r.Refaccion,
                        Cantidad = r.Cantidad,
                        PrecioUnitario = r.PrecioUnitario,
                        Total = r.Cantidad * r.PrecioUnitario
                    })
                    .ToListAsync();

                var total = refacciones.Sum(r => r.Total);

                return Ok(new ObtenerRefaccionesTrabajoResponse
                {
                    Success = true,
                    Message = refacciones.Any()
                        ? $"Se encontraron {refacciones.Count} refacción(es)"
                        : "No hay refacciones registradas",
                    TrabajoId = trabajoId,
                    NumeroOrden = trabajo.OrdenGeneral?.NumeroOrden ?? "",
                    Refacciones = refacciones,
                    TotalRefacciones = total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener refacciones del trabajo {trabajoId}");
                return StatusCode(500, new ObtenerRefaccionesTrabajoResponse
                {
                    Success = false,
                    Message = "Error al obtener refacciones"
                });
            }
        }

        /// <summary>
        /// Eliminar una refacción específica de un trabajo
        /// DELETE api/RefaccionesTrabajo/{refaccionId}
        /// </summary>
        [HttpDelete("{refaccionId}")]
        [ProducesResponseType(typeof(AgregarRefaccionesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EliminarRefaccion(int refaccionId)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var refaccion = await _db.Set<Refacciontrabajo>()
                    .Include(r => r.TrabajoPorOrden)
                    .FirstOrDefaultAsync(r => r.Id == refaccionId);

                if (refaccion == null)
                {
                    return NotFound(new AgregarRefaccionesResponse
                    {
                        Success = false,
                        Message = "Refacción no encontrada"
                    });
                }

                // Verificar que el trabajo no esté completado
                if (refaccion.TrabajoPorOrden?.EstadoTrabajo == 4)
                {
                    return BadRequest(new AgregarRefaccionesResponse
                    {
                        Success = false,
                        Message = "No se pueden eliminar refacciones de un trabajo completado"
                    });
                }

                var trabajoId = refaccion.TrabajoId;
                var ordenId = refaccion.OrdenGeneralId;
                var totalEliminado = refaccion.PrecioUnitario * refaccion.Cantidad;

                // Eliminar la refacción
                _db.Set<Refacciontrabajo>().Remove(refaccion);

                // Actualizar el total del trabajo
                var trabajo = await _db.TrabajosPorOrden.FindAsync(trabajoId);
                if (trabajo != null)
                {
                    trabajo.RefaccionesTotal -= totalEliminado;
                    if (trabajo.RefaccionesTotal < 0) trabajo.RefaccionesTotal = 0;
                }

                await _db.SaveChangesAsync();

                // Actualizar progreso de la orden
                await ActualizarProgresoOrden(ordenId);

                await transaction.CommitAsync();

                _logger.LogInformation($"Refacción {refaccionId} eliminada del trabajo {trabajoId}");

                return Ok(new AgregarRefaccionesResponse
                {
                    Success = true,
                    Message = "Refacción eliminada exitosamente"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error al eliminar refacción {refaccionId}");
                return StatusCode(500, new AgregarRefaccionesResponse
                {
                    Success = false,
                    Message = "Error al eliminar refacción"
                });
            }
        }

        /// <summary>
        /// Obtener todas las refacciones de una orden
        /// GET api/RefaccionesTrabajo/orden/{ordenId}
        /// </summary>
        [HttpGet("orden/{ordenId}")]
        [ProducesResponseType(typeof(ObtenerRefaccionesTrabajoResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerRefaccionesPorOrden(int ordenId)
        {
            try
            {
                var orden = await _db.OrdenesGenerales.FindAsync(ordenId);

                if (orden == null)
                {
                    return NotFound(new ObtenerRefaccionesTrabajoResponse
                    {
                        Success = false,
                        Message = "Orden no encontrada"
                    });
                }

                var refacciones = await _db.Set<Refacciontrabajo>()
                    .Where(r => r.OrdenGeneralId == ordenId)
                    .Select(r => new RefaccionTrabajoDto
                    {
                        Id = r.Id,
                        TrabajoId = r.TrabajoId,
                        OrdenGeneralId = r.OrdenGeneralId,
                        Refaccion = r.Refaccion,
                        Cantidad = r.Cantidad,
                        PrecioUnitario = r.PrecioUnitario,
                        Total = r.Cantidad * r.PrecioUnitario
                    })
                    .ToListAsync();

                var total = refacciones.Sum(r => r.Total);

                return Ok(new ObtenerRefaccionesTrabajoResponse
                {
                    Success = true,
                    Message = refacciones.Any()
                        ? $"Se encontraron {refacciones.Count} refacción(es)"
                        : "No hay refacciones registradas",
                    TrabajoId = 0,
                    NumeroOrden = orden.NumeroOrden,
                    Refacciones = refacciones,
                    TotalRefacciones = total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener refacciones de la orden {ordenId}");
                return StatusCode(500, new ObtenerRefaccionesTrabajoResponse
                {
                    Success = false,
                    Message = "Error al obtener refacciones"
                });
            }
        }

        // ============================================
        // MÉTODOS PRIVADOS AUXILIARES
        // ============================================

        private async Task ActualizarProgresoOrden(int ordenId)
        {
            var orden = await _db.OrdenesGenerales
                .Include(o => o.Trabajos)
                .FirstOrDefaultAsync(o => o.Id == ordenId);

            if (orden == null) return;

            // Recalcular totales
            orden.TotalTrabajos = orden.Trabajos.Count(t => t.Activo);
            orden.TrabajosCompletados = orden.Trabajos.Count(t => t.Activo && t.EstadoTrabajo == 4);

            // Calcular progreso
            if (orden.TotalTrabajos > 0)
            {
                orden.ProgresoGeneral = Math.Round(
                    ((decimal)orden.TrabajosCompletados / orden.TotalTrabajos) * 100, 2);
            }

            await _db.SaveChangesAsync();
        }
    }
}