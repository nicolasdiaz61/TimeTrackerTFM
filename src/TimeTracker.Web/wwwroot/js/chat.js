// Ayudas de presentación del chat del asistente (TimeTracker.Web). Sin eval ni recursos externos.
window.timeTrackerChat = {
    // Desplaza la lista de mensajes hasta el último mensaje.
    scrollToEnd: function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },
    // Intro envía el formulario; Mayús+Intro (o composición con IME) conserva el comportamiento normal del textarea.
    sendOnEnter: function (textarea) {
        if (!textarea || textarea.dataset.sendOnEnter === "true") {
            return;
        }
        textarea.dataset.sendOnEnter = "true";
        textarea.addEventListener("keydown", function (event) {
            if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
                event.preventDefault();
                if (textarea.form) {
                    textarea.form.requestSubmit();
                }
            }
        });
    }
};
