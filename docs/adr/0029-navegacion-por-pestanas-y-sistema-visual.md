# ADR 0029: Navegación por pestañas, diálogo reutilizable y sistema visual

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

Las pantallas de responsable mezclaban varias tareas en una sola página (`/jornadas`: formulario, lista y asignación a la vez; `/equipo`: lista de personas y detalle mensual juntos) y el aspecto era el de la plantilla de Blazor con estilos sueltos. Se pidió un flujo más claro y un aspecto coherente sin añadir dependencias.

## Decisión

- **Pestañas accesibles con la pestaña en la URL.** `/equipo` (`?tab=personas|fichajes`) y `/jornadas` (`?tab=jornadas|asignacion`) usan el componente `TabList` (`role=tablist/tab/tabpanel`, `aria-selected`, tabulación itinerante y flechas izquierda/derecha). La regla «qué pestaña corresponde a un valor» está en `TabSelection` (valor desconocido o ausente: la primera), probada sin navegador. Cambiar de pestaña navega con `replace` para no llenar el historial. «Ver fichajes» elige a la persona y pasa a la pestaña de fichajes.
- **Un único diálogo modal.** `Dialog.razor` (`role=dialog` o `alertdialog`, `aria-modal`, `aria-labelledby`; Esc y clic en el fondo cierran) sirve para crear y editar una jornada y para confirmar su borrado (sin `window.confirm`). `WorkScheduleDialogs` decide qué diálogo se ve y delega toda la lógica en `WorkSchedulesState`. El foco, la trampa de foco, el bloqueo del desplazamiento y la devolución del foco pasan por el servicio inyectable `DialogFocus` y `js/dialog.js`, como `ChatViewport`: las páginas no usan `IJSRuntime`.
- **Sistema visual en `app.css`.** Variables CSS (`:root`) para colores, radios, sombras, espaciado y tipografía; clases comunes para cabecera de página, tarjetas, tablas, botones, formularios, insignias, avisos, estados vacíos y pestañas. Se apoya en Bootstrap, que ya estaba, y lo ajusta con las variables. Tema claro, contraste AA, transiciones que respetan `prefers-reduced-motion`, sin estilos en línea (CSP).

## Consecuencias

- Cada tarea tiene su pantalla y se puede enlazar (`/equipo?tab=fichajes`).
- Los diálogos y las pestañas se reutilizan en otras páginas sin repetir marcado.
- El diálogo no atrapa el foco sin JavaScript (solo pierde esa mejora).
- «Rol» y «estado de fichaje» de cada persona no se muestran en la lista de equipo: la API de equipo solo devuelve nombre y jornada; mostrarlos exigiría ampliar el contrato.

## Alternativas

- **Biblioteca de componentes (MudBlazor, Radzen...):** descartada; añade dependencias y superficie de ataque para dos componentes.
- **Bootstrap JS (modal, pestañas):** descartada; añade JavaScript y gestiona el estado fuera de Blazor.
- **Estado de la pestaña solo en memoria:** descartada; no sobrevive a recargar ni se puede enlazar.
