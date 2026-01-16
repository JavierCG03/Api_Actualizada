using CarSlineAPI.Data;
using CarSlineAPI.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarSlineAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BusquedaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<BusquedaController> _logger;

        public BusquedaController(ApplicationDbContext db, ILogger<BusquedaController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Búsqueda unificada inteligente
        /// GET api/Busqueda/unificada?termino=texto
        /// </summary>
        [HttpGet("unificada")]
        [ProducesResponseType(typeof(BusquedaUnificadaResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> BusquedaUnificada([FromQuery] string termino)
        {
            if (string.IsNullOrWhiteSpace(termino) || termino.Length < 3)
            {
                return Ok(new BusquedaUnificadaResponse
                {
                    Success = false,
                    Message = "Ingresa al menos 3 caracteres para buscar",
                    Resultados = new List<ResultadoBusquedaDto>()
                });
            }

            try
            {
                var resultados = new List<ResultadoBusquedaDto>();
                var terminoUpper = termino.ToUpper().Trim();

                // 🔍 DETECTAR TIPO DE BÚSQUEDA

                // 1️⃣ ¿Es un número de orden? (Formato: ABC-123456)
                if (EsNumeroOrden(terminoUpper))
                {
                    var ordenesEncontradas = await BuscarOrdenes(terminoUpper);
                    resultados.AddRange(ordenesEncontradas);
                }

                // 2️⃣ ¿Son 4 dígitos del VIN?
                if (terminoUpper.Length == 4 && terminoUpper.All(char.IsLetterOrDigit))
                {
                    var vehiculosEncontrados = await BuscarVehiculosPorVIN(terminoUpper);
                    resultados.AddRange(vehiculosEncontrados);
                }

                // 3️⃣ ¿Es nombre de cliente? (Si tiene letras y más de 3 caracteres)
                if (terminoUpper.Length >= 3 && terminoUpper.Any(char.IsLetter))
                {
                    var clientesEncontrados = await BuscarClientes(terminoUpper);
                    resultados.AddRange(clientesEncontrados);
                }

                // 🎯 RESPUESTA
                if (!resultados.Any())
                {
                    return Ok(new BusquedaUnificadaResponse
                    {
                        Success = false,
                        Message = $"No se encontraron resultados para '{termino}'",
                        Resultados = new List<ResultadoBusquedaDto>(),
                        TerminoBusqueda = termino
                    });
                }

                return Ok(new BusquedaUnificadaResponse
                {
                    Success = true,
                    Message = $"Se encontraron {resultados.Count} resultado(s)",
                    Resultados = resultados,
                    TotalResultados = resultados.Count,
                    TerminoBusqueda = termino
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en búsqueda unificada: {termino}");
                return StatusCode(500, new BusquedaUnificadaResponse
                {
                    Success = false,
                    Message = "Error al realizar la búsqueda",
                    Resultados = new List<ResultadoBusquedaDto>()
                });
            }
        }

        /// <summary>
        /// Obtener orden por número de orden
        /// GET api/Busqueda/orden/{numeroOrden}
        /// </summary>
        [HttpGet("orden/{numeroOrden}")]
        [ProducesResponseType(typeof(BuscarOrdenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BuscarOrdenPorNumero(string numeroOrden)
        {
            try
            {
                var orden = await _db.OrdenesGenerales
                    .Include(o => o.Cliente)
                    .Include(o => o.Vehiculo)
                    .Where(o => o.NumeroOrden == numeroOrden.ToUpper() && o.Activo)
                    .Select(o => new OrdenSimpleDto
                    {
                        Id = o.Id,
                        NumeroOrden = o.NumeroOrden,
                        ClienteNombre = o.Cliente.NombreCompleto,
                        VehiculoInfo = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo} {o.Vehiculo.Anio}",
                        FechaCreacion = o.FechaCreacion,
                        EstadoOrdenId = o.EstadoOrdenId,
                        // ✅ Estado se asigna después en memoria
                        EstadoOrden = "" // Temporal
                    })
                    .FirstOrDefaultAsync();

                if (orden == null)
                {
                    return NotFound(new BuscarOrdenResponse
                    {
                        Success = false,
                        Message = "Orden no encontrada"
                    });
                }

                // ✅ Asignar el estado después de traer los datos
                orden.EstadoOrden = GetEstadoOrdenTexto(orden.EstadoOrdenId);

                return Ok(new BuscarOrdenResponse
                {
                    Success = true,
                    Message = "Orden encontrada",
                    Orden = orden
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al buscar orden: {numeroOrden}");
                return StatusCode(500, new BuscarOrdenResponse
                {
                    Success = false,
                    Message = "Error al buscar orden"
                });
            }
        }

        // ============================================
        // MÉTODOS PRIVADOS DE BÚSQUEDA
        // ============================================

        private async Task<List<ResultadoBusquedaDto>> BuscarClientes(string nombre)
        {
            var clientes = await _db.Clientes
                .Where(c => c.Activo && c.NombreCompleto.Contains(nombre))
                .OrderBy(c => c.NombreCompleto)
                .Take(10)
                .Select(c => new ResultadoBusquedaDto
                {
                    Id = c.Id,
                    Tipo = TipoResultadoBusqueda.Cliente,
                    TipoTexto = "Cliente",
                    TituloPrincipal = c.NombreCompleto,
                    Subtitulo = $"Tel: {c.TelefonoMovil}",
                    Detalle = $"RFC: {c.RFC}",
                    IconoColor = "#4CAF50",
                    Icono = "👤"
                })
                .ToListAsync();

            return clientes;
        }

        private async Task<List<ResultadoBusquedaDto>> BuscarVehiculosPorVIN(string ultimos4)
        {
            var vehiculos = await _db.Vehiculos
                .Include(v => v.Cliente)
                .Where(v => v.Activo && v.VIN.EndsWith(ultimos4))
                .OrderBy(v => v.Marca)
                .Take(10)
                .Select(v => new ResultadoBusquedaDto
                {
                    Id = v.Id,
                    Tipo = TipoResultadoBusqueda.Vehiculo,
                    TipoTexto = "Vehículo",
                    TituloPrincipal = $"{v.Marca} {v.Modelo} {v.Anio}",
                    Subtitulo = $"VIN: ...{v.VIN.Substring(Math.Max(0, v.VIN.Length - 4))} | {v.Color}",
                    Detalle = $"Cliente: {v.Cliente.NombreCompleto}",
                    IconoColor = "#2196F3",
                    Icono = "🚗"
                })
                .ToListAsync();

            return vehiculos;
        }

        private async Task<List<ResultadoBusquedaDto>> BuscarOrdenes(string numeroOrden)
        {
            // ✅ SOLUCIÓN: Traer los datos primero, luego aplicar métodos en memoria
            var ordenes = await _db.OrdenesGenerales
                .Include(o => o.Cliente)
                .Include(o => o.Vehiculo)
                .Where(o => o.Activo && o.NumeroOrden.Contains(numeroOrden))
                .OrderByDescending(o => o.FechaCreacion)
                .Take(10)
                .Select(o => new
                {
                    o.Id,
                    o.NumeroOrden,
                    ClienteNombre = o.Cliente.NombreCompleto,
                    VehiculoInfo = $"{o.Vehiculo.Marca} {o.Vehiculo.Modelo}",
                    o.EstadoOrdenId
                })
                .ToListAsync(); // ✅ Ejecuta la query aquí

            // ✅ Ahora aplicamos las transformaciones en memoria
            var resultados = ordenes.Select(o => new ResultadoBusquedaDto
            {
                Id = o.Id,
                Tipo = TipoResultadoBusqueda.Orden,
                TipoTexto = "Orden",
                TituloPrincipal = o.NumeroOrden,
                Subtitulo = o.ClienteNombre,
                Detalle = $"{o.VehiculoInfo} | {GetEstadoOrdenTexto(o.EstadoOrdenId)}",
                IconoColor = GetColorEstadoOrden(o.EstadoOrdenId),
                Icono = "📋"
            }).ToList();

            return resultados;
        }

        // ============================================
        // MÉTODOS AUXILIARES
        // ============================================

        private static bool EsNumeroOrden(string texto)
        {
            // Formato válido: ABC-123456 (3 letras, guión, 6 dígitos)
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 4)
                return false;

            // Verificar si contiene guión
            if (!texto.Contains("-"))
                return false;

            var partes = texto.Split('-');
            if (partes.Length != 2)
                return false;

            // Verificar prefijo (3 letras)
            var prefijo = partes[0];
            if (prefijo.Length != 3 || !prefijo.All(char.IsLetter))
                return false;

            // Verificar número (dígitos)
            var numero = partes[1];
            if (numero.Length < 1 || !numero.All(char.IsDigit))
                return false;

            return true;
        }

        // ✅ MÉTODOS AHORA SON PRIVADOS (no static, porque se usan después de ToListAsync)
        private static string GetEstadoOrdenTexto(int estadoId)
        {
            return estadoId switch
            {
                1 => "Pendiente",
                2 => "En Proceso",
                3 => "Finalizada",
                _ => "Desconocido"
            };
        }

        private static string GetColorEstadoOrden(int estadoId)
        {
            return estadoId switch
            {
                1 => "#FFA500", // Pendiente - Naranja
                2 => "#2196F3", // En Proceso - Azul
                3 => "#4CAF50", // Finalizada - Verde
                _ => "#757575"  // Desconocido - Gris
            };
        }
    }
}