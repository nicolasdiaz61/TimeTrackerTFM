// Ayudas de presentación del chat del asistente (TimeTracker.Web). Sin eval ni recursos externos.
window.timeTrackerChat = {
    // Desplaza la lista de mensajes hasta el último mensaje.
    scrollToEnd: function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },
    // Descarga un archivo generado en el cliente: Blob + enlace temporal con download; libera la URL después.
    downloadFile: function (fileName, contentType, content) {
        var blob = new Blob([content], { type: contentType });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        // Firefox y Safari inician la descarga de forma asíncrona: revocar antes la cancelaría.
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
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
