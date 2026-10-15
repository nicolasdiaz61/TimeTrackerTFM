// Zona horaria del navegador para TimeTracker.Web. Solo expone dos funciones puras: el desfase local de un instante y
// el instante en que empieza un día local. La Web calcula el resto (ver ILocalTimeZone). Sin eval ni recursos externos.
window.timeTrackerLocalTime = {
    // Minutos que hay que sumar a UTC para obtener la hora local en ese instante (positivo al este de Greenwich).
    offsetMinutes: function (epochMilliseconds) {
        return -new Date(epochMilliseconds).getTimezoneOffset();
    },
    // Milisegundos desde 1970 (UTC) de las 00:00 locales del día indicado (mes de 1 a 12).
    startOfDay: function (year, month, day) {
        return new Date(year, month - 1, day).getTime();
    }
};
