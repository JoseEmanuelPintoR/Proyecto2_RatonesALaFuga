# Registro de comprobaciones

Fecha: 16 de septiembre de 2026.

## Comprobado en el entorno de preparación

| Área | Resultado |
|---|---|
| Compilación de Core y Network | Correcta con .NET SDK 8.0.408. |
| Sala | Mínimo 2, máximo 4, solo inicia el anfitrión y sin entrada a mitad de ronda. |
| Generación | 250 semillas con 4 jugadores cada una: 50 quesos, 8 objetos de poderes, sin superposición inicial y fuera de muebles. |
| Movimiento | Velocidad base, diagonal normalizada, límites del mapa, obstáculos, entrada no finita y pérdida de input. |
| Recolección | Dos ratones disputando el mismo queso: suma un punto total y no vuelve a recogerse. |
| Poderes | Carga, consumo, límite de una carga, velocidad ×1,5, duración, bloqueo, alcance y línea de visión. |
| Resultados | Tiempo, desempate, victoria compartida, cancelación al quedar uno y reinicio. |
| Protocolo | Estado completo, Unicode, delimitadores, números inválidos y mensajes incompletos. |
| Transporte TCP | Tramas enviadas byte a byte, Unicode y múltiples mensajes consecutivos. |
| Sesión real de sockets | Un anfitrión y tres clientes en el mismo equipo: quinto rechazado, inicio sincronizado, movimiento, resultados, vuelta a sala y liberación de cupo. |
| Sintaxis C# | Archivos revisados con un parser de C#. Esta revisión por sí sola no certifica las APIs de Unity. |

La ejecución automatizada produjo **9 grupos correctos y 491 883 comprobaciones**. La mayoría de estas comprobaciones corresponden a posiciones y separaciones del generador; no representan cientos de miles de partidas ejecutadas.

## Pendiente de comprobar en Unity y equipos reales

- Importación y compilación de todos los componentes del proyecto en Unity 6.0.
- Compilación de shaders y aspecto de modelos, iluminación, textos, sonidos y botones.
- Tamaño de la interfaz, zona segura y joystick multitáctil en pantallas físicas.
- Generación e instalación de APK ARM64.
- Comunicación entre dos y cuatro dispositivos conectados por Wi-Fi.
- FPS, temperatura y consumo de batería en los celulares del grupo.
- Grabación del video de evidencia exigido por la guía de la asignatura.

No se incluye una captura presentada como ejecución real ni se afirma haber probado el proyecto en un celular. Unity y el SDK Android no estaban instalados en el entorno de preparación.

## Prueba recomendada para la entrega de clase

1. Abrir la misma compilación en dos dispositivos y conectarlos a la sala.
2. Mostrar que un solo jugador no puede iniciar; iniciar cuando haya dos.
3. Recoger quesos en ambos dispositivos y comprobar que desaparecen para todos.
4. Recoger un caramelo azul, activar Azúcar y observar el efecto de cuatro segundos.
5. Recoger un frasco violeta, acercarse al rival y activar Bloqueo.
6. Esperar el final de la ronda y comprobar el mismo ganador en las dos pantallas.
7. Volver a la sala y empezar otra ronda con puntajes en cero.
8. Repetir con cuatro participantes y grabar las evidencias cuando funcione en los equipos.
