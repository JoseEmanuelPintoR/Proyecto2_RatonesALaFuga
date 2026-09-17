# Ratones a la fuga

Proyecto de videojuego 3D para Unity, basado en la propuesta que el grupo escogió como **Ladrones de queso**. Aquí ya se utiliza el nombre **Ratones a la fuga**.

Controlas un ratón en una cocina gigante y compites contra otros jugadores por conseguir más queso. La ronda dura dos minutos. Hay 50 quesos, un poder de velocidad y otro que deja pegado a un rival.

## Abrir el juego

1. Descomprime el ZIP completo.
2. En **Unity Hub → Projects → Add / Add project from disk**, selecciona la carpeta **Ratones_a_la_fuga**. Es la carpeta que contiene `Assets`, `Packages` y `ProjectSettings`.
3. Abre el proyecto con **Unity 6.0**. La versión registrada es **6000.0.62f1**. Si Hub pregunta por una versión, elige esa versión o una actualización de Unity 6.0 que tengas instalada. La ejecución con otras versiones todavía necesita comprobación.
4. Espera a que termine de importar los archivos.
5. En la barra superior, abre **Ratones a la fuga → 1. Abrir escena del juego**.
6. Ejecuta **Ratones a la fuga → 2. Configurar PC y Android** una vez y luego pulsa **Play**.

**La cocina, los ratones y la interfaz aparecen al pulsar Play.** Se construyen por código; es normal ver únicamente el objeto `Ratones a la fuga` antes de ejecutar la escena. No necesitas acomodar objetos ni conectar scripts a mano.

El proyecto utiliza el renderizador integrado de Unity (Built-in). Mantén ese renderizador; los shaders incluidos están preparados para él.

## Jugar con el grupo

La partida requiere **mínimo 2 y máximo 4 personas**. Con un solo jugador se muestra la sala, pero no comienza la ronda.

1. Conecta los computadores o celulares a la **misma red Wi-Fi**.
2. Cada persona abre su propia copia del juego y escribe un nombre.
3. Una persona pulsa **Crear sala**. La IP y el puerto aparecen en la sala.
4. Los demás escriben esa IP y el mismo puerto, y pulsan **Unirme**.
5. Cuando estén conectados, el anfitrión pulsa **Iniciar partida**.

El puerto inicial es **7777**, y puede cambiarse en la pantalla inicial. Si aparecen varias IP, utiliza la de la conexión Wi-Fi compartida. `127.0.0.1` sirve únicamente cuando las dos copias se ejecutan en el **mismo computador**.

Para probar dos jugadores en un PC: ejecuta una copia desde Unity y otra desde una compilación de escritorio. Crea la sala en una de ellas; en la otra usa `127.0.0.1`. Con las ventanas una al lado de la otra puedes alternar cuál controlas. No hace falta un programa de servidor separado: quien crea la sala también ejecuta el servidor.

## Controles

| Acción | Computador | Celular |
|---|---|---|
| Mover el ratón | WASD o flechas | Joystick izquierdo |
| Recoger un objeto | Acercarse al objeto | Acercarse al objeto |
| Subidón de Azúcar | E | Botón AZÚCAR |
| Trampa Pegajosa | Q | Botón BLOQUEO |
| Salir de la ronda | Esc o Menú | Menú |

Puedes usar el joystick con el mouse para revisar su funcionamiento desde el editor.

## Reglas implementadas

| Elemento | Comportamiento |
|---|---|
| Escenario | Cocina de 100 × 100 unidades, con muebles sólidos y límites. |
| Aparición de ratones | Posiciones aleatorias transitables, separadas al menos 14 unidades. |
| Quesos | Se generan 50 al inicio; cada uno suma un punto y desaparece para todos. |
| Tiempo | 3 segundos de preparación y 120 segundos de partida. |
| Victoria | Mayor puntaje al terminar el tiempo. |
| Empate | Gana quien llegó antes a ese puntaje; si también empatan en ese momento, hay victoria compartida. |
| Caramelo azul | Entrega una carga de Subidón de Azúcar. Velocidad ×1,5 durante 4 segundos. |
| Frasco violeta | Entrega una carga de Trampa Pegajosa. Inmoviliza durante 3 segundos al rival visible más cercano, a menos de 14 unidades. |
| Cargas | Máximo una de cada poder. Los objetos de poderes reaparecen después de 14 segundos. |
| Poder de bloqueo sin objetivo | No consume la carga y avisa que te acerques. No atraviesa muebles. |
| Durante el bloqueo | El ratón no se mueve, no recoge objetos y no activa poderes. |
| Desconexión de un cliente | Se elimina de la competencia; si queda menos de 2, se cancela la ronda sin declarar un ganador. |
| Salida del anfitrión | Se cierra la sala y los demás vuelven al menú con un aviso. |
| Revancha | El anfitrión vuelve a la sala e inicia otra ronda con puntajes y objetos nuevos. |

Si recogen todos los quesos antes del final, la ronda espera a que el temporizador llegue a cero. No se generan quesos nuevos durante esa ronda.

## Crear una aplicación para PC

1. En Unity, abre **File → Build Profiles**.
2. Selecciona tu plataforma de escritorio, por ejemplo Windows, y usa **Switch Platform** si hace falta.
3. Comprueba que esté seleccionada la escena `Assets/Ratones/Scenes/Ratones_a_la_fuga.unity`.
4. Pulsa **Build** y elige una carpeta para la compilación.
5. Al compartir una compilación de Windows, entrega toda la carpeta generada, incluido el directorio de datos, no solamente el `.exe`.

## Crear el APK para Android

1. En Unity Hub, añade a tu instalación **Android Build Support**, **Android SDK & NDK Tools** y **OpenJDK**.
2. Abre el proyecto y ejecuta **Ratones a la fuga → 2. Configurar PC y Android**.
3. Selecciona **Ratones a la fuga → 3. Crear APK Android** y elige dónde guardarlo.
4. Espera a que termine la compilación e instala el APK en los dispositivos del grupo para probarlo.

El comando configura orientación horizontal, permiso de Internet y compilación ARM64 con IL2CPP. Está pensado para pruebas en celulares Android ARM64. La generación del APK necesita que Unity y sus módulos Android estén instalados en tu computador.

La [guía oficial de compilación para Android](https://docs.unity3d.com/2022.3/Documentation/Manual/android-BuildProcess.html) describe los requisitos del proceso. El proyecto activa el [permiso de Internet de Android](https://docs.unity3d.com/6000.6/Documentation//ScriptReference/PlayerSettings.Android-forceInternetPermission.html) desde su configuración del editor.

## Si algo no funciona

| Lo que ocurre | Qué revisar |
|---|---|
| Solo hay un ratón en la sala | Abre una segunda copia del juego; el mínimo es dos. |
| No encuentra la sala | Revisa IP, puerto y que los dispositivos compartan red. Si el sistema pregunta por el firewall, permite la conexión en tu red privada. Algunas redes de la universidad aíslan los dispositivos; usa una red local que permita comunicarlos. |
| La sala aparece llena | Admite cuatro jugadores. Tampoco admite entradas cuando una ronda ya comenzó. |
| No responde el teclado o la interfaz | En Player Settings usa **Active Input Handling: Input Manager (Old)**; el menú de configuración ya lo selecciona. |
| No se ve la cocina antes de Play | Es normal: el escenario se crea al ejecutar. |
| Los materiales aparecen rosados | Comprueba que el proyecto siga usando Built-in y que exista `Assets/Resources/Ratones`. |
| Bajos FPS en el celular | Desactiva las sombras desde Opciones. |
| Sales de la sala al cambiar de aplicación | Se cierra la conexión cuando el juego pasa a segundo plano para evitar una partida con un anfitrión suspendido. |

## Qué puedes modificar

| Archivo o campo | Qué controla |
|---|---|
| `Assets/Ratones/Scripts/Core/GameRules.cs` | Duración, velocidad, alcance y duración de poderes. |
| `Assets/Ratones/Scripts/Core/MatchSimulation.cs` | Reglas, recolección, ganador y generación aleatoria. |
| `KitchenMap.Blocks` en `GameRules.cs` | Muebles y zonas de colisión. La geometría visual utiliza esas mismas medidas. |
| `Assets/Ratones/Scripts/Gameplay/KitchenView.cs` | Construcción de cocina, objetos y cámara. |
| `Assets/Ratones/Scripts/Gameplay/MouseView.cs` | Modelo y animaciones del ratón. |
| `Assets/Ratones/Scripts/Gameplay/GameHud.cs` | Pantallas, textos, joystick y botones. |
| Campos `Mouse Visual Prefab` y `Cheese Visual Prefab` del objeto principal | Permiten poner los modelos de Laura en lugar de los modelos generados. |

Para sustituir un modelo, crea un prefab con el origen al nivel del suelo, eje Y hacia arriba y eje Z hacia el frente. Usa escala 1 y un ratón de aproximadamente 3 unidades de largo. Arrástralo al campo correspondiente del objeto `Ratones a la fuga` en la escena. No necesita scripts de movimiento ni colliders propios, porque esas reglas ya las controla el servidor.

## Estado de verificación

Se compilaron y ejecutaron las clases de reglas y red con .NET 8. Pasaron **9 grupos de pruebas**, incluidos cuatro jugadores por TCP, generación válida de 250 mapas, recolección simultánea, poderes, desempates y revancha.

**Este archivo contiene el proyecto fuente. No incluye un APK ni un ejecutable de escritorio ya compilado.** En el entorno donde se preparó no estaba instalado Unity: quedan pendientes la importación y compilación dentro del editor, la revisión visual y las pruebas en celulares físicos. Consulta `Documentation/VALIDACION.md` para distinguir las comprobaciones hechas de las pendientes.

Los modelos y sonidos de esta versión se generan mediante código. Puedes sustituirlos por los recursos de tu equipo conservando el funcionamiento del juego.
