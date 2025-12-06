namespace CarSlineAPI.Models.DTOs
{
    public class CrearOrdenRequest
    {
        public int TipoOrdenId { get; set; }
        public int ClienteId { get; set; }
        public int VehiculoId { get; set; }
        public int? TipoServicioId { get; set; }
        public int KilometrajeActual { get; set; }
        public DateTime FechaHoraPromesaEntrega { get; set; }
        public string? ObservacionesAsesor { get; set; }
        public List<int>? ServiciosExtraIds { get; set; }
    }

}
