// Sesión compartida entre pestañas para TimeTracker.Web (ADR 0028). Envoltorio mínimo del almacenamiento local del navegador y
// del evento «storage» (que el navegador dispara solo en las OTRAS pestañas). Sin lógica de sesión, sin eval ni recursos externos:
// los errores (almacenamiento bloqueado) se propagan a C#, que degrada a una sesión en memoria.
(function () {
    let listener = null;

    window.timeTrackerSession = {
        get: function (key) {
            return window.localStorage.getItem(key);
        },
        set: function (key, value) {
            window.localStorage.setItem(key, value);
        },
        remove: function (key) {
            window.localStorage.removeItem(key);
        },
        // Lee y borra la sesión que guardaba la versión anterior solo para la pestaña (para migrarla una vez).
        takeLegacy: function (key) {
            const value = window.sessionStorage.getItem(key);
            window.sessionStorage.removeItem(key);
            return value;
        },
        subscribe: function (reference) {
            window.timeTrackerSession.unsubscribe();
            listener = function (event) {
                if (event.storageArea === window.localStorage) {
                    reference.invokeMethodAsync("OnStorageChanged", event.key, event.newValue).catch(function () { });
                }
            };
            window.addEventListener("storage", listener);
        },
        unsubscribe: function () {
            if (listener) {
                window.removeEventListener("storage", listener);
                listener = null;
            }
        }
    };
})();
