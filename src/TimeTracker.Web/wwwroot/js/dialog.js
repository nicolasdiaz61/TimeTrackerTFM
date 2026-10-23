// Ayudas de los diálogos modales (TimeTracker.Web): foco inicial, trampa de foco, bloqueo del fondo y devolución del foco. Sin recursos externos.
window.timeTrackerDialog = {
    previous: null,
    dialog: null,
    handler: null,
    open: function (dialog) {
        if (!dialog) {
            return;
        }
        this.previous = document.activeElement;
        document.body.classList.add("dialog-open");
        var selector = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";
        var first = dialog.querySelector("input:not([disabled]), select:not([disabled]), textarea:not([disabled])") || dialog.querySelector(selector) || dialog;
        first.focus();
        this.handler = function (event) {
            if (event.key !== "Tab") {
                return;
            }
            var items = Array.prototype.filter.call(dialog.querySelectorAll(selector), function (item) { return item.offsetParent !== null; });
            if (items.length === 0) {
                event.preventDefault();
                return;
            }
            var firstItem = items[0];
            var lastItem = items[items.length - 1];
            if (event.shiftKey && document.activeElement === firstItem) {
                event.preventDefault();
                lastItem.focus();
            } else if (!event.shiftKey && document.activeElement === lastItem) {
                event.preventDefault();
                firstItem.focus();
            }
        };
        dialog.addEventListener("keydown", this.handler);
        this.dialog = dialog;
    },
    close: function () {
        document.body.classList.remove("dialog-open");
        if (this.dialog && this.handler) {
            this.dialog.removeEventListener("keydown", this.handler);
        }
        this.dialog = null;
        this.handler = null;
        var target = this.previous;
        this.previous = null;
        if (target && document.body.contains(target)) {
            target.focus();
        }
    }
};
