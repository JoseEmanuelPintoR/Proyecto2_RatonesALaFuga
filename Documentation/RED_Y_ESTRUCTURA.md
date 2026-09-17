# Estructura de Ratones a la fuga

## Organización

La escena tiene un componente principal, `RatonesGame`, que une la interfaz, la vista 3D y la conexión. La construcción visual ocurre en ejecución para que el proyecto pueda abrirse sin descargar modelos de terceros ni conectar referencias manualmente.

Las carpetas separan responsabilidades:

- `Core`: reglas y estado de la partida; no depende de Unity.
- `Network`: sockets TCP y mensajes; tampoco depende de Unity.
- `Gameplay`: cámara, personajes, escenario, audio y controles de Unity.
- `Editor`: comandos para abrir la escena, configurar el proyecto y compilar Android.
- `Resources/Ratones`: shaders del renderizador integrado, incluidos expresamente en la compilación.
- `Tests`: pruebas ejecutables de reglas y sockets con .NET 8, sin paquetes de terceros.

## Servidor y clientes

Al pulsar Crear sala, el dispositivo abre un `TcpListener` en el puerto seleccionado. El anfitrión controla el primer ratón localmente y recibe por TCP hasta tres clientes. La simulación del anfitrión avanza a 30 pasos por segundo y transmite el estado 15 veces por segundo.

Cada cliente envía únicamente la intención de movimiento y las solicitudes de poderes. No decide coordenadas finales, puntaje, recolecciones ni ganadores. El servidor limita y normaliza el movimiento, comprueba muebles, identifica el objeto más cercano y actualiza cada queso una sola vez.

La conexión asigna la identidad. El cliente no puede elegir otro ID dentro de los mensajes de movimiento. Iniciar y reiniciar la ronda son acciones exclusivas del anfitrión.

La lectura y escritura de cada socket ocurren en hilos separados. Los mensajes entrantes pasan por colas; la simulación y la vista de Unity se actualizan en el hilo principal. Las colas tienen límites y los estados antiguos pendientes de enviar se sustituyen por el estado más reciente.

## Protocolo

Se utiliza UTF-8 con un salto de línea al final de cada mensaje. Los textos libres se codifican en Base64 para conservar nombres con acentos y evitar que los separadores modifiquen la estructura. El parser conserva mensajes que lleguen fragmentados y separa los que lleguen juntos.

| Mensaje | Dirección | Propósito |
|---|---|---|
| `HELLO|RATONES1|nombreBase64` | Cliente → servidor | Solicitud de entrada y versión del protocolo. |
| `WELCOME|id` | Servidor → cliente | Identidad asignada. |
| `I|x|z|azucar|bloqueo` | Cliente → servidor | Movimiento normalizado y poderes solicitados. |
| `S|...` | Servidor → cliente | Estado: fase, ronda, reloj, jugadores, objetos y ganadores. |
| `PING` / `PONG` | Ambos sentidos | Comprobar que la conexión responde. |
| `ERROR|mensajeBase64` | Servidor → cliente | Sala llena, ronda iniciada o mensaje incompatible. |

No se utiliza serialización binaria de objetos. Los mensajes de entrada al servidor tienen un límite de 512 caracteres; el receptor tiene un límite de trama de 32 KiB. Los números no finitos son rechazados.

## Decisiones de esta versión

- Conexión directa por IP y puerto en una red local. No hay servicio externo de cuentas ni descubrimiento automático de salas.
- La sala se cierra si sale el anfitrión; no hay migración de host.
- Los jugadores desconectados no pueden ganar. Con menos de dos participantes se cancela la ronda.
- No se permite ingresar a una ronda en curso. Después de volver a la sala se admiten jugadores hasta completar cuatro.
- Se usa un solo modelo de obtención de poderes: recoger objetos especiales. Cada ratón almacena una carga de cada tipo.
- La cámara suaviza posiciones recibidas. No se implementa predicción avanzada ni compensación de latencia para Internet; el objetivo de esta versión es la red local de la actividad.

## Probar las clases sin Unity

Con .NET 8 instalado, ejecuta desde la carpeta del proyecto:

```bash
dotnet run --project Tests/Ratones.Tests.csproj
```

Las pruebas compilan exactamente los archivos de `Core` y `Network` utilizados por Unity. No son una implementación alternativa de las reglas. Las pruebas de sockets abren conexiones TCP reales de loopback.

## Cambiar el mapa

Los obstáculos están declarados una sola vez en `KitchenMap.Blocks`. Cada bloque define centro X/Z, ancho, profundidad, altura y tipo de mueble. La simulación utiliza su planta para impedir cruces y la vista usa las mismas dimensiones al construir el mueble.

Al cambiar bloques, ejecuta las pruebas de generación para comprobar que los jugadores y objetos siguen apareciendo en posiciones transitables. Si añades obstáculos que cierren completamente una zona, comprueba también sus rutas de acceso desde el juego.
